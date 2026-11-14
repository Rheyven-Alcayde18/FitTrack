using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace FitTrack;

/// <summary>
/// Compact card showing achievement badges. Use maxItems for the Home card,
/// or int.MaxValue for the full list on the Goals tab.
/// </summary>
public class AchievementsPanel : UserControl
{
    private readonly Label _title = new();
    private readonly Label _counter = new();
    private readonly FlowLayoutPanel _flow = new();

    public AchievementsPanel()
    {
        DoubleBuffered = true;
        BackColor = Color.FromArgb(235, 240, 255);   // tweak to match your card color
        Padding = new Padding(12);
        Height = 130;

        _title.Text = "Achievements";
        _title.Font = new Font("Segoe UI Semibold", 11f);
        _title.AutoSize = true;
        _title.Location = new Point(12, 10);

        _counter.AutoSize = true;
        _counter.ForeColor = Color.DimGray;
        _counter.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        _flow.Location = new Point(8, 40);
        _flow.Size = new Size(Width - 16, Height - 48);
        _flow.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        _flow.WrapContents = true;
        _flow.AutoScroll = true;

        Controls.AddRange(new Control[] { _title, _counter, _flow });
        Resize += (_, _) => _counter.Location = new Point(Width - _counter.Width - 14, 12);
    }

    public void SetAchievements(IReadOnlyList<Achievement> all, int maxItems = 4)
    {
        _flow.SuspendLayout();
        _flow.Controls.Clear();

        // Unlocked first, then locked
        foreach (var a in all.OrderByDescending(a => a.IsUnlocked).Take(maxItems))
            _flow.Controls.Add(CreateBadge(a));

        _flow.ResumeLayout();

        _counter.Text = $"{all.Count(a => a.IsUnlocked)} / {all.Count}";
        _counter.Location = new Point(Width - _counter.Width - 14, 12);
    }

    private static Control CreateBadge(Achievement a)
    {
        var badge = new Label
        {
            Width = 76,
            Height = 60,
            Margin = new Padding(4),
            TextAlign = ContentAlignment.MiddleCenter,
            Text = $"{a.Icon}\n{a.Title}",
            Font = new Font("Segoe UI", 8.5f),
            BackColor = a.IsUnlocked ? Color.FromArgb(200, 245, 235) : Color.FromArgb(225, 225, 230),
            ForeColor = a.IsUnlocked ? Color.FromArgb(20, 90, 70) : Color.Gray,
        };

        var tip = new ToolTip();
        tip.SetToolTip(badge, a.IsUnlocked
            ? $"{a.Description}\nUnlocked {a.UnlockedOn:d MMM yyyy}"
            : $"{a.Description}\nLocked");

        return badge;
    }
}