using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Polly;
using WeatherTestApp.Api.Configuration;
using WeatherTestApp.Api.Infrastructure;
using WeatherTestApp.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ────────────────────────────────────────────────────────────
var weatherApiOptions = builder.Configuration
    .GetSection(WeatherApiOptions.SectionName)
    .Get<WeatherApiOptions>()
    ?? throw new InvalidOperationException($"Missing configuration section '{WeatherApiOptions.SectionName}'");

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Missing configuration key 'Jwt:Key'");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "WeatherTestApp";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "WeatherTestApp";

// ── Controllers + ProblemDetails ─────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// ── Health Checks (used by K8s liveness/readiness probes) ────────────────────
builder.Services.AddHealthChecks();

// ── Memory Cache ─────────────────────────────────────────────────────────────
builder.Services.AddMemoryCache();

// ── HttpClient (factory) + Resilience (Retry + Circuit Breaker) ──────────────
builder.Services.AddHttpClient<IWeatherApiClient, WeatherApiClient>(client =>
{
    client.BaseAddress = new Uri(weatherApiOptions.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddResilienceHandler("weather-api", pipeline =>
{
    // Retry: 3 attempts, exponential backoff 1s/2s/4s, on transient HTTP errors
    pipeline.AddRetry(new HttpRetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromSeconds(1),
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true,
        ShouldHandle = args => ValueTask.FromResult(
            HttpClientResiliencePredicates.IsTransient(args.Outcome))
    });

    // Circuit Breaker: opens after 3 failures in a 30s window, stays open 15s
    pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
    {
        SamplingDuration = TimeSpan.FromSeconds(30),
        MinimumThroughput = 3,
        FailureRatio = 0.5,
        BreakDuration = TimeSpan.FromSeconds(15),
        ShouldHandle = args => ValueTask.FromResult(
            HttpClientResiliencePredicates.IsTransient(args.Outcome))
    });
});

// ── Application Services ─────────────────────────────────────────────────────
builder.Services.AddScoped<ITemperatureService, TemperatureService>();

// ── JWT Bearer Authentication ─────────────────────────────────────────────────
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

// ── Swagger / OpenAPI ─────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Weather Temperature API",
        Version = "v1",
        Description = "Returns current temperature for supported cities (bratislava, praha, budapest, vieden)."
    });

    // JWT auth in Swagger UI
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT Bearer token."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });

    // Include XML comments
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);
});

// ── Build ─────────────────────────────────────────────────────────────────────
var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Weather Temperature API v1"));
}

app.UseAuthentication();
app.UseAuthorization();

// ── Dev-only token endpoint ───────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.MapGet("/dev/token", () =>
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: [new Claim(ClaimTypes.Name, "dev-user")],
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds);
        return Results.Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token) });
    }).WithTags("Dev").WithSummary("Generate a dev JWT token (Development only)");
}

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
