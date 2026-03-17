using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

// Named HttpClient for Claude API calls (avoids socket exhaustion from new HttpClient per request)
builder.Services.AddHttpClient("Claude", client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
});

// Background processing
builder.Services.AddSingleton<IepProcessingQueue>();
builder.Services.AddHostedService<IepProcessingWorker>();
builder.Services.AddSingleton<IepAnalysisQueue>();
builder.Services.AddHostedService<IepAnalysisWorker>();
builder.Services.AddSingleton<MeetingPrepQueue>();
builder.Services.AddHostedService<MeetingPrepWorker>();

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
