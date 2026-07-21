using FluentValidation;

namespace BoardGameAiDashboard.Application.Features.Games.Commands.CreateGame;

/// <summary>
/// FluentValidation rules for <see cref="CreateGameCommand"/>.
/// </summary>
public sealed class CreateGameCommandValidator : AbstractValidator<CreateGameCommand>
{
    public CreateGameCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Game name is required.")
            .MaximumLength(200).WithMessage("Game name must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Game description is required.")
            .MaximumLength(2000).WithMessage("Game description must not exceed 2000 characters.");

        RuleFor(x => x.MinPlayers)
            .InclusiveBetween(1, 100).WithMessage("MinPlayers must be between 1 and 100.");

        RuleFor(x => x.MaxPlayers)
            .InclusiveBetween(1, 100).WithMessage("MaxPlayers must be between 1 and 100.");

        RuleFor(x => x.MaxPlayers)
            .GreaterThanOrEqualTo(x => x.MinPlayers)
            .WithMessage("MaxPlayers must be greater than or equal to MinPlayers.");
    }
}
