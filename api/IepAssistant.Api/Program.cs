using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using Elastic.Ingest.Elasticsearch;
using Elastic.Ingest.Elasticsearch.DataStreams;
using Elastic.Serilog.Sinks;
using Elastic.Transport;
using IepAssistant.Api.Middleware;
using IepAssistant.Api.Seeding;
using IepAssistant.Domain;
using IepAssistant.Domain.Data;
using IepAssistant.Api.BackgroundServices;
using IepAssistant.Services;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

// QuestPDF runs under the free Community license (org is under the $1M-revenue threshold). Set once
// at startup before any rendering — the P5b PDF worker generates IepVersion PDFs headless.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog with Elasticsearch
var logConfiguration = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .WriteTo.Console();

if (builder.Environment.IsProduction())
{
    var elasticUrl = builder.Configuration["Elastic:Url"];
    var elasticUsername = builder.Configuration["Elastic:Username"];
    var elasticPassword = builder.Configuration["Elastic:Password"];

    if (!string.IsNullOrEmpty(elasticUrl) && !string.IsNullOrEmpty(elasticUsername) && !string.IsNullOrEmpty(elasticPassword))
    {
        logConfiguration
            .WriteTo.Elasticsearch(new[] { new Uri(elasticUrl) }, opts =>
            {
                opts.DataStream = new DataStreamName("app-logs", "iepadvisor-api", "production");
                opts.BootstrapMethod = BootstrapMethod.Failure;
            }, transport =>
            {
                transport.Authentication(new BasicAuthentication(elasticUsername, elasticPassword));
            });

        builder.Services.AddAllElasticApm();
    }
}

Log.Logger = logConfiguration.CreateLogger();
builder.Host.UseSerilog();

// Add layers via extension methods
builder.Services.AddDomain(builder.Configuration);
builder.Services.AddServices();

// Pilot-gates plan, phase 2: backs the signed account-deletion-cancellation link (AccountService /
// POST /api/account/cancel-deletion). SetApplicationName pins the key ring's discriminator explicitly
// rather than relying on ASP.NET Core's default (derived from ContentRootPath), which can differ across
// deploy slots/hosts and would otherwise make a token minted on one instance fail to validate on another.
builder.Services.AddDataProtection().SetApplicationName("IepAssistant");

// Email:ExposeLinksForTesting gate (e2e/testing convenience). Enabling it surfaces raw invite URLs in
// API responses, so it is allowed ONLY in Development AND only when no real ACS connection string is set.
// Startup is the one place IHostEnvironment is in scope, so any attempt to enable it outside Development
// is logged and IGNORED here — making non-Development exposure impossible regardless of config.
{
    var exposeRequested = builder.Configuration.GetValue<bool>("Email:ExposeLinksForTesting");
    var acsConnectionEmpty = string.IsNullOrEmpty(builder.Configuration["Email:ConnectionString"]);
    var isDevelopment = builder.Environment.IsDevelopment();
    var exposeEnabled = exposeRequested && acsConnectionEmpty && isDevelopment;

    if (exposeRequested && !exposeEnabled)
    {
        Log.Warning(
            "Email:ExposeLinksForTesting=true ignored — it requires Development environment (is={IsDev}) and an empty Email:ConnectionString (empty={AcsEmpty}).",
            isDevelopment, acsConnectionEmpty);
    }

    builder.Services.AddSingleton(new IepAssistant.Services.Security.InviteLinkExposure(exposeEnabled));
}

// Anthropic model/effort configuration. ValidateOnStart is deliberate for MODEL and EFFORT: a
// blank model or a typo'd effort is exactly the class of defect that took analysis down, it is
// unfixable at runtime, and a fail-fast boot error is far cheaper than a 404 found by the first
// user to run an analysis.
//
// ApiKey is deliberately NOT validated here — see AnthropicOptions.ApiKey. Failing boot over a
// missing key would take down login, billing, and uploads for a credential only the AI features
// need, and this pipeline deploys straight to Production with no staging slot. ClaudeClient's
// blank-key guard scopes that failure to the AI features instead.
builder.Services.AddOptions<AnthropicOptions>()
    .Bind(builder.Configuration.GetSection(AnthropicOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Named HttpClient for Claude API calls (avoids socket exhaustion from new HttpClient per request).
// Timeout is generous because long-document (30+ page ETR/IEP) non-streaming responses with
// large output token budgets can take several minutes. Consider switching to streaming if this
// becomes a sustained issue.
//
// todos/P3-01 #11: Anthropic.SDK stamps the API key onto this client's DefaultRequestHeaders
// (x-api-key) rather than passing it per-request, so any resilience/logging handler added to this
// HttpClientBuilder later must NOT log request headers — that would put the API key in every log
// line for every Claude call. Separately: Anthropic.SDK ships an opt-in LoggingRequestInterceptor
// that logs full request bodies (IEP/ETR content, child names). ClaudeClient correctly uses the
// 2-arg AnthropicClient(apiKey, httpClient) constructor, which does not attach it — keep it that way.
builder.Services.AddHttpClient("Claude", client =>
{
    client.Timeout = TimeSpan.FromMinutes(15);
});

// Background processing
builder.Services.AddSingleton<IepProcessingQueue>();
builder.Services.AddHostedService<IepProcessingWorker>();
builder.Services.AddSingleton<EtrProcessingQueue>();
builder.Services.AddHostedService<EtrProcessingWorker>();
builder.Services.AddSingleton<IepAnalysisQueue>();
builder.Services.AddHostedService<IepAnalysisWorker>();
builder.Services.AddSingleton<EtrAnalysisQueue>();
builder.Services.AddHostedService<EtrAnalysisWorker>();
builder.Services.AddSingleton<MeetingPrepQueue>();
builder.Services.AddHostedService<MeetingPrepWorker>();
builder.Services.AddSingleton<ProgressReportAnalysisQueue>();
builder.Services.AddHostedService<ProgressReportAnalysisWorker>();
builder.Services.AddSingleton<AnalysisRunQueue>();
builder.Services.AddHostedService<AnalysisRunWorker>();
builder.Services.AddSingleton<IepVersionPdfQueue>();
builder.Services.AddHostedService<IepVersionPdfWorker>();
builder.Services.AddSingleton<AuthoredDocumentPdfQueue>();
builder.Services.AddHostedService<AuthoredDocumentPdfWorker>();
// One-off, idempotent legacy-analysis backfill (runs once at startup; skips already-migrated rows).
builder.Services.AddHostedService<AnalysisRunBackfillHostedService>();
// One-off, idempotent default IEP template seed (Phase 5): reproduces the legacy typed IEP structure so
// new IEP drafts resolve through the generic template engine. Skips if the default already exists.
builder.Services.AddHostedService<DefaultIepTemplateSeederHostedService>();
// FERPA-aligned access logging (P6a): singleton fire-and-forget enqueue + hosted drain-and-insert.
builder.Services.AddSingleton<AuditLogger>();
builder.Services.AddSingleton<IAuditLogger>(sp => sp.GetRequiredService<AuditLogger>());
builder.Services.AddHostedService<AccessAuditLogWorker>();
// Pilot-gates plan, phase 1: nightly (+ on-demand) audit hash-chain integrity walk.
builder.Services.AddHostedService<AuditIntegrityWorker>();
// Pilot-gates plan, phase 1: outbound email queue — IEmailService only composes and enqueues now;
// this worker is the only real sender (see IEmailTransport/AcsEmailTransport).
builder.Services.AddHostedService<OutboundEmailWorker>();
// Pilot-gates plan, phase 2: purges parent/staff accounts 30+ days past DeletionRequestedAt.
builder.Services.AddHostedService<AccountPurgeWorker>();
// Phase 3: warns the inviting admin ~3 days before a pending staff invite expires (daily timer; scoped
// per-invite processing). All decision logic lives in IStaffInviteExpiryService; single-instance assumption
// is documented on the worker.
builder.Services.AddHostedService<StaffInviteExpiryWorker>();
// Plan 4: meetings, deadlines, notifications, calendar. All three workers push their scheduling-only
// shell down to a scoped service (INotificationEmailService/IMeetingReminderService/IDigestService) so
// the actual decision logic is unit-testable without a timer; single-instance assumption documented on
// NotificationEmailWorker mirrors StaffInviteExpiryWorker's.
builder.Services.AddHostedService<NotificationEmailWorker>();
builder.Services.AddHostedService<MeetingReminderWorker>();
builder.Services.AddHostedService<DigestWorker>();
builder.Services.AddHostedService<EvaluatorOverdueWorker>();
// Plan 7 phase 4: district/student data export — builds a ZIP off a queue, mirrors AuthoredDocumentPdfWorker.
builder.Services.AddSingleton<ExportQueue>();
builder.Services.AddHostedService<ExportWorker>();
// Pilot-gates plan, phase 3: `dotnet run --project IepAssistant.Api -- seed-demo [--reset]` — see the
// args check below, after the host is built.
builder.Services.AddScoped<IDemoSeeder, DemoSeeder>();

// Add controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// Configure OpenAPI (.NET 9)
builder.Services.AddOpenApi();

// Configure JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT Key must be configured in appsettings.json");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "IepAssistant.Api";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "IepAssistant.Client";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero
    };

    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            // Reject MFA pending tokens used as regular auth
            var tokenType = context.Principal?.FindFirst("token_type")?.Value;
            if (tokenType == "mfa_pending")
            {
                context.Fail("MFA pending tokens cannot be used for authorization.");
                return;
            }

            // Validate SecurityStamp
            var stampClaim = context.Principal?.FindFirst("SecurityStamp")?.Value;
            var userIdClaim = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (stampClaim != null && userIdClaim != null && int.TryParse(userIdClaim, out var userId))
            {
                var dbContext = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
                var user = await dbContext.Users.FindAsync(userId);
                if (user == null || !user.IsActive || user.SecurityStamp.ToString() != stampClaim)
                {
                    context.Fail("Token has been revoked.");
                    return;
                }
            }
        }
    };
});

builder.Services.AddAuthorization();

// Forwarded headers — required so the rate limiter partitions on the real client IP rather than the
// Azure App Service front-end proxy. Trust model: the App Service platform front end OVERWRITES any
// client-supplied X-Forwarded-For with the observed remote IP (client spoofing is stripped), so we
// clear the default KnownNetworks/KnownProxies allow-lists (which would otherwise reject the platform
// hop and leave RemoteIpAddress as the proxy) and set ForwardLimit = 1 to honor ONLY the closest
// (platform-appended) hop. Configured here, applied EARLY in the pipeline via UseForwardedHeaders.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Rate limiting — disabled in Development via appsettings
var disableRateLimiting = builder.Configuration.GetValue<bool>("RateLimiting:Disabled");
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("login", context =>
        disableRateLimiting
            ? RateLimitPartition.GetNoLimiter<string>("")
            : RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(15),
                    SegmentsPerWindow = 3
                }));

    options.AddPolicy("mfa", context =>
        disableRateLimiting
            ? RateLimitPartition.GetNoLimiter<string>("")
            : RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(15),
                    SegmentsPerWindow = 3
                }));

    options.AddPolicy("password-reset", context =>
        disableRateLimiting
            ? RateLimitPartition.GetNoLimiter<string>("")
            : RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 3,
                    Window = TimeSpan.FromHours(1),
                    SegmentsPerWindow = 6
                }));

    // Pilot-gates plan, phase 3: defense-in-depth IP cap on magic-link requests, on top of
    // IMagicLinkService's own per-user 5-per-15-min limit (the built-in limiter partitions by
    // connection, not request-body email, so it can't enforce the per-email limit itself).
    options.AddPolicy("magic-link", context =>
        disableRateLimiting
            ? RateLimitPartition.GetNoLimiter<string>("")
            : RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(15),
                    SegmentsPerWindow = 3
                }));

    // todos/P2-02: caps how fast one authenticated user can create analysis runs. Every run is a fully-
    // billed Claude call regardless of outcome (including an InvalidResponse from a document engineered
    // to make Claude's output unparseable), and the per-child quota alone does not stop the same user
    // from spraying create requests across many children. Partitions on user id (not IP) since this is
    // an authenticated-only endpoint.
    options.AddPolicy("analysis-run", context =>
        disableRateLimiting
            ? RateLimitPartition.GetNoLimiter<string>("")
            : RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey: context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 20,
                    Window = TimeSpan.FromMinutes(15),
                    SegmentsPerWindow = 3
                }));

    // Virtual Advocate messages: each one is a multi-round Claude tool loop and the per-year usage cap is
    // enforced in the service, but a burst of sends could still run many concurrent streams for one user.
    // 30 / 15 min per user, on top of the yearly cap.
    options.AddPolicy("advocate-message", context =>
        disableRateLimiting
            ? RateLimitPartition.GetNoLimiter<string>("")
            : RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey: context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 30,
                    Window = TimeSpan.FromMinutes(15),
                    SegmentsPerWindow = 3
                }));

    // Unauthenticated district self-serve signup — very tight per-IP cap (3 / hour, fixed window) since
    // each success provisions a brand-new District + DistrictAdmin. Depends on UseForwardedHeaders to see
    // the real client IP behind the App Service front end.
    options.AddPolicy("register-district", context =>
        disableRateLimiting
            ? RateLimitPartition.GetNoLimiter<string>("")
            : RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 3,
                    Window = TimeSpan.FromHours(1)
                }));
});

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? new[] { "http://localhost:5200", "http://localhost:3000" };
        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Add Health Checks
builder.Services.AddHealthChecks()
    .AddSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")!);

var app = builder.Build();

// Pilot-gates plan, phase 3: `dotnet run --project IepAssistant.Api -- seed-demo [--reset|--fresh]`.
// Handled right after the host is built (so the full DI graph — every real service DemoSeeder drives — is
// available) and before the normal request pipeline is configured; the process exits here instead of
// falling through to app.Run(). Refused in Production (fictional PII-shaped data has no business there).
//
//   seed-demo           create the demo district (no-op if one already exists)
//   seed-demo --reset   remove it
//   seed-demo --fresh   remove and recreate it — the one command to run between demos
// The verb is looked for anywhere in args, not just at args[0]: `dotnet run --urls … -- seed-demo`
// forwards the host options AHEAD of the verb, and silently starting a web server instead of seeding is a
// trap worth closing. Only the arguments AFTER the verb are read as seeder options.
var seedVerbIndex = Array.FindIndex(args, a => string.Equals(a, "seed-demo", StringComparison.OrdinalIgnoreCase));
if (seedVerbIndex >= 0)
{
    if (app.Environment.IsProduction())
    {
        Console.Error.WriteLine("seed-demo is refused in Production.");
        Environment.Exit(1);
    }

    var flags = args.Skip(seedVerbIndex + 1).ToList();
    var unknown = flags.FirstOrDefault(a =>
        !a.Equals("--reset", StringComparison.OrdinalIgnoreCase) &&
        !a.Equals("--fresh", StringComparison.OrdinalIgnoreCase) &&
        !a.Equals("--reseed", StringComparison.OrdinalIgnoreCase));
    if (unknown != null)
    {
        Console.Error.WriteLine($"Unknown option '{unknown}'. Usage: seed-demo [--reset | --fresh]");
        Environment.Exit(1);
    }

    var fresh = flags.Any(a => a.Equals("--fresh", StringComparison.OrdinalIgnoreCase)
                               || a.Equals("--reseed", StringComparison.OrdinalIgnoreCase));
    var resetOnly = !fresh && flags.Any(a => a.Equals("--reset", StringComparison.OrdinalIgnoreCase));

    // Hosted services (PDF render worker, outbound email worker, audit worker, …) must actually be
    // running during the seed so a finalized IEP's PDF really gets queued and rendered, exactly as it
    // would from a live request — StartAsync/StopAsync brackets the CLI run the same way a normal
    // request's background processing would happen around it, and StopAsync's graceful shutdown flush
    // (e.g. AccessAuditLogWorker) still runs before the process exits.
    await app.StartAsync();

    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

    // --fresh runs both operations in one process, each in its own DI scope: the reset's DbContext has
    // tracked and deleted the old district's entities, and the seed must start from a clean change tracker.
    DemoSeedResult? resetResult = null;
    if (resetOnly || fresh)
    {
        using var resetScope = app.Services.CreateScope();
        resetResult = await resetScope.ServiceProvider.GetRequiredService<IDemoSeeder>().ResetAsync();
    }

    // A reset-only run reports the reset as its outcome; so does a --fresh whose reset failed, since
    // seeding on top of a half-removed district would only compound the failure.
    DemoSeedResult seedResult;
    if (resetOnly || resetResult is { Success: false })
    {
        seedResult = resetResult!;
    }
    else
    {
        using var seedScope = app.Services.CreateScope();
        seedResult = await seedScope.ServiceProvider.GetRequiredService<IDemoSeeder>().SeedAsync();
    }
    stopwatch.Stop();

    await app.StopAsync();

    Console.WriteLine();
    if (resetResult != null && !ReferenceEquals(resetResult, seedResult))
        Console.WriteLine(resetResult.Message);
    Console.WriteLine(seedResult.Message);
    Console.WriteLine($"Elapsed: {stopwatch.Elapsed.TotalSeconds:F1}s");

    if (seedResult.Logins.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("Demo logins (also kept in docs/demo/maple-ridge-demo-district.md):");
        Console.WriteLine($"{"Role",-58}{"Email",-46}Password");
        foreach (var login in seedResult.Logins)
            Console.WriteLine($"{login.Role,-58}{login.Email,-46}{login.Password}");
    }

    Environment.Exit(seedResult.Success ? 0 : 1);
}

// Pilot-gates plan, phase 1, decision 2: outside Development, sending must be genuinely configured —
// an empty Email:ConnectionString there means every queued email will fail (see AcsEmailTransport),
// not silently "succeed" the way the Development fake-send path does. One WARN banner at startup, not
// per-email, so it is visible in a deploy's logs without spamming them per send attempt.
if (!app.Environment.IsDevelopment() && string.IsNullOrEmpty(builder.Configuration["Email:ConnectionString"]))
{
    Log.Warning("Email delivery is not configured (Email:ConnectionString is empty) outside Development — outbound emails will be queued but will fail to send until it is set.");
}

// Initialize database (only in development)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    DbInitializer.Initialize(context);
}

// Forwarded headers FIRST — rewrites RemoteIpAddress/scheme from the App Service front-end proxy's
// X-Forwarded-* headers before any IP-sensitive middleware (rate limiting) or logging runs.
app.UseForwardedHeaders();

// Global exception handling
app.UseMiddleware<GlobalExceptionMiddleware>();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "IepAssistant API";
        options.Theme = ScalarTheme.BluePlanet;
    });
}

app.UseCors("AllowFrontend");

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
