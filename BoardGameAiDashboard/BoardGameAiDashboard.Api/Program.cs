using BoardGameAiDashboard.Api.Filters;
using BoardGameAiDashboard.Api.Middleware;
using BoardGameAiDashboard.Application;
using BoardGameAiDashboard.Infrastructure;
using Hangfire;
using Hangfire.Dashboard;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog Structured Logging ──────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// ── Infrastructure Services (EF Core, Redis, Hangfire, JWT) ─────────
builder.Services.AddInfrastructure(builder.Configuration);

// ── Application Services (MediatR, FluentValidation, AutoMapper) ────
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(IApplicationAssemblyMarker).Assembly);
});

// ── CORS for Angular Frontend ───────────────────────────────────────
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:4200" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ── Controllers + Swagger ───────────────────────────────────────────
builder.Services.AddControllers(options =>
{
    // Auto-wrap all successful responses in { success, data, timestamp } envelope
    options.Filters.Add<ApiResultFilter>();
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter your JWT token"
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// ── Custom Exception Handling Middleware (RFC 7807 ProblemDetails) ───
app.UseMiddleware<ExceptionHandlingMiddleware>();

// ── Custom Request Logging Middleware (Serilog) ─────────────────────
app.UseMiddleware<RequestLoggingMiddleware>();

// ── Swagger (always available in dev, can be enabled in prod too) ───
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ── Hangfire Dashboard (dev only, requires auth) ────────────────────
if (app.Environment.IsDevelopment())
{
    app.MapHangfireDashboard("/hangfire", new DashboardOptions
    {
        // In production, add proper authorization here
        DashboardTitle = "BoardGame AI Dashboard - Jobs"
    });
}

app.UseHttpsRedirection();

// ── CORS ────────────────────────────────────────────────────────────
app.UseCors("AllowAngular");

// ── Authentication & Authorization ──────────────────────────────────
app.UseAuthentication();
app.UseAuthorization();

// ── Map Endpoints ───────────────────────────────────────────────────
app.MapControllers();

app.Run();
