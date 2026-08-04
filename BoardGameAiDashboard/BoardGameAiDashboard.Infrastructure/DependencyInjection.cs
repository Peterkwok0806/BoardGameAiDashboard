using System.Text;
using BoardGameAiDashboard.Application.Common.Interfaces;
using BoardGameAiDashboard.Application.Features.ML.Interfaces;
using BoardGameAiDashboard.Infrastructure.Common.Repositories;
using BoardGameAiDashboard.Infrastructure.Persistence;
using BoardGameAiDashboard.Infrastructure.Services;
using BoardGameAiDashboard.Infrastructure.Services.Auth;
using BoardGameAiDashboard.Infrastructure.Services.ML;
using BoardGameAiDashboard.Infrastructure.Settings;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;
using Qdrant.Client;

namespace BoardGameAiDashboard.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── EF Core DbContext ──────────────────────────────────────────
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection")));

        // ── Generic Repository & Unit of Work ───────────────────────────
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // ── Redis Distributed Cache ────────────────────────────────────
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration["Redis:Connection"];
            options.InstanceName = "BoardGame_";
        });

        // ── Hangfire (Background Jobs) ─────────────────────────────────
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(
                configuration.GetConnectionString("DefaultConnection"),
                new SqlServerStorageOptions
                {
                    CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                    SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                    QueuePollInterval = TimeSpan.FromSeconds(15),
                    CountersAggregateInterval = TimeSpan.FromMinutes(5),
                    PrepareSchemaIfNecessary = true
                }));

        services.AddHangfireServer();

        // ── JWT Authentication ─────────────────────────────────────────
        var jwtSecret = configuration["Jwt:Secret"]!;
        var jwtIssuer = configuration["Jwt:Issuer"]!;
        var jwtAudience = configuration["Jwt:Audience"]!;

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false;
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                ClockSkew = TimeSpan.Zero // Remove default 5-minute grace period
            };
        });

        services.AddAuthorization();

        // ── Auth Services (Password Hashing + JWT) ──────────────────────
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        // ── Ollama Settings ───────────────────────────────────────────────
        services.Configure<OllamaSettings>(configuration.GetSection("Ollama"));

        // ── Qdrant Settings + Client ──────────────────────────────────────
        services.Configure<QdrantSettings>(configuration.GetSection("Qdrant"));
        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<QdrantSettings>>().Value;
            var uri = new Uri(settings.Endpoint);
            return new QdrantClient(uri.Host, uri.Port);
        });

        // ── Semantic Kernel (Ollama LLM + Embedding) ──────────────────────
        services.AddSingleton<Kernel>(sp =>
        {
            var ollamaSettings = sp.GetRequiredService<IOptions<OllamaSettings>>().Value;

            var builder = Kernel.CreateBuilder();

            // Chat completion via Ollama
            builder.Services.AddOllamaChatCompletion(
                ollamaSettings.ChatModel,
                new Uri(ollamaSettings.Endpoint));

            // Text embedding via Ollama
            builder.Services.AddOllamaTextEmbeddingGeneration(
                ollamaSettings.EmbeddingModel,
                new Uri(ollamaSettings.Endpoint));

            return builder.Build();
        });

        // ── Bridge Registration ──────────────────────────────────────────
        // SK's Kernel.CreateBuilder() creates an isolated ServiceProvider.
        // Services registered inside (IChatCompletionService, ITextEmbeddingGenerationService)
        // are NOT visible to the outer DI container.
        // Bridge: extract them from the Kernel's internal SP and re-register
        // as singletons in the outer DI so they can be injected directly.
        services.AddSingleton(sp =>
        {
            var kernel = sp.GetRequiredService<Kernel>();
            return kernel.GetRequiredService<IChatCompletionService>();
        });

        services.AddSingleton(sp =>
        {
            var kernel = sp.GetRequiredService<Kernel>();
            return kernel.GetRequiredService<ITextEmbeddingGenerationService>();
        });

        // ── PDF Parsing ───────────────────────────────────────────────────
        services.AddScoped<IPdfParser, PdfPigPdfParser>();

        // ── RAG Services ──────────────────────────────────────────────────
        services.AddScoped<IDocumentChunker, DocumentChunker>();
        services.AddScoped<IVectorSearchService, VectorSearchService>();
        services.AddScoped<IQueryRewriter, LlmQueryRewriter>();
        services.AddScoped<IDocumentIngestionService, DocumentIngestionService>();
        services.AddScoped<IRagService, RagService>();

        // ── ML Prediction Services ──────────────────────────────────────────
        services.Configure<MLSettings>(configuration.GetSection(MLSettings.SectionName));

        // FeatureEngineeringService is stateless — register as Singleton for efficiency
        services.AddSingleton<IFeatureEngineeringService, FeatureEngineeringService>();

        // CsvExportService needs IUnitOfWork (Scoped) — must be Scoped
        services.AddScoped<ICsvExportService, CsvExportService>();

        // OnnxRuntime InferenceSession is thread-safe, use Singleton
        services.AddSingleton<IWinRatePredictionService, WinRatePredictionService>();

        return services;
    }
}
