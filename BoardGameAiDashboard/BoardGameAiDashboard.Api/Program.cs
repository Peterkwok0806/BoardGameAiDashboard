using System.Text.Json;
using BoardGameAiDashboard.Api.Filters;
using BoardGameAiDashboard.Api.Middleware;
using BoardGameAiDashboard.Application;
using BoardGameAiDashboard.Infrastructure;
using Hangfire;
using Hangfire.Dashboard;
using Serilog;
using Microsoft.EntityFrameworkCore;

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
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// ── Controllers + Swagger ───────────────────────────────────────────
builder.Services.AddControllers(options =>
{
    // Auto-wrap all successful responses in { success, data, timestamp } envelope
    options.Filters.Add<ApiResultFilter>();
})
.AddJsonOptions(options =>
{
    // Accept camelCase JSON from Angular frontend (e.g., "hourOfDay" instead of "HourOfDay")
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
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

// ── HTTPS Redirection (Production only — dev uses HTTP) ───────────────
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// ── CORS ────────────────────────────────────────────────────────────
app.UseCors("AllowAngular");

// ── Authentication & Authorization ──────────────────────────────────
app.UseAuthentication();
app.UseAuthorization();

// ── Map Endpoints ───────────────────────────────────────────────────
app.MapControllers();

// ── Health Check Endpoint ─────────────────────────────────────────────
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }));

// ── Auto-Apply Database Migrations (with retry for SQL Server startup delay) ──
bool isMigrationSuccessful = false;

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    // 取得資料庫上下文
    var context = services.GetRequiredService<BoardGameAiDashboard.Infrastructure.Persistence.ApplicationDbContext>();

    int retryCount = 5;          // 最大重試次數
    int delaySeconds = 5;        // 每次重試間隔時間

    for (int i = 1; i <= retryCount; i++)
    {
        try
        {
            logger.LogInformation("正在檢查資料庫遷移狀態... (嘗試第 {Current}/{Max} 次)", i, retryCount);

            // 檢查是否有未完成的遷移
            if (context.Database.GetPendingMigrations().Any())
            {
                logger.LogInformation("偵測到未套用的遷移，開始執行資料庫遷移...");
                context.Database.Migrate();
                logger.LogInformation("資料庫遷移已成功完成！");
            }
            else
            {
                logger.LogInformation("資料庫已是最新狀態，無需遷移。");
            }

            isMigrationSuccessful = true;
            break; // 成功則跳出循環
        }
        catch (Exception ex)
        {
            logger.LogWarning("第 {Current} 次資料庫遷移嘗試失敗。原因: {Message}", i, ex.Message);

            if (i < retryCount)
            {
                logger.LogInformation("等待 {Delay} 秒後重新嘗試...", delaySeconds);
                Thread.Sleep(TimeSpan.FromSeconds(delaySeconds));
            }
            else
            {
                logger.LogError(ex, "已達最大重試次數，資料庫自動遷移徹底失敗。");
            }
        }
    }
}

// 現在外面就能讀取到 isMigrationSuccessful 了
if (!isMigrationSuccessful)
{
    // 這裡使用常規的 Console 拋出異常，因為 scope 已經釋放，logger 無法安全使用
    throw new Exception("Database migration failed after max retries. API handles shutdown.");
}

app.Run();
