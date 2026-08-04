using BoardGameAiDashboard.Application.Features.ML.Models;
using FluentValidation;

namespace BoardGameAiDashboard.Application.Features.ML.Commands.PredictWinRate;

/// <summary>
/// FluentValidation rules for <see cref="PredictWinRateCommand"/>.
/// </summary>
public sealed class PredictWinRateCommandValidator : AbstractValidator<PredictWinRateCommand>
{
    public PredictWinRateCommandValidator()
    {
        RuleFor(x => x.Input)
            .NotNull().WithMessage("Input data is required.")
            .SetValidator(new GameStatePredictionInputValidator());
    }
}

/// <summary>
/// FluentValidation rules for <see cref="GameStatePredictionInput"/>.
/// </summary>
public sealed class GameStatePredictionInputValidator : AbstractValidator<GameStatePredictionInput>
{
    public GameStatePredictionInputValidator()
    {
        // Player count: typical board games have 2-10 players
        RuleFor(x => x.PlayerCount)
            .InclusiveBetween(1, 10)
            .WithMessage("PlayerCount must be between 1 and 10.");

        // Time-based features
        RuleFor(x => x.HourOfDay)
            .InclusiveBetween(0, 23)
            .WithMessage("HourOfDay must be between 0 and 23.");

        RuleFor(x => x.DayOfWeek)
            .InclusiveBetween(0, 6)
            .WithMessage("DayOfWeek must be between 0 (Sunday) and 6 (Saturday).");

        // Hero-related features must be non-negative
        RuleFor(x => x.HeroLevel)
            .GreaterThanOrEqualTo(0)
            .WithMessage("HeroLevel must be non-negative.");

        RuleFor(x => x.HeroKills)
            .GreaterThanOrEqualTo(0)
            .WithMessage("HeroKills must be non-negative.");

        RuleFor(x => x.Deaths)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Deaths must be non-negative.");

        RuleFor(x => x.UnitKills)
            .GreaterThanOrEqualTo(0)
            .WithMessage("UnitKills must be non-negative.");

        // Economy features must be non-negative
        RuleFor(x => x.TotalGold)
            .GreaterThanOrEqualTo(0)
            .WithMessage("TotalGold must be non-negative.");

        // Combat stats must be non-negative
        RuleFor(x => x.HighestAtk)
            .GreaterThanOrEqualTo(0)
            .WithMessage("HighestAtk must be non-negative.");

        RuleFor(x => x.HighestDef)
            .GreaterThanOrEqualTo(0)
            .WithMessage("HighestDef must be non-negative.");

        RuleFor(x => x.HighestSpeed)
            .GreaterThanOrEqualTo(0)
            .WithMessage("HighestSpeed must be non-negative.");

        RuleFor(x => x.AtkRange)
            .GreaterThanOrEqualTo(0)
            .WithMessage("AtkRange must be non-negative.");

        // Sanity checks: reasonable upper bounds for game features
        RuleFor(x => x.HeroLevel)
            .LessThanOrEqualTo(30)
            .WithMessage("HeroLevel seems unreasonably high (max 30).");

        RuleFor(x => x.TotalGold)
            .LessThanOrEqualTo(100000)
            .WithMessage("TotalGold exceeds reasonable limit (max 100,000).");
    }
}
