using BoardGameAiDashboard.Application.Features.Games.Commands.CreateGame;
using BoardGameAiDashboard.Application.Features.Games.Commands.UpdateGame;
using BoardGameAiDashboard.Application.Features.Games.Queries.GetGames;
using BoardGameAiDashboard.Application.Features.Games.Queries.GetGameById;
using BoardGameAiDashboard.Domain.Entities;
using AutoMapper;

namespace BoardGameAiDashboard.Application.Features.Games.Common;

/// <summary>
/// AutoMapper profile for <see cref="Game"/> entity mappings.
/// Maps between domain entity and command/query DTOs.
/// </summary>
public class GameMappings : Profile
{
    public GameMappings()
    {
        // ── Query Mappings ────────────────────────────────────────────
        CreateMap<Game, GameDto>();
        CreateMap<Game, GameDetailDto>()
            .ForMember(dest => dest.RuleChunkCount,
                opt => opt.MapFrom(src => src.RuleChunks.Count))
            .ForMember(dest => dest.CharacterCount,
                opt => opt.MapFrom(src => src.Characters.Count))
            .ForMember(dest => dest.CardCount,
                opt => opt.MapFrom(src => src.Cards.Count))
            .ForMember(dest => dest.MatchHistoryCount,
                opt => opt.MapFrom(src => src.MatchHistories.Count));

        // ── Command Mappings ──────────────────────────────────────────
        CreateMap<CreateGameCommand, Game>()
            .ConstructUsing(src => new Game(
                src.Name,
                src.Description,
                src.MinPlayers,
                src.MaxPlayers));

        // ── Response Mappings ─────────────────────────────────────────
        CreateMap<Game, CreateGameCommandResponse>();
        CreateMap<Game, UpdateGameCommandResponse>();
    }
}
