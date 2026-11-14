
using System;
namespace FitTrack;

public class Achievement
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string Icon { get; init; } = "★";
    public bool IsUnlocked { get; set; }
    public DateTime? UnlockedOn { get; set; }
}

/// <summary>
/// Minimal workout data the achievement rules need.
/// Map your own workout class to this (see README notes in AchievementService).
/// </summary>
public record WorkoutRecord(DateTime Date, int Minutes, int Calories);