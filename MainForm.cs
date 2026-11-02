using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FitTrack
{
    public partial class MainForm : Form
    {
        // Palette: deep ink, soft aqua canvas, mint/teal accents. Glass cards over a soft gradient backdrop.
        private Color aquaBg = ColorTranslator.FromHtml("#E9FFFF");
        private Color mint = ColorTranslator.FromHtml("#4DFED1");
        private Color teal = ColorTranslator.FromHtml("#0BDBB6");
        private Color dark = ColorTranslator.FromHtml("#173B3A");
        private Color muted = ColorTranslator.FromHtml("#4A6664");
        private Color softField = ColorTranslator.FromHtml("#EFFAF8");

        // Theme colors (swapped by ApplyTheme when the dark mode button is pressed).
        private bool darkMode = false;
        private Color cardColor = Color.White;
        private Color rowColor = ColorTranslator.FromHtml("#F0FFFC");
        private Color badgeColor = ColorTranslator.FromHtml("#D9FFF5");
        private Color dotOffColor = ColorTranslator.FromHtml("#EAF6F3");
        private Color accent = ColorTranslator.FromHtml("#078F78");
        private Color btnHover2 = ColorTranslator.FromHtml("#D8F7F1");
        private Color btnText2 = ColorTranslator.FromHtml("#416B67");
        private Color trackColor = Color.FromArgb(226, 244, 240);
        private Color ink = ColorTranslator.FromHtml("#173B3A");   // text on teal; never changes
        private Label themeButton;

        // The content area is a fixed 400px wide and every page is designed to fit without page scrolling.
        private const int CW = 400;

        // Global text size multiplier for every label (1.0 = original sizes).
        private const float FontScale = 1.12f;

        private List<Workout> workouts = new List<Workout>();
        private Stack<Workout> undoStack = new Stack<Workout>();
        private string historySortStatus = "Pick a field, then choose a sorting algorithm.";
        private string historyCriterion = "Duration (minutes)";

        private string profileName = "Fitness User";
        private string fitnessLevel = "Beginner";
        private int dailyGoal = 45;
        private bool showSavedNotice;

        // QUEUE (real-life use: your "Up next" playlist of planned workouts, first in = first done)
        private LinkedQueue<Workout> planQueue = new LinkedQueue<Workout>();
        private string queueStatus = "Add workouts to the back of the line. You always do the front one first.";

        private NavBar navBar;
        private string[] navKeys = new string[] { "Dashboard", "My Workouts", "Workout History", "Fitness Goals", "Progress", "Settings", "Workout Queue" };

        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        // Navigation animation
        private System.Windows.Forms.Timer pageTimer;
        private Stopwatch clock = new Stopwatch();
        private PictureBox oldPageSnapshot;
        private PictureBox newPageSnapshot;
        private int slideDirection = 1;          // 1 = new page enters from the right, -1 = from the left
        private const int AnimationMs = 260;
        private bool isAnimating = false;
        private Action[] pages;

        private void DragWindow(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, 0xA1, 0x2, 0);
            }
        }

        private Label MakeWindowButton(string text, int x, Action action, Func<Color> hover)
        {
            Label button = MakeLabel(text, x, 14, 30, 28, 10, true, muted);
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.Cursor = Cursors.Hand;
            button.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            button.Click += delegate { action(); };
            button.MouseEnter += delegate { button.ForeColor = hover(); };
            button.MouseLeave += delegate { button.ForeColor = muted; };
            return button;
        }

        // Wraps an input control in a soft, borderless rounded field (solid, so the text box matches it).
        private Control MakeInput(Control input, int x, int y, int width)
        {
            ModernCard holder = new ModernCard();
            holder.Radius = 12;
            holder.BackColor = softField;
            holder.TintAlpha = 255;
            holder.Location = new Point(x, y);
            holder.Size = new Size(width, 38);
            input.Font = new Font("Segoe UI", 11F);
            input.BackColor = holder.BackColor;
            input.ForeColor = dark;
            TextBox textBox = input as TextBox;
            if (textBox != null) textBox.BorderStyle = BorderStyle.None;
            NumericUpDown numeric = input as NumericUpDown;
            if (numeric != null) numeric.BorderStyle = BorderStyle.None;
            ComboBox combo = input as ComboBox;
            if (combo != null) combo.FlatStyle = FlatStyle.Flat;
            input.Width = width - 24;
            input.Location = new Point(12, Math.Max(2, (holder.Height - input.Height) / 2));
            holder.Controls.Add(input);
            return holder;
        }

        // A NumericUpDown only commits a typed number when it loses focus, so read the text directly.
        private int ReadNumber(NumericUpDown box)
        {
            decimal typed;
            if (!decimal.TryParse(box.Text, out typed)) typed = box.Value;
            typed = Math.Max(box.Minimum, Math.Min(box.Maximum, typed));
            return (int)typed;
        }

        private int EstimateCalories(string category, int minutes)
        {
            int perMinute = 5;
            if (category == "Strength") perMinute = 6;
            else if (category == "Cardio") perMinute = 9;
            else if (category == "Flexibility") perMinute = 3;
            return minutes * perMinute;
        }

        public MainForm()
        {
            workouts.Add(new Workout("Full Body Strength", "Strength", 45, 320, DateTime.Today.AddDays(-1)));
            workouts.Add(new Workout("Morning Cardio", "Cardio", 30, 210, DateTime.Today.AddDays(-2)));
            workouts.Add(new Workout("Upper Body", "Strength", 40, 275, DateTime.Today.AddDays(-4)));
            workouts.Add(new Workout("Evening Walk", "Cardio", 25, 130, DateTime.Today.AddDays(-5)));

            // A few planned workouts so the queue page is not empty on first open.
            planQueue.Enqueue(new Workout("Leg Day", "Strength", 50, 300, DateTime.Today));
            planQueue.Enqueue(new Workout("Yoga Flow", "Flexibility", 30, 90, DateTime.Today));
            planQueue.Enqueue(new Workout("Evening Run", "Cardio", 35, 315, DateTime.Today));

            InitializeComponent();

            pageTimer = new System.Windows.Forms.Timer();
            pageTimer.Interval = 15;
            pageTimer.Tick += PageTimer_Tick;

            BuildNavigation();
            this.Controls.Add(navBar);
            navBar.Dock = DockStyle.Bottom;
            navBar.Height = 70;
            navBar.BringToFront();

            ApplyMobileLayout();
            ShowDashboard();

            Panel look = MakeCard(0, 516, CW, 64);
            look.Controls.Add(MakeLabel("Appearance", 18, 20, 200, 24, 11, true, dark));
            look.Controls.Add(MakeButton(darkMode ? "Light mode" : "Dark mode", CW - 134, 15, 116, 34, ToggleTheme, true));
        }

        // Dark mode button: flip the flag, recolor everything, rebuild the current page.
        private void ToggleTheme()
        {
            darkMode = !darkMode;
            ApplyTheme();
            if (isAnimating) FinishAnimation();
            int index = Math.Max(0, navBar.Selected);
            pages[index]();                 // rebuild the visible page with the new colors
            navBar.Invalidate();
            Invalidate(true);
        }

        private void ApplyTheme()
        {
            Backdrop.Dark = darkMode;       // also clears the cached backdrop images

            if (darkMode)
            {
                aquaBg = ColorTranslator.FromHtml("#0F2A2E");
                dark = ColorTranslator.FromHtml("#EAFBF8");
                muted = ColorTranslator.FromHtml("#9BB9B6");
                softField = ColorTranslator.FromHtml("#1F403F");
                cardColor = ColorTranslator.FromHtml("#1B3B3A");
                rowColor = ColorTranslator.FromHtml("#2A5250");
                badgeColor = ColorTranslator.FromHtml("#1F5A52");
                dotOffColor = ColorTranslator.FromHtml("#2E5654");
                accent = mint;
                btnHover2 = ColorTranslator.FromHtml("#2B5553");
                btnText2 = ColorTranslator.FromHtml("#CDEEE8");
                trackColor = ColorTranslator.FromHtml("#2E5654");

                navBar.Accent = mint;
                navBar.PillColor = ColorTranslator.FromHtml("#23514C");
                navBar.HoverColor = ColorTranslator.FromHtml("#1C3B39");
                navBar.MutedColor = ColorTranslator.FromHtml("#9BB9B6");
                navBar.GlassColor = Color.FromArgb(130, 15, 35, 38);
                navBar.EdgeColor = Color.FromArgb(50, 255, 255, 255);
            }
            else
            {
                aquaBg = ColorTranslator.FromHtml("#E9FFFF");
                dark = ColorTranslator.FromHtml("#173B3A");
                muted = ColorTranslator.FromHtml("#4A6664");
                softField = ColorTranslator.FromHtml("#EFFAF8");
                cardColor = Color.White;
                rowColor = ColorTranslator.FromHtml("#F0FFFC");
                badgeColor = ColorTranslator.FromHtml("#D9FFF5");
                dotOffColor = ColorTranslator.FromHtml("#EAF6F3");
                accent = ColorTranslator.FromHtml("#078F78");
                btnHover2 = ColorTranslator.FromHtml("#D8F7F1");
                btnText2 = ColorTranslator.FromHtml("#416B67");
                trackColor = Color.FromArgb(226, 244, 240);

                navBar.Accent = Color.FromArgb(7, 143, 120);
                navBar.PillColor = Color.FromArgb(217, 255, 245);
                navBar.HoverColor = Color.FromArgb(240, 255, 252);
                navBar.MutedColor = Color.FromArgb(74, 102, 100);
                navBar.GlassColor = Color.FromArgb(120, 255, 255, 255);
                navBar.EdgeColor = Color.FromArgb(160, 255, 255, 255);
            }

            this.BackColor = aquaBg;
            mainContent.BackColor = aquaBg;
            logoLabel.ForeColor = dark;
            titleLabel.ForeColor = dark;
            subtitleLabel.ForeColor = muted;
            foreach (Control c in sidebar.Controls)
            {
                Label l = c as Label;
                if (l != null && l != logoLabel) l.ForeColor = muted;   // window buttons + theme button
            }
            themeButton.Text = darkMode ? "☀" : "☾";
        }

        private void ApplyMobileLayout()
        {
            // Phone-sized, borderless app window with a fixed bottom tab bar.
            this.FormBorderStyle = FormBorderStyle.None;
            this.MaximizeBox = false;
            this.MinimumSize = new Size(390, 700);
            this.MaximumSize = new Size(470, 1000);
            this.ClientSize = new Size(430, 850);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = aquaBg;
            this.DoubleBuffered = true;
            this.Region = new Region(Gfx.RoundRect(new Rectangle(0, 0, this.Width, this.Height), 28));

            // Slim top bar: brand + window controls, same canvas as the page.
            sidebar.Dock = DockStyle.Top;
            sidebar.Height = 56;
            sidebar.Padding = new Padding(20, 4, 18, 0);
            menuLabel.Visible = false;
            tipPanel.Visible = false;
            navigation.Visible = false;

            logoLabel.Dock = DockStyle.Top;
            logoLabel.Height = 48;
            logoLabel.Font = new Font("Segoe UI", 18F, FontStyle.Bold);
            logoLabel.ForeColor = dark;
            logoLabel.Text = "FIT TRACK";
            logoLabel.TextAlign = ContentAlignment.MiddleLeft;

            mainPanel.Padding = new Padding(15, 4, 15, 0);
            headerPanel.Height = 62;
            titleLabel.Font = new Font("Segoe UI", 21F, FontStyle.Bold);
            titleLabel.ForeColor = dark;
            titleLabel.Location = new Point(0, 0);
            subtitleLabel.Location = new Point(2, 38);
            subtitleLabel.Font = new Font("Segoe UI", 9.5F);
            subtitleLabel.ForeColor = muted;
            subtitleLabel.MaximumSize = new Size(380, 30);
            profileLabel.Visible = false;
            mainContent.BackColor = aquaBg;
            mainContent.AutoScroll = false;
            typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(mainContent, true, null);

            // Borderless window: custom minimize/close and drag-to-move.
            sidebar.Controls.Add(MakeWindowButton("✕", 372, delegate { Close(); }, delegate { return Color.FromArgb(220, 70, 70); }));
            sidebar.Controls.Add(MakeWindowButton("—", 340, delegate { WindowState = FormWindowState.Minimized; }, delegate { return dark; }));
            themeButton = MakeWindowButton("☾", 308, delegate { ToggleTheme(); }, delegate { return dark; });
            themeButton.Font = new Font("Segoe UI Symbol", 13F);
            sidebar.Controls.Add(themeButton);
            foreach (Control c in sidebar.Controls) if (c is Label) c.BringToFront();
            sidebar.MouseDown += DragWindow;
            logoLabel.MouseDown += DragWindow;

            // Glass backdrop: the form paints it, and transparent containers let it show through.
            this.Paint += delegate(object s, PaintEventArgs e)
            {
                e.Graphics.DrawImage(Backdrop.Get(ClientSize), 0, 0);
            };
            this.ResizeRedraw = true;

            sidebar.BackColor = Color.Transparent;
            mainPanel.BackColor = Color.Transparent;
            headerPanel.BackColor = Color.Transparent;

            // mainContent stays opaque (so the slide snapshots work) and paints its own slice of the backdrop.
            mainContent.Paint += delegate(object s, PaintEventArgs e) { Backdrop.PaintBehind(mainContent, e.Graphics); };
        }

        // Fixed bottom navigation: all seven tabs are always visible, no scrolling, no hamburger menu.
        private void BuildNavigation()
        {
            navBar = new NavBar();
            navBar.Dock = DockStyle.Bottom;
            navBar.Height = 70;
            navBar.Names = new string[] { "Home", "Train", "History", "Goals", "Stats", "Settings", "Queue" };
            navBar.Icons = new string[] { "⌂", "♥", "↺", "◎", "◔", "⚙", "≡" };
            pages = new Action[] { ShowDashboard, ShowWorkouts, ShowHistory, ShowGoals, ShowProgress, ShowSettings, ShowQueue };
            navBar.ItemClicked += delegate(int index) { GoToPage(index); };
        }

        private void SetActiveNavigation(string selected)
        {
            int index = Array.IndexOf(navKeys, selected);
            if (index >= 0 && navBar != null)
                navBar.Selected = index;
        }

        private void PreparePage(string title, string subtitle)
        {
            if (isAnimating) FinishAnimation();   // never clear controls while snapshots are on screen
            titleLabel.Text = title;
            subtitleLabel.Text = subtitle;
            mainContent.Controls.Clear();
        }

        // Single entry point for tab changes (nav bar and in-page shortcut buttons).
        private void GoToPage(int index)
        {
            if (pages == null || index < 0 || index >= pages.Length) return;

            FinishAnimation();                    // a new click completes any slide in progress
            int previous = navBar.Selected;
            if (index == previous) return;

            navBar.Selected = index;
            SlideTo(pages[index], index > previous ? 1 : -1);
        }

        private Bitmap TakeSnapshot()
        {
            Bitmap bmp = new Bitmap(mainContent.Width, mainContent.Height);
            mainContent.DrawToBitmap(bmp, mainContent.ClientRectangle);
            return bmp;
        }

        private PictureBox MakeSnapshotBox(Bitmap bmp)
        {
            PictureBox box = new PictureBox();
            box.Image = bmp;
            box.SizeMode = PictureBoxSizeMode.StretchImage;
            box.BackColor = aquaBg;
            box.Size = mainContent.ClientSize;
            box.Location = new Point(0, 0);
            return box;
        }

        private void SlideTo(Action showPage, int direction)
        {
            if (mainContent.Width <= 0 || mainContent.Height <= 0 || !mainContent.IsHandleCreated)
            {
                showPage();
                return;
            }

            Bitmap oldBmp = TakeSnapshot();       // current page
            showPage();                           // build the new page (live, underneath)
            mainContent.PerformLayout();
            Bitmap newBmp = TakeSnapshot();       // new page

            slideDirection = direction;
            oldPageSnapshot = MakeSnapshotBox(oldBmp);
            newPageSnapshot = MakeSnapshotBox(newBmp);
            newPageSnapshot.Left = direction * mainContent.Width;

            mainContent.Controls.Add(newPageSnapshot);
            mainContent.Controls.Add(oldPageSnapshot);
            newPageSnapshot.BringToFront();
            oldPageSnapshot.BringToFront();

            isAnimating = true;
            clock.Restart();
            pageTimer.Start();
        }

        private void PageTimer_Tick(object sender, EventArgs e)
        {
            if (!isAnimating) { pageTimer.Stop(); return; }

            float t = Math.Min(1f, clock.ElapsedMilliseconds / (float)AnimationMs);
            float eased = 1f - (float)Math.Pow(1f - t, 3);   // ease-out
            int w = mainContent.Width;
            int offset = (int)(w * eased);

            oldPageSnapshot.Left = -slideDirection * offset;
            newPageSnapshot.Left = slideDirection * (w - offset);

            if (t >= 1f) FinishAnimation();
        }

        private void FinishAnimation()
        {
            pageTimer.Stop();
            if (!isAnimating) return;
            isAnimating = false;

            DisposeSnapshot(ref oldPageSnapshot);
            DisposeSnapshot(ref newPageSnapshot);
            mainContent.Invalidate(true);
        }

        private void DisposeSnapshot(ref PictureBox box)
        {
            if (box == null) return;
            mainContent.Controls.Remove(box);
            Image img = box.Image;
            box.Image = null;
            box.Dispose();
            if (img != null) img.Dispose();
            box = null;
        }

        private Panel MakeCard(int x, int y, int width, int height)
        {
            Panel panel = new ModernCard();
            panel.Location = new Point(x, y);
            panel.Size = new Size(width, height);
            panel.BackColor = cardColor;
            panel.Padding = new Padding(0);
            mainContent.Controls.Add(panel);
            return panel;
        }

        private Label MakeLabel(string text, int x, int y, int width, int height, int size, bool bold, Color color)
        {
            Label label = new Label();
            label.Text = text;
            label.Location = new Point(x, y);
            label.Size = new Size(width, height);
            label.Font = new Font("Segoe UI", size * FontScale, bold ? FontStyle.Bold : FontStyle.Regular);
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            return label;
        }

        private Label MakeRightLabel(string text, int x, int y, int width, int height, int size, bool bold, Color color)
        {
            Label label = MakeLabel(text, x, y, width, height, size, bold, color);
            label.TextAlign = ContentAlignment.MiddleRight;
            return label;
        }

        private Control MakeButton(string text, int x, int y, int width, int height, Action action, bool primary)
        {
            RoundButton button = new RoundButton();
            button.Text = text;
            button.Location = new Point(x, y);
            button.Size = new Size(width, height);
            button.Fill = primary ? teal : softField;
            button.HoverFill = primary ? ColorTranslator.FromHtml("#20E7C0") : btnHover2;
            button.ForeColor = primary ? ink : btnText2;
            button.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            button.Click += delegate { action(); };
            return button;
        }

        private ScrollPanel MakeScroller(Panel card, int top)
        {
            ScrollPanel scroller = new ScrollPanel();
            scroller.Location = new Point(4, top);
            scroller.Size = new Size(card.Width - 8, card.Height - top - 14);
            scroller.BackColor = Color.White;
            card.Controls.Add(scroller);
            return scroller;
        }

        private string FriendlyDate(DateTime date)
        {
            if (date.Date == DateTime.Today) return "Today";
            if (date.Date == DateTime.Today.AddDays(-1)) return "Yesterday";
            return date.ToString("MMM d");
        }

        private void AddStat(string heading, string value, string foot, int x, int y, int width)
        {
            Panel card = MakeCard(x, y, width, 84);
            card.Controls.Add(MakeLabel(heading, 13, 12, width - 20, 16, 7, true, muted));
            card.Controls.Add(MakeLabel(value, 12, 30, width - 20, 32, 16, true, dark));
            card.Controls.Add(MakeLabel(foot, 13, 62, width - 20, 16, 7, false, muted));
        }

        private void ShowDashboard()
        {
            string first = profileName.Trim().Split(' ')[0];
            PreparePage("Today", "Hi " + first + ", a little progress adds up.");
            SetActiveNavigation("Dashboard");
            DateTime weekStart = DateTime.Today.AddDays(-6);
            List<Workout> thisWeek = workouts.Where(x => x.Date.Date >= weekStart && x.Date.Date <= DateTime.Today).ToList();
            int sessions = thisWeek.Count;
            int minutes = thisWeek.Sum(x => x.Duration);
            int calories = thisWeek.Sum(x => x.Calories);
            int average = sessions == 0 ? 0 : minutes / sessions;
            int todayMinutes = workouts.Where(x => x.Date.Date == DateTime.Today).Sum(x => x.Duration);
            int goalPercent = Math.Min(100, todayMinutes * 100 / Math.Max(1, dailyGoal));

            // Hero: today's goal at a glance (dark glass, mostly opaque so white text stays readable).
            Panel hero = MakeCard(0, 0, CW, 120);
            hero.BackColor = ColorTranslator.FromHtml("#123D3A");
            ((ModernCard)hero).TintAlpha = 190;
            hero.Controls.Add(MakeLabel("DAILY MOVEMENT GOAL", 20, 16, 240, 18, 7, true, mint));
            hero.Controls.Add(MakeLabel(todayMinutes + " / " + dailyGoal + " min", 20, 36, 280, 36, 20, true, Color.White));
            hero.Controls.Add(MakeLabel(goalPercent + "% completed today", 20, 74, 240, 18, 8, false, ColorTranslator.FromHtml("#D8FFF6")));
            SoftProgress progress = new SoftProgress();
            progress.Track = ColorTranslator.FromHtml("#245A55");
            progress.Fill = mint;
            progress.Location = new Point(20, 98);
            progress.Size = new Size(CW - 40, 10);
            progress.Value = goalPercent;
            hero.Controls.Add(progress);
            hero.Controls.Add(MakeButton("Goals", CW - 92, 22, 72, 30, delegate { GoToPage(3); }, true));

            // Four compact stats in a single row.
            int sw = (CW - 24) / 4;
            AddStat("SESSIONS", sessions.ToString(), "this week", 0, 130, sw);
            AddStat("MINUTES", minutes.ToString(), "active", sw + 8, 130, sw);
            AddStat("CALORIES", calories.ToString("N0"), "kcal", 2 * (sw + 8), 130, sw);
            AddStat("AVERAGE", average.ToString(), "min each", 3 * (sw + 8), 130, sw);

            Panel chartCard = MakeCard(0, 224, CW, 185);
            chartCard.Controls.Add(MakeLabel("Activity this week", 18, 12, 240, 24, 11, true, dark));
            chartCard.Controls.Add(MakeLabel("Workout minutes by day", 18, 34, 240, 16, 7, false, muted));
            MiniBarChart chart = new MiniBarChart();
            chart.Location = new Point(12, 56);
            chart.Size = new Size(chartCard.Width - 24, chartCard.Height - 62);
            chart.Values = Enumerable.Range(0, 7).Select(o => workouts.Where(x => x.Date.Date == weekStart.AddDays(o).Date).Sum(x => x.Duration)).ToArray();
            chart.Labels = Enumerable.Range(0, 7).Select(o => weekStart.AddDays(o).ToString("ddd").Substring(0, 1)).ToArray();
            chart.Accent = teal;
            chart.LabelColor = muted;
            chartCard.Controls.Add(chart);

            int recentCount = Math.Min(3, workouts.Count);
            Panel recent = MakeCard(0, 419, CW, recentCount == 0 ? 110 : 44 + recentCount * 54 + 6);
            recent.Controls.Add(MakeLabel("Recent workouts", 18, 12, 220, 24, 11, true, dark));
            recent.Controls.Add(MakeButton("See all", CW - 84, 10, 66, 28, delegate { GoToPage(2); }, false));
            AddWorkoutRows(recent, 44, 3);
        }

        private void ShowWorkouts()
        {
            PreparePage("My Workouts", "Build your routine and log every session.");
            SetActiveNavigation("My Workouts");

            Panel top = MakeCard(0, 0, CW, 74);
            top.Controls.Add(MakeLabel("Exercise library", 18, 14, CW - 160, 24, 12, true, dark));
            top.Controls.Add(MakeLabel("Log a new workout to your fitness diary.", 18, 40, CW - 160, 20, 8, false, muted));
            top.Controls.Add(MakeButton("+ Add workout", CW - 134, 21, 116, 34, AddWorkoutDialog, true));

            Panel list = MakeCard(0, 84, CW, 556);
            list.Controls.Add(MakeLabel("Workout sessions (" + workouts.Count + ")", 18, 14, 300, 24, 12, true, dark));
            ScrollPanel scroller = MakeScroller(list, 48);
            AddWorkoutRows(scroller, 4, workouts.Count);
        }

        private void AddWorkoutDialog()
        {
            Form dialog = new Form();
            dialog.Text = "Add workout";
            dialog.Size = new Size(390, 450);
            dialog.StartPosition = FormStartPosition.CenterParent;
            dialog.FormBorderStyle = FormBorderStyle.None;
            dialog.ShowInTaskbar = false;
            dialog.BackColor = aquaBg;
            typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(dialog, true, null);
            dialog.Region = new Region(Gfx.RoundRect(new Rectangle(0, 0, dialog.Width, dialog.Height), 28));
            dialog.MouseDown += DragWindow;
            dialog.Paint += delegate(object s, PaintEventArgs e)
            {
                e.Graphics.DrawImage(Backdrop.Get(dialog.ClientSize), 0, 0);
            };

            TextBox name = new TextBox();
            ComboBox category = new ComboBox();
            category.DropDownStyle = ComboBoxStyle.DropDownList;
            category.Items.AddRange(new object[] { "Strength", "Cardio", "Flexibility", "Other" });
            category.SelectedIndex = 0;

            NumericUpDown duration = new NumericUpDown();
            duration.Minimum = 1;
            duration.Maximum = 600;
            duration.Value = 30;

            NumericUpDown calories = new NumericUpDown();
            calories.Minimum = 1;
            calories.Maximum = 5000;
            calories.Value = 200;

            dialog.Controls.Add(MakeLabel("New workout", 25, 20, 300, 30, 14, true, dark));
            dialog.Controls.Add(MakeLabel("Workout name", 25, 62, 200, 22, 9, true, muted));
            dialog.Controls.Add(MakeInput(name, 25, 86, 340));
            dialog.Controls.Add(MakeLabel("Category", 25, 132, 200, 22, 9, true, muted));
            dialog.Controls.Add(MakeInput(category, 25, 156, 340));
            dialog.Controls.Add(MakeLabel("Duration (minutes)", 25, 202, 200, 22, 9, true, muted));
            dialog.Controls.Add(MakeInput(duration, 25, 226, 340));
            dialog.Controls.Add(MakeLabel("Calories burned (estimated)", 25, 272, 260, 22, 9, true, muted));
            dialog.Controls.Add(MakeInput(calories, 25, 296, 340));
            dialog.Controls.Add(MakeButton("Cancel", 25, 372, 100, 42, delegate
            {
                dialog.DialogResult = DialogResult.Cancel;
                dialog.Close();
            }, false));

            Control save = MakeButton("Save workout", 135, 372, 230, 42, delegate
            {
                if (name.Text.Trim().Length == 0)
                {
                    MessageBox.Show("Please enter a workout name.");
                    return;
                }
                workouts.Insert(0, new Workout(name.Text.Trim(), category.Text, ReadNumber(duration), ReadNumber(calories), DateTime.Now));
                undoStack.Push(workouts[0]);
                dialog.DialogResult = DialogResult.OK;
                dialog.Close();
            }, true);
            dialog.Controls.Add(save);

            if (dialog.ShowDialog(this) == DialogResult.OK)
                ShowWorkouts();
        }

        // Adds compact workout rows. Rows are 48px tall with a 6px gap.
        private void AddWorkoutRows(Panel parent, int startY, int count)
        {
            int y = startY;
            bool scroller = parent is ScrollPanel;
            int rowX = scroller ? 12 : 16;
            int rowW = scroller ? parent.Width - 24 : parent.Width - 32;
            if (scroller && Math.Min(count, workouts.Count) * 54 + startY + 8 > parent.Height)
                rowW -= SystemInformation.VerticalScrollBarWidth;

            foreach (Workout item in workouts.Take(count))
            {
                ModernCard row = new ModernCard();
                row.Radius = 14;
                row.Location = new Point(rowX, y);
                row.Size = new Size(rowW, 48);
                row.BackColor = rowColor;
                row.TintAlpha = 70;

                ModernCard badge = new ModernCard();
                badge.Radius = 10;
                badge.Location = new Point(9, 8);
                badge.Size = new Size(32, 32);
                badge.BackColor = badgeColor;
                badge.TintAlpha = 140;
                string initial = item.Category.Length > 0 ? item.Category.Substring(0, 1).ToUpper() : "•";
                Label initialLabel = MakeLabel(initial, 0, 0, 32, 32, 10, true, accent);
                initialLabel.TextAlign = ContentAlignment.MiddleCenter;
                badge.Controls.Add(initialLabel);
                row.Controls.Add(badge);

                Label nameLabel = MakeLabel(item.Name, 50, 6, rowW - 50 - 74, 20, 9, true, dark);
                nameLabel.AutoEllipsis = true;
                row.Controls.Add(nameLabel);
                row.Controls.Add(MakeLabel(item.Category + "  •  " + item.Calories + " kcal  •  " + FriendlyDate(item.Date), 50, 27, rowW - 60, 16, 7, false, muted));
                row.Controls.Add(MakeRightLabel(item.Duration + " min", rowW - 78, 6, 66, 20, 9, true, dark));
                parent.Controls.Add(row);
                y += 54;
            }
            if (workouts.Count == 0 || count == 0)
                parent.Controls.Add(MakeLabel("No workouts yet. Add your first session!", 20, y + 4, 340, 25, 9, false, muted));
        }

        private void ShowHistory()
        {
            PreparePage("Workout History", "Review sessions and sort your records.");
            SetActiveNavigation("Workout History");

            Panel sort = MakeCard(0, 0, CW, 166);
            sort.Controls.Add(MakeLabel("Sort records", 18, 14, 250, 24, 12, true, dark));

            ComboBox sortBy = new ComboBox();
            sortBy.DropDownStyle = ComboBoxStyle.DropDownList;
            sortBy.Items.AddRange(new object[] { "Duration (minutes)", "Calories burned", "Workout name", "Date" });
            sortBy.SelectedIndex = Math.Max(0, sortBy.Items.IndexOf(historyCriterion));
            sort.Controls.Add(MakeInput(sortBy, 16, 44, CW - 32));

            Label sortStatus = MakeLabel(historySortStatus, 18, 130, CW - 36, 30, 7, false, muted);
            sort.Controls.Add(sortStatus);

            int bw = (CW - 32 - 16) / 3;
            sort.Controls.Add(MakeButton("Bubble sort", 16, 92, bw, 34, delegate
            {
                ApplySort("Bubble Sort", sortBy, sortStatus);
                ShowHistory();
            }, true));
            sort.Controls.Add(MakeButton("Selection", 16 + bw + 8, 92, bw, 34, delegate
            {
                ApplySort("Selection Sort", sortBy, sortStatus);
                ShowHistory();
            }, false));
            sort.Controls.Add(MakeButton("Insertion", 16 + 2 * (bw + 8), 92, bw, 34, delegate
            {
                ApplySort("Insertion Sort", sortBy, sortStatus);
                ShowHistory();
            }, false));

            Panel list = MakeCard(0, 176, CW, 464);
            list.Controls.Add(MakeLabel("Completed sessions (" + workouts.Count + ")", 18, 14, 260, 24, 12, true, dark));
            list.Controls.Add(MakeButton("Undo add", CW - 100, 12, 82, 28, delegate
            {
                if (undoStack.Count == 0)
                {
                    MessageBox.Show("There is no recent addition to undo.");
                    return;
                }

                Workout lastAdded = undoStack.Pop();
                workouts.Remove(lastAdded);
                ShowHistory();
            }, false));
            ScrollPanel scroller = MakeScroller(list, 48);
            AddWorkoutRows(scroller, 4, workouts.Count);
        }

        private void ApplySort(string algorithm, ComboBox sortBy, Label status)
        {
            string criterion = sortBy.SelectedItem == null ? "Duration (minutes)" : sortBy.SelectedItem.ToString();
            List<Workout> sortedWorkouts = new List<Workout>(workouts);

            if (algorithm == "Bubble Sort")
                BubbleSort(sortedWorkouts, criterion);
            else if (algorithm == "Selection Sort")
                SelectionSort(sortedWorkouts, criterion);
            else if (algorithm == "Insertion Sort")
                InsertionSort(sortedWorkouts, criterion);

            workouts = sortedWorkouts;
            historyCriterion = criterion;
            historySortStatus = algorithm + " applied. Sorted by " + criterion + " (ascending).";
            status.Text = historySortStatus;
        }

        // Bubble Sort: repeatedly compares adjacent records and swaps them when needed.
        private void BubbleSort(List<Workout> items, string criterion)
        {
            for (int i = 0; i < items.Count - 1; i++)
            {
                for (int j = 0; j < items.Count - i - 1; j++)
                {
                    if (CompareWorkout(items[j], items[j + 1], criterion) > 0)
                    {
                        Workout temp = items[j];
                        items[j] = items[j + 1];
                        items[j + 1] = temp;
                    }
                }
            }
        }

        // Selection Sort: selects the smallest remaining record for each position.
        private void SelectionSort(List<Workout> items, string criterion)
        {
            for (int i = 0; i < items.Count - 1; i++)
            {
                int minimumIndex = i;
                for (int j = i + 1; j < items.Count; j++)
                {
                    if (CompareWorkout(items[j], items[minimumIndex], criterion) < 0)
                        minimumIndex = j;
                }

                if (minimumIndex != i)
                {
                    Workout temp = items[i];
                    items[i] = items[minimumIndex];
                    items[minimumIndex] = temp;
                }
            }
        }

        // Insertion Sort: inserts each record into its correct position in the sorted portion.
        private void InsertionSort(List<Workout> items, string criterion)
        {
            for (int i = 1; i < items.Count; i++)
            {
                Workout key = items[i];
                int j = i - 1;

                while (j >= 0 && CompareWorkout(items[j], key, criterion) > 0)
                {
                    items[j + 1] = items[j];
                    j--;
                }

                items[j + 1] = key;
            }
        }

        // Returns a negative, zero, or positive value depending on the selected field.
        private int CompareWorkout(Workout first, Workout second, string criterion)
        {
            if (criterion == "Calories burned")
                return first.Calories.CompareTo(second.Calories);
            if (criterion == "Workout name")
                return String.Compare(first.Name, second.Name, StringComparison.OrdinalIgnoreCase);
            if (criterion == "Date")
                return first.Date.CompareTo(second.Date);

            return first.Duration.CompareTo(second.Duration);
        }

        private void ShowGoals()
        {
            PreparePage("Fitness Goals", "Set realistic targets and celebrate progress.");
            SetActiveNavigation("Fitness Goals");
            DateTime weekStart = DateTime.Today.AddDays(-6);
            List<Workout> thisWeek = workouts.Where(x => x.Date.Date >= weekStart && x.Date.Date <= DateTime.Today).ToList();
            int sessions = thisWeek.Count;
            int minutes = thisWeek.Sum(x => x.Duration);
            int burned = thisWeek.Sum(x => x.Calories);
            int activeDays = thisWeek.Select(x => x.Date.Date).Distinct().Count();

            Panel p = MakeCard(0, 0, CW, 352);
            p.Controls.Add(MakeLabel("Your weekly goals", 18, 16, 240, 24, 12, true, dark));
            p.Controls.Add(MakeButton("Edit targets", CW - 114, 14, 96, 28, delegate
            {
                MessageBox.Show("Goal editing can be added in the next development step.", "Fitness goals");
            }, false));
            AddGoal(p, "Workout sessions", sessions + " of 5 sessions", sessions * 100 / 5, 60);
            AddGoal(p, "Active minutes", minutes + " of 180 min", minutes * 100 / 180, 138);
            AddGoal(p, "Calories burned", burned.ToString("N0") + " of 1,200 kcal", burned * 100 / 1200, 216);
            AddGoal(p, "Active days", activeDays + " of 7 days", activeDays * 100 / 7, 294);

            Panel week = MakeCard(0, 362, CW, 112);
            week.Controls.Add(MakeLabel("This week", 18, 14, 160, 24, 12, true, dark));
            week.Controls.Add(MakeRightLabel(activeDays + " of 7 days active", CW - 198, 16, 180, 20, 8, false, muted));
            int slot = (CW - 32) / 7;
            for (int i = 0; i < 7; i++)
            {
                DateTime day = weekStart.AddDays(i);
                bool done = workouts.Any(x => x.Date.Date == day.Date);
                ModernCard dot = new ModernCard();
                dot.Radius = 19;
                dot.Size = new Size(38, 38);
                dot.Location = new Point(16 + i * slot + (slot - 38) / 2, 54);
                dot.BackColor = done ? teal : dotOffColor;
                dot.TintAlpha = done ? 230 : 90;
                Label letter = MakeLabel(day.ToString("ddd").Substring(0, 1), 0, 0, 38, 38, 9, true, done ? ink : muted);
                letter.TextAlign = ContentAlignment.MiddleCenter;
                dot.Controls.Add(letter);
                week.Controls.Add(dot);
            }
        }

        private void AddGoal(Panel panel, string title, string value, int percent, int y)
        {
            panel.Controls.Add(MakeLabel(title, 20, y, 200, 22, 9, true, dark));
            panel.Controls.Add(MakeRightLabel(value, panel.Width - 20 - 190, y, 190, 22, 8, false, muted));
            SoftProgress bar = new SoftProgress();
            bar.Location = new Point(20, y + 30);
            bar.Size = new Size(panel.Width - 40, 10);
            bar.Track = trackColor;
            bar.Value = Math.Min(100, percent);
            panel.Controls.Add(bar);
        }

        private void ShowProgress()
        {
            PreparePage("Progress", "See how your activity adds up.");
            SetActiveNavigation("Progress");
            DateTime weekStart = DateTime.Today.AddDays(-6);
            int totalMinutes = workouts.Sum(x => x.Duration);
            int totalCalories = workouts.Sum(x => x.Calories);

            int sw = (CW - 16) / 3;
            AddStat("WORKOUTS", workouts.Count.ToString(), "all time", 0, 0, sw);
            AddStat("MINUTES", totalMinutes.ToString("N0"), "all time", sw + 8, 0, sw);
            AddStat("CALORIES", totalCalories.ToString("N0"), "kcal total", 2 * (sw + 8), 0, sw);

            Panel p = MakeCard(0, 94, CW, 320);
            p.Controls.Add(MakeLabel("Calories burned", 18, 14, 260, 24, 12, true, dark));
            p.Controls.Add(MakeLabel("Last 7 days", 18, 38, 260, 16, 7, false, muted));
            MiniBarChart chart = new MiniBarChart();
            chart.Location = new Point(12, 62);
            chart.Size = new Size(p.Width - 24, p.Height - 74);
            chart.Values = Enumerable.Range(0, 7).Select(o => workouts.Where(x => x.Date.Date == weekStart.AddDays(o).Date).Sum(x => x.Calories)).ToArray();
            chart.Labels = Enumerable.Range(0, 7).Select(o => weekStart.AddDays(o).ToString("ddd")).ToArray();
            chart.Accent = teal;
            chart.LabelColor = muted;
            p.Controls.Add(chart);

            Workout longest = workouts.OrderByDescending(x => x.Duration).FirstOrDefault();
            Workout topBurn = workouts.OrderByDescending(x => x.Calories).FirstOrDefault();
            Panel highlights = MakeCard(0, 424, CW, 108);
            highlights.Controls.Add(MakeLabel("Highlights", 18, 14, 200, 24, 12, true, dark));
            AddHighlight(highlights, "Longest session", longest == null ? "-" : longest.Name + "  ·  " + longest.Duration + " min", 48);
            AddHighlight(highlights, "Top calorie burn", topBurn == null ? "-" : topBurn.Name + "  ·  " + topBurn.Calories + " kcal", 74);
        }

        private void AddHighlight(Panel panel, string caption, string value, int y)
        {
            panel.Controls.Add(MakeLabel(caption, 18, y, 130, 20, 8, false, muted));
            panel.Controls.Add(MakeRightLabel(value, 140, y, panel.Width - 140 - 18, 20, 8, true, dark));
        }

        private void ShowSettings()
        {
            PreparePage("Settings", "Manage your profile and preferences.");
            SetActiveNavigation("Settings");

            Panel p = MakeCard(0, 0, CW, 410);

            ModernCard avatar = new ModernCard();
            avatar.Radius = 32;
            avatar.Size = new Size(64, 64);
            avatar.Location = new Point(20, 20);
            avatar.BackColor = teal;
            avatar.TintAlpha = 230;
            string initial = profileName.Trim().Length > 0 ? profileName.Trim().Substring(0, 1).ToUpper() : "F";
            Label initialLabel = MakeLabel(initial, 0, 0, 64, 64, 20, true, ink);
            initialLabel.TextAlign = ContentAlignment.MiddleCenter;
            avatar.Controls.Add(initialLabel);
            p.Controls.Add(avatar);
            p.Controls.Add(MakeLabel(profileName, 98, 26, CW - 120, 28, 14, true, dark));
            p.Controls.Add(MakeLabel(fitnessLevel + " level", 99, 56, CW - 120, 20, 8, false, muted));

            p.Controls.Add(MakeLabel("Name", 20, 104, 200, 20, 8, true, muted));
            TextBox name = new TextBox();
            name.Text = profileName;
            p.Controls.Add(MakeInput(name, 20, 126, CW - 40));

            p.Controls.Add(MakeLabel("Fitness level", 20, 174, 200, 20, 8, true, muted));
            ComboBox level = new ComboBox();
            level.DropDownStyle = ComboBoxStyle.DropDownList;
            level.Items.AddRange(new object[] { "Beginner", "Intermediate", "Advanced" });
            level.SelectedIndex = Math.Max(0, level.Items.IndexOf(fitnessLevel));
            p.Controls.Add(MakeInput(level, 20, 196, CW - 40));

            p.Controls.Add(MakeLabel("Daily movement goal (minutes)", 20, 244, 260, 20, 8, true, muted));
            NumericUpDown goalInput = new NumericUpDown();
            goalInput.Minimum = 5;
            goalInput.Maximum = 600;
            goalInput.Value = dailyGoal;
            p.Controls.Add(MakeInput(goalInput, 20, 266, CW - 40));

            p.Controls.Add(MakeButton("Save preferences", 20, 324, CW - 40, 42, delegate
            {
                string newName = name.Text.Trim();
                profileName = newName.Length == 0 ? "Fitness User" : newName;
                fitnessLevel = level.Text;
                dailyGoal = ReadNumber(goalInput);
                showSavedNotice = true;
                ShowSettings();
            }, true));

            if (showSavedNotice)
            {
                Label notice = MakeLabel("✓  Preferences saved", 20, 374, CW - 40, 20, 8, true, accent);
                notice.TextAlign = ContentAlignment.MiddleCenter;
                p.Controls.Add(notice);
                showSavedNotice = false;
            }

            Panel tip = MakeCard(0, 420, CW, 86);
            tip.BackColor = badgeColor;
            ((ModernCard)tip).TintAlpha = 150;
            tip.Controls.Add(MakeLabel("STAY CONSISTENT", 20, 14, 200, 18, 7, true, dark));
            tip.Controls.Add(MakeLabel("Small steps every day lead to big results. Keep moving!", 20, 36, CW - 40, 36, 9, false, muted));
        }

        // ---------------------------------------------------------------
        // WORKOUT QUEUE: real-life use of a queue in a personal app.
        // It works like the "Up next" list of a music player. You line up
        // the workouts you plan to do, and you always do the front one first.
        //   Enqueue  = plan a workout (goes to the back of the line)
        //   Peek     = see what is up next without removing it
        //   Dequeue  = complete the front workout (it is logged in History)
        //   Postpone = dequeue the front one, then enqueue it again at the back
        //   IsEmpty  = nothing planned, so you are free to add something
        // ---------------------------------------------------------------
        private void ShowQueue()
        {
            PreparePage("Workout Queue", "Line up your workouts: first in, first done.");
            SetActiveNavigation("Workout Queue");

            // PLAN card (ENQUEUE)
            Panel plan = MakeCard(0, 0, CW, 154);
            plan.Controls.Add(MakeLabel("Plan a workout", 18, 12, 260, 24, 12, true, dark));

            TextBox name = new TextBox();
            plan.Controls.Add(MakeInput(name, 16, 42, CW - 32));

            ComboBox category = new ComboBox();
            category.DropDownStyle = ComboBoxStyle.DropDownList;
            category.Items.AddRange(new object[] { "Strength", "Cardio", "Flexibility", "Other" });
            category.SelectedIndex = 0;
            plan.Controls.Add(MakeInput(category, 16, 90, 170));

            NumericUpDown mins = new NumericUpDown();
            mins.Minimum = 5;
            mins.Maximum = 240;
            mins.Value = 30;
            plan.Controls.Add(MakeInput(mins, 194, 90, 90));

            plan.Controls.Add(MakeButton("Add", 292, 90, CW - 292 - 16, 38, delegate
            {
                string workoutName = name.Text.Trim();
                if (workoutName.Length == 0)
                {
                    MessageBox.Show("Please enter a workout name.");
                    return;
                }

                int m = ReadNumber(mins);
                planQueue.Enqueue(new Workout(workoutName, category.Text, m, EstimateCalories(category.Text, m), DateTime.Today));
                queueStatus = "'" + workoutName + "' added to the back of the queue. Position " + planQueue.Count + ".";
                ShowQueue();
            }, true));
            plan.Controls.Add(MakeLabel("Workout name   •   category   •   minutes", 18, 132, CW - 36, 16, 7, false, muted));

            // UP NEXT card (PEEK / DEQUEUE / POSTPONE / CLEAR)
            Workout front = planQueue.Peek();
            Panel next = MakeCard(0, 164, CW, 164);
            next.Controls.Add(MakeLabel("UP NEXT", 18, 12, CW - 36, 18, 7, true, muted));
            next.Controls.Add(MakeLabel(front == null ? "Nothing planned" : front.Name, 18, 30, CW - 36, 28, 14, true, dark));
            next.Controls.Add(MakeLabel(front == null
                ? "The queue is empty. Add a workout above."
                : front.Category + "   •   " + front.Duration + " min   •   ~" + front.Calories + " kcal", 18, 60, CW - 36, 18, 8, false, muted));

            int bw = (CW - 32 - 16) / 3;
            next.Controls.Add(MakeButton("Complete", 16, 88, bw, 34, delegate
            {
                Workout done = planQueue.Dequeue();
                if (done == null)
                {
                    queueStatus = "Nothing to complete. The queue is empty.";
                }
                else
                {
                    Workout logged = new Workout(done.Name, done.Category, done.Duration, done.Calories, DateTime.Now);
                    workouts.Insert(0, logged);
                    undoStack.Push(logged);
                    Workout upcoming = planQueue.Peek();
                    queueStatus = "'" + done.Name + "' completed and saved to History. " + (upcoming == null ? "Your queue is now empty." : "Next: " + upcoming.Name + ".");
                }
                ShowQueue();
            }, true));
            next.Controls.Add(MakeButton("Postpone", 16 + bw + 8, 88, bw, 34, delegate
            {
                if (planQueue.Count < 2)
                {
                    queueStatus = planQueue.IsEmpty() ? "Nothing to postpone." : "Only one workout is queued, so there is nothing to swap with.";
                }
                else
                {
                    Workout later = planQueue.Dequeue();
                    planQueue.Enqueue(later);
                    queueStatus = "'" + later.Name + "' moved to the back. Now up next: " + planQueue.Peek().Name + ".";
                }
                ShowQueue();
            }, false));
            next.Controls.Add(MakeButton("Clear", 16 + 2 * (bw + 8), 88, bw, 34, delegate
            {
                planQueue.Clear();
                queueStatus = "Queue cleared.";
                ShowQueue();
            }, false));
            next.Controls.Add(MakeLabel(queueStatus, 18, 130, CW - 36, 28, 7, false, muted));

            // LINED UP card (everything behind the front workout)
            List<Workout> items = planQueue.ToList();
            int waiting = Math.Max(0, items.Count - 1);
            int totalMinutes = items.Sum(w => w.Duration);
            Panel list = MakeCard(0, 338, CW, 302);
            list.Controls.Add(MakeLabel("Lined up after this (" + waiting + ")", 18, 14, 260, 24, 12, true, dark));
            list.Controls.Add(MakeRightLabel("Total " + totalMinutes + " min", CW - 138, 16, 120, 20, 8, false, muted));
            ScrollPanel scroller = MakeScroller(list, 48);

            int rowW = scroller.Width - 24;
            if (waiting * 50 + 12 > scroller.Height) rowW -= SystemInformation.VerticalScrollBarWidth;

            int y = 4;
            int before = 0;                                  // minutes of everything ahead of this workout
            for (int i = 0; i < items.Count; i++)
            {
                int startsAfter = before;
                before += items[i].Duration;
                if (i == 0) continue;                        // the front one is shown in the Up Next card

                ModernCard row = new ModernCard();
                row.Radius = 14;
                row.Location = new Point(12, y);
                row.Size = new Size(rowW, 44);
                row.BackColor = rowColor;
                row.TintAlpha = 70;

                Label pos = MakeLabel(i.ToString(), 12, 0, 24, 44, 10, true, accent);
                pos.TextAlign = ContentAlignment.MiddleLeft;
                row.Controls.Add(pos);

                Label nameLabel = MakeLabel(items[i].Name, 40, 4, rowW - 40 - 128, 18, 9, true, dark);
                nameLabel.AutoEllipsis = true;
                row.Controls.Add(nameLabel);
                row.Controls.Add(MakeLabel(items[i].Category + "  •  " + items[i].Duration + " min", 40, 24, rowW - 40 - 128, 16, 7, false, muted));
                Label whenLabel = MakeRightLabel("starts in " + startsAfter + " min", rowW - 124, 0, 114, 44, 7, true, accent);
                row.Controls.Add(whenLabel);
                whenLabel.BringToFront();      // transparent labels hide anything behind them, so keep this one on top
                scroller.Controls.Add(row);
                y += 50;
            }
            if (waiting == 0)
                scroller.Controls.Add(MakeLabel("Nothing else is lined up. Plan another workout above!", 16, 8, 340, 24, 9, false, muted));
        }
    }

    public class QueueNode<T>
    {
        public T Data;
        public QueueNode<T> Next;
        public QueueNode(T data) { Data = data; }
    }

    // FIFO queue made from a linked list (front = first in line, rear = last in line).
    public class LinkedQueue<T> where T : class
    {
        private QueueNode<T> front;
        private QueueNode<T> rear;
        private int count;

        public int Count { get { return count; } }
        public bool IsEmpty() { return count == 0; }

        // Add to the rear.
        public void Enqueue(T item)
        {
            QueueNode<T> node = new QueueNode<T>(item);
            if (rear == null) { front = node; rear = node; }
            else { rear.Next = node; rear = node; }
            count++;
        }

        // Remove from the front. Returns null when the queue is empty (underflow).
        public T Dequeue()
        {
            if (front == null) return null;
            T item = front.Data;
            front = front.Next;
            if (front == null) rear = null;
            count--;
            return item;
        }

        // Look at the front without removing it.
        public T Peek() { return front == null ? null : front.Data; }

        public void Clear() { front = null; rear = null; count = 0; }

        // Front-to-rear snapshot, used for drawing the list.
        public List<T> ToList()
        {
            List<T> list = new List<T>();
            for (QueueNode<T> n = front; n != null; n = n.Next) list.Add(n.Data);
            return list;
        }
    }

    public static class Gfx
    {
        public static GraphicsPath RoundRect(Rectangle bounds, int radius)
        {
            int d = Math.Max(2, Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height)));
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    // Soft gradient + glowing blobs. Glass panels draw the slice of this image that sits behind them.
    public static class Backdrop
    {
        private static Dictionary<Size, Bitmap> cache = new Dictionary<Size, Bitmap>();
        private static bool dark;

        // Switching theme throws away the cached images so they are redrawn in the new colors.
        public static bool Dark
        {
            get { return dark; }
            set
            {
                if (dark == value) return;
                dark = value;
                foreach (Bitmap b in cache.Values) b.Dispose();
                cache.Clear();
            }
        }

        public static Bitmap Get(Size size)
        {
            size = new Size(Math.Max(1, size.Width), Math.Max(1, size.Height));
            Bitmap existing;
            if (cache.TryGetValue(size, out existing)) return existing;

            Bitmap bmp = new Bitmap(size.Width, size.Height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (LinearGradientBrush bg = new LinearGradientBrush(
                    new Rectangle(0, 0, size.Width, size.Height),
                    ColorTranslator.FromHtml(Dark ? "#0E2A2D" : "#B8F5E8"), ColorTranslator.FromHtml(Dark ? "#1A1D40" : "#B9C7FF"), 60f))
                    g.FillRectangle(bg, 0, 0, size.Width, size.Height);

                // Soft glowing blobs: these are what the glass "blurs".
                Blob(g, new Point(80, 150), 220, Dark ? "#0BA88C" : "#4DFED1");
                Blob(g, new Point(size.Width - 40, 330), 260, Dark ? "#6B4CFF" : "#A58BFF");
                Blob(g, new Point(120, size.Height - 200), 240, Dark ? "#2F6BFF" : "#7FB5FF");
                Blob(g, new Point(size.Width - 90, size.Height - 120), 200, Dark ? "#C2338F" : "#FFB7E1");
            }
            cache[size] = bmp;
            return bmp;
        }

        private static void Blob(Graphics g, Point c, int r, string hex)
        {
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddEllipse(c.X - r, c.Y - r, r * 2, r * 2);
                using (PathGradientBrush pgb = new PathGradientBrush(path))
                {
                    pgb.CenterColor = Color.FromArgb(Dark ? 110 : 200, ColorTranslator.FromHtml(hex));
                    pgb.SurroundColors = new Color[] { Color.FromArgb(0, ColorTranslator.FromHtml(hex)) };
                    g.FillPath(pgb, path);
                }
            }
        }

        // Paints whatever is "behind" a control: the backdrop slice, then any glass cards above it.
        public static void PaintBehind(Control c, Graphics g)
        {
            Form f = c.FindForm();
            if (f == null || !c.IsHandleCreated) { g.Clear(Color.FromArgb(185, 245, 232)); return; }

            Point origin = f.PointToClient(c.PointToScreen(Point.Empty));
            g.DrawImage(Get(f.ClientSize), new Rectangle(0, 0, c.Width, c.Height),
                        origin.X, origin.Y, c.Width, c.Height, GraphicsUnit.Pixel);

            List<ModernCard> chain = new List<ModernCard>();
            for (Control p = c.Parent; p != null; p = p.Parent)
            {
                ModernCard m = p as ModernCard;
                if (m != null) chain.Insert(0, m);
            }
            foreach (ModernCard m in chain)
            {
                Point mo = f.PointToClient(m.PointToScreen(Point.Empty));
                m.PaintGlassFill(g, new Rectangle(mo.X - origin.X, mo.Y - origin.Y, m.Width, m.Height));
            }
        }
    }

    // Frosted glass surface: translucent tint over the backdrop, with a thin light edge.
    public class ModernCard : Panel
    {
        public int Radius = 22;
        public int TintAlpha = 110;     // 0 = clear, 255 = solid
        public bool Glass = true;

        public ModernCard()
        {
            DoubleBuffered = true;
            BackColor = Color.White;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Padding = new Padding(0);
        }

        public void PaintGlassFill(Graphics g, Rectangle r)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = Gfx.RoundRect(r, Radius))
            {
                if (TintAlpha >= 255)
                {
                    using (SolidBrush solid = new SolidBrush(BackColor))
                        g.FillPath(solid, path);
                    return;
                }
                // Frost: brighter at the top-left, more transparent at the bottom-right.
                using (LinearGradientBrush fill = new LinearGradientBrush(r,
                    Color.FromArgb(Math.Min(255, TintAlpha + 40), BackColor),
                    Color.FromArgb(Math.Max(0, TintAlpha - 30), BackColor), 45f))
                    g.FillPath(fill, path);
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            if (!Glass)
            {
                g.Clear(Parent != null ? Parent.BackColor : SystemColors.Control);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (SolidBrush b = new SolidBrush(BackColor))
                using (GraphicsPath p = Gfx.RoundRect(new Rectangle(0, 0, Width, Height), Radius))
                    g.FillPath(b, p);
                return;
            }

            Backdrop.PaintBehind(this, g);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            PaintGlassFill(g, r);

            // Thin light edge: the signature glass highlight.
            if (TintAlpha < 255)
            {
                using (Pen edge = new Pen(Color.FromArgb(Backdrop.Dark ? 45 : 150, 255, 255, 255), 1f))
                using (GraphicsPath p = Gfx.RoundRect(r, Radius))
                    g.DrawPath(edge, p);
            }
        }
    }

    // Panel that scrolls its own content (used for long workout lists) and reacts to the mouse wheel on hover.
    public class ScrollPanel : Panel
    {
        public ScrollPanel()
        {
            SetStyle(ControlStyles.Selectable, true);
            TabStop = false;
            AutoScroll = true;
            DoubleBuffered = true;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Backdrop.PaintBehind(this, e.Graphics);
        }

        protected override void OnScroll(ScrollEventArgs se)
        {
            base.OnScroll(se);
            Invalidate(true);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            Invalidate(true);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            Focus();
        }
    }

    // Fixed bottom tab bar: all tabs are always visible, active tab gets a soft pill.
    public class NavBar : Control
    {
        public string[] Names = new string[0];
        public string[] Icons = new string[0];
        public Color Accent = Color.FromArgb(7, 143, 120);
        public Color PillColor = Color.FromArgb(217, 255, 245);
        public Color HoverColor = Color.FromArgb(240, 255, 252);
        public Color MutedColor = Color.FromArgb(74, 102, 100);
        public Color GlassColor = Color.FromArgb(120, 255, 255, 255);
        public Color EdgeColor = Color.FromArgb(160, 255, 255, 255);
        public event Action<int> ItemClicked;

        private int selected;
        private int hover = -1;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Selected
        {
            get { return selected; }
            set
            {
                if (selected != value)
                {
                    selected = value;
                    Invalidate();
                }
            }
        }

        public NavBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Height = 70;
        }

        private int IndexAt(int x)
        {
            if (Names.Length == 0) return -1;
            int index = x * Names.Length / Math.Max(1, Width);
            return Math.Max(0, Math.Min(Names.Length - 1, index));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int index = IndexAt(e.X);
            if (index != hover)
            {
                hover = index;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hover = -1;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left) return;
            int index = IndexAt(e.X);
            if (index >= 0 && ItemClicked != null)
                ItemClicked(index);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Backdrop.PaintBehind(this, g);
            using (SolidBrush glass = new SolidBrush(GlassColor))
                g.FillRectangle(glass, 0, 0, Width, Height);
            using (Pen edge = new Pen(EdgeColor))
                g.DrawLine(edge, 0, 0, Width, 0);

            int count = Names.Length;
            if (count == 0) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            int slotWidth = Width / count;

            using (Font iconFont = new Font("Segoe UI Symbol", 15F))
            using (Font labelFont = new Font("Segoe UI", 8.5F, FontStyle.Bold))
            using (StringFormat format = new StringFormat())
            {
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                for (int i = 0; i < count; i++)
                {
                    bool active = i == selected;
                    Rectangle pill = new Rectangle(i * slotWidth + (slotWidth - 50) / 2, 8, 50, 30);
                    if (active || i == hover)
                    {
                        using (SolidBrush fill = new SolidBrush(active ? PillColor : HoverColor))
                        using (GraphicsPath path = Gfx.RoundRect(pill, 15))
                            g.FillPath(fill, path);
                    }
                    using (SolidBrush text = new SolidBrush(active ? Accent : MutedColor))
                    {
                        string icon = i < Icons.Length ? Icons[i] : "";
                        g.DrawString(icon, iconFont, text, new RectangleF(pill.X, pill.Y, pill.Width, pill.Height), format);
                        g.DrawString(Names[i], labelFont, text, new RectangleF(i * slotWidth, 42, slotWidth, 20), format);
                    }
                }
            }
        }
    }

    // Flat pill button with no border.
    public class RoundButton : Control
    {
        public Color Fill = Color.MediumTurquoise;
        public Color HoverFill = Color.Turquoise;
        private bool hover;

        public RoundButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Backdrop.PaintBehind(this, e.Graphics);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            using (SolidBrush brush = new SolidBrush(hover ? HoverFill : Fill))
            using (GraphicsPath path = Gfx.RoundRect(new Rectangle(0, 0, Width, Height), Height / 2))
                e.Graphics.FillPath(brush, path);
            using (SolidBrush text = new SolidBrush(ForeColor))
            using (StringFormat format = new StringFormat())
            {
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                e.Graphics.DrawString(Text, Font, text, new RectangleF(0, 0, Width, Height), format);
            }
        }
    }

    // Slim rounded progress bar.
    public class SoftProgress : Control
    {
        private int value;
        public Color Track = Color.FromArgb(226, 244, 240);
        public Color Fill = Color.FromArgb(11, 219, 182);

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Value
        {
            get { return value; }
            set { this.value = Math.Max(0, Math.Min(100, value)); Invalidate(); }
        }

        public SoftProgress()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Backdrop.PaintBehind(this, e.Graphics);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush track = new SolidBrush(Track))
            using (GraphicsPath path = Gfx.RoundRect(new Rectangle(0, 0, Width, Height), Height / 2))
                e.Graphics.FillPath(track, path);
            int w = Width * value / 100;
            if (w > 0)
            {
                using (SolidBrush fill = new SolidBrush(Fill))
                using (GraphicsPath path = Gfx.RoundRect(new Rectangle(0, 0, Math.Max(w, Height), Height), Height / 2))
                    e.Graphics.FillPath(fill, path);
            }
        }
    }

    public class Workout
    {
        public string Name;
        public string Category;
        public int Duration;
        public int Calories;
        public DateTime Date;

        public Workout(string name, string category, int duration, int calories, DateTime date)
        {
            Name = name;
            Category = category;
            Duration = duration;
            Calories = calories;
            Date = date;
        }
    }

    public class MiniBarChart : Control
    {
        public int[] Values = new int[0];
        public string[] Labels = new string[0];
        public Color Accent = Color.MediumTurquoise;
        public Color LabelColor = Color.FromArgb(74, 102, 100);

        public MiniBarChart()
        {
            DoubleBuffered = true;
            BackColor = Color.White;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Backdrop.PaintBehind(this, e.Graphics);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (Values == null || Values.Length == 0)
                return;

            int max = Math.Max(1, Values.Max());
            int bottom = Height - 28;
            int slot = Math.Max(1, (Width - 20) / Values.Length);
            using (SolidBrush barBrush = new SolidBrush(Accent))
            using (SolidBrush labelBrush = new SolidBrush(LabelColor))
            using (Pen linePen = new Pen(Color.FromArgb(Backdrop.Dark ? 50 : 150, 255, 255, 255)))
            using (Font labelFont = new Font("Segoe UI", 9F))
            using (Font valueFont = new Font("Segoe UI", 9F, FontStyle.Bold))
            {
                e.Graphics.DrawLine(linePen, 0, bottom, Width, bottom);
                for (int i = 0; i < Values.Length; i++)
                {
                    int barWidth = Math.Min(36, Math.Max(14, slot / 2));
                    int barHeight = (int)((bottom - 30) * Values[i] / (double)max);
                    int x = i * slot + (slot - barWidth) / 2;
                    int y = bottom - barHeight;
                    if (barHeight > 0)
                    {
                        using (GraphicsPath barPath = new GraphicsPath())
                        {
                            int radius = Math.Min(7, Math.Max(2, barWidth / 2));
                            int diameter = radius * 2;
                            barPath.AddArc(x, y, diameter, diameter, 180, 90);
                            barPath.AddArc(x + barWidth - diameter, y, diameter, diameter, 270, 90);
                            barPath.AddLine(x + barWidth, y + radius, x + barWidth, bottom);
                            barPath.AddLine(x + barWidth, bottom, x, bottom);
                            barPath.AddLine(x, bottom, x, y + radius);
                            barPath.CloseFigure();
                            e.Graphics.FillPath(barBrush, barPath);
                        }
                    }
                    string label = i < Labels.Length ? Labels[i] : (i + 1).ToString();
                    SizeF labelSize = e.Graphics.MeasureString(label, labelFont);
                    e.Graphics.DrawString(label, labelFont, labelBrush, x + (barWidth - labelSize.Width) / 2, bottom + 5);
                    SizeF valueSize = e.Graphics.MeasureString(Values[i].ToString(), valueFont);
                    e.Graphics.DrawString(Values[i].ToString(), valueFont, labelBrush, x + (barWidth - valueSize.Width) / 2, Math.Max(0, y - 18));
                }
            }
        }
    }
}