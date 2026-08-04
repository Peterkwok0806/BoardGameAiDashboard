using BoardGameAiDashboard.Application.Features.ML.Interfaces;
using BoardGameAiDashboard.Application.Features.ML.Models;

namespace BoardGameAiDashboard.Infrastructure.Services.ML;

/// <summary>
/// Feature engineering service that MUST match Python feature_engineering.py exactly.
/// Produces 20 features from 12 raw inputs.
/// </summary>
public sealed class FeatureEngineeringService : IFeatureEngineeringService
{
    // Feature columns in exact order (must match Python's feature_engineering.py)
    private static readonly string[] FeatureColumnsList =
    [
        // Raw features (12)
        "player_count",
        "hour_of_day",
        "day_of_week",
        "hero_level",
        "hero_kills",
        "deaths",
        "unit_kills",
        "total_gold",
        "highest_atk",
        "highest_def",
        "highest_speed",
        "atk_range",
        // Derived features (8)
        "gold_per_level",
        "atk_per_level",
        "def_per_level",
        "speed_per_level",
        "kd_ratio",
        "total_kills",
        "gold_efficiency",
        "death_ratio"
    ];

    /// <inheritdoc />
    public IReadOnlyList<string> FeatureColumns => FeatureColumnsList;

    /// <inheritdoc />
    public float[] TransformToFeatureVector(GameStatePredictionInput input)
    {
        // Extract raw values
        var playerCount = input.PlayerCount;
        var heroLevel = input.HeroLevel;
        var heroKills = input.HeroKills;
        var deaths = input.Deaths;
        var unitKills = input.UnitKills;
        var totalGold = input.TotalGold;
        var highestAtk = input.HighestAtk;
        var highestDef = input.HighestDef;
        var highestSpeed = input.HighestSpeed;

        // Handle edge cases to prevent division by zero or infinity
        var levelPlusOne = heroLevel + 1;
        var deathsPlusOne = deaths + 1;
        var playerCountPlusOne = playerCount + 1;
        var heroKillsPlusOne = heroKills + 1;

        return
        [
            // Raw features (12)
            playerCount,                    // 0: player_count
            input.HourOfDay,                // 1: hour_of_day
            input.DayOfWeek,                // 2: day_of_week
            heroLevel,                      // 3: hero_level
            heroKills,                      // 4: hero_kills
            deaths,                         // 5: deaths
            unitKills,                      // 6: unit_kills
            totalGold,                      // 7: total_gold
            highestAtk,                     // 8: highest_atk
            highestDef,                     // 9: highest_def
            highestSpeed,                   // 10: highest_speed
            input.AtkRange,                 // 11: atk_range

            // Derived features (8)
            SafeDivide(totalGold, levelPlusOne),           // 12: gold_per_level
            SafeDivide(highestAtk, levelPlusOne),          // 13: atk_per_level
            SafeDivide(highestDef, levelPlusOne),          // 14: def_per_level
            SafeDivide(highestSpeed, levelPlusOne),        // 15: speed_per_level
            SafeDivide(heroKills, deathsPlusOne),          // 16: kd_ratio
            heroKills + unitKills,                          // 17: total_kills
            SafeDivide(totalGold, heroKillsPlusOne),       // 18: gold_efficiency
            SafeDivide(deaths, playerCountPlusOne)         // 19: death_ratio
        ];
    }

    /// <summary>
    /// Safe division that returns 0 when denominator is 0 or negative.
    /// </summary>
    private static float SafeDivide(float numerator, float denominator)
        => denominator > 0 ? numerator / denominator : 0f;
}
