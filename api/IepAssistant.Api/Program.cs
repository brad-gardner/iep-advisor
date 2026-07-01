using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
using IepAssistant.Domain;
using IepAssistant.Domain.Data;
using IepAssistant.Api.BackgroundServices;
using IepAssistant.Services;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Interfaces;

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

// Named HttpClient for Claude API calls (avoids socket exhaustion from new HttpClient per request).
// Timeout is generous because long-document (30+ page ETR/IEP) non-streaming responses with
// large output token budgets can take several minutes. Consider switching to streaming if this
// becomes a sustained issue.
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
// One-off, idempotent legacy-analysis backfill (runs once at startup; skips already-migrated rows).
builder.Services.AddHostedService<AnalysisRunBackfillHostedService>();
// FERPA-aligned access logging (P6a): singleton fire-and-forget enqueue + hosted drain-and-insert.
builder.Services.AddSingleton<AuditLogger>();
builder.Services.AddSingleton<IAuditLogger>(sp => sp.GetRequiredService<AuditLogger>());
builder.Services.AddHostedService<AccessAuditLogWorker>();
// Phase 3: warns the inviting admin ~3 days before a pending staff invite expires (daily timer; scoped
// per-invite processing). All decision logic lives in IStaffInviteExpiryService; single-instance assumption
// is documented on the worker.
builder.Services.AddHostedService<StaffInviteExpiryWorker>();

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
