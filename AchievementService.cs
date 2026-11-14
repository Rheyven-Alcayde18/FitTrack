using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FitTrack;

public class AchievementService
{
    private readonly string _savePath;
    private readonly List<(Achievement Info, Func<Context, bool> Rule)> _definitions;

    public IReadOnlyList<Achievement> All => _definitions.Select(d => d.Info).ToList();

    public int UnlockedCount => _definitions.Count(d => d.Info.IsUnlocked);

    public AchievementService(string? savePath = null)
    {
        _savePath = savePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FitTrack", "achievements.json");

        _definitions = BuildDefinitions();
        Load();
    }

    // ---------- Rules ----------

    private sealed record Context(IReadOnlyList<WorkoutRecord> Workouts, int DailyGoalMinutes);

    private static List<(Achievement, Func<Context, bool>)> BuildDefinitions() => new()
    {
        (new Achievement { Id = "first_workout", Title = "First Step", Icon = "🏁",
            Description = "Log your first workout." },
            c => c.Workouts.Count >= 1),

        (new Achievement { Id = "five_week", Title = "Consistent", Icon = "📅",
            Description = "Complete 5 sessions in the last 7 days." },
            c => c.Workouts.Count(w => w.Date.Date > DateTime.Today.AddDays(-7)) >= 5),

        (new Achievement { Id = "goal_hit", Title = "Goal Crusher", Icon = "🎯",
            Description = "Reach your daily movement goal." },
            c => c.Workouts.GroupBy(w => w.Date.Date)
                           .Any(g => g.Sum(w => w.Minutes) >= c.DailyGoalMinutes)),

        (new Achievement { Id = "kcal_1000", Title = "Furnace", Icon = "🔥",
            Description = "Burn 1,000 kcal in total." },
            c => c.Workouts.Sum(w => w.Calories) >= 1000),

        (new Achievement { Id = "minutes_500", Title = "Endurance", Icon = "⏱",
            Description = "Accumulate 500 active minutes." },
            c => c.Workouts.Sum(w => w.Minutes) >= 500),

        (new Achievement { Id = "streak_3", Title = "On a Roll", Icon = "⚡",
            Description = "Work out 3 days in a row." },
            c => LongestStreak(c.Workouts) >= 3),

        (new Achievement { Id = "streak_7", Title = "Unstoppable", Icon = "🏆",
            Description = "Work out 7 days in a row." },
            c => LongestStreak(c.Workouts) >= 7),
    };

    private static int LongestStreak(IReadOnlyList<WorkoutRecord> workouts)
    {
        var days = workouts.Select(w => w.Date.Date).Distinct().OrderBy(d => d).ToList();
        int best = 0, run = 0;
        DateTime? prev = null;
        foreach (var d in days)
        {
            run = prev.HasValue && (d - prev.Value).Days == 1 ? run + 1 : 1;
            best = Math.Max(best, run);
            prev = d;
        }
        return best;
    }

    // ---------- Public API ----------

    /// <summary>
    /// Evaluates all rules and returns the achievements that were newly unlocked.
    /// Call this after saving a workout or changing the daily goal.
    /// </summary>
    public List<Achievement> CheckAchievements(IReadOnlyList<WorkoutRecord> workouts, int dailyGoalMinutes)
    {
        var ctx = new Context(workouts, dailyGoalMinutes);
        var newlyUnlocked = new List<Achievement>();

        foreach (var (info, rule) in _definitions)
        {
            if (info.IsUnlocked) continue;
            if (!rule(ctx)) continue;

            info.IsUnlocked = true;
            info.UnlockedOn = DateTime.Now;
            newlyUnlocked.Add(info);
        }

        if (newlyUnlocked.Count > 0) Save();
        return newlyUnlocked;
    }

    // ---------- Persistence ----------

    private void Load()
    {
        try
        {
            if (!File.Exists(_savePath)) return;
            var saved = JsonSerializer.Deserialize<Dictionary<string, DateTime>>(File.ReadAllText(_savePath));
            if (saved is null) return;

            foreach (var (info, _) in _definitions)
            {
                if (saved.TryGetValue(info.Id, out var when))
                {
                    info.IsUnlocked = true;
                    info.UnlockedOn = when;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // Corrupt or locked file: start fresh rather than crash the app.
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_savePath)!);
            var data = _definitions
                .Where(d => d.Info.IsUnlocked && d.Info.UnlockedOn.HasValue)
                .ToDictionary(d => d.Info.Id, d => d.Info.UnlockedOn!.Value);
            File.WriteAllText(_savePath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (IOException) { }
    }
}