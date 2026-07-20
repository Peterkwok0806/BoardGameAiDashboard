using System.Reflection;
using BoardGameAiDashboard.Application.Common.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace BoardGameAiDashboard.Application;

/// <summary>
/// Composition root for the Application layer.
/// Registers MediatR, FluentValidation, and pipeline behaviors.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // ── MediatR ─────────────────────────────────────────────────
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(assembly));

        // ── Pipeline Behaviors (order matters — logging first, then validation) ──
        services.AddTransient(typeof(IPipelineBehavior<,>),
            typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>),
            typeof(ValidationBehavior<,>));

        // ── FluentValidation ─────────────────────────────────────────
        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
