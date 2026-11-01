using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FitTrack
{
    public partial class MainForm : Form
    {
        // Palette: deep ink, soft aqua canvas, mint/teal accents. No outlines anywhere.
        private Color aquaBg = ColorTranslator.FromHtml("#E9FFFF");
        private Color mint = ColorTranslator.FromHtml("#4DFED1");
        private Color teal = ColorTranslator.FromHtml("#0BDBB6");
        private Color dark = ColorTranslator.FromHtml("#173B3A");
        private Color muted = ColorTranslator.FromHtml("#6B8583");
        private Color softField = ColorTranslator.FromHtml("#EFFAF8");

        // The content area is a fixed 400px wide and every page is designed to fit without page scrolling.
        private const int CW = 400;

        private List<Workout> workouts = new List<Workout>();
        private Stack<Workout> undoStack = new Stack<Workout>();
        private string historySortStatus = "Pick a field, then choose a sorting algorithm.";
        private string historyCriterion = "Duration (minutes)";

        private string profileName = "Fitness User";
        private string fitnessLevel = "Beginner";
        private int dailyGoal = 45;
        private bool showSavedNotice;

        private NavBar navBar;
        private string[] navKeys = new string[] { "Dashboard", "My Workouts", "Workout History", "Fitness Goals", "Progress", "Settings" };

        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        private void DragWindow(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, 0xA1, 0x2, 0);
            }
        }

        private Label MakeWindowButton(string text, int x, Action action, Color hover)
        {
            Label button = MakeLabel(text, x, 14, 30, 28, 10, true, muted);
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.Cursor = Cursors.Hand;
            button.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            button.Click += delegate { action(); };
            button.MouseEnter += delegate { button.ForeColor = hover; };
            button.MouseLeave += delegate { button.ForeColor = muted; };
            return button;
        }

        // Wraps an input control in a soft, borderless rounded field.
        private Control MakeInput(Control input, int x, int y, int width)
        {
            ModernCard holder = new ModernCard();
            holder.Radius = 12;
            holder.BackColor = softField;
            holder.Location = new Point(x, y);
            holder.Size = new Size(width, 38);
            input.Font = new Font("Segoe UI", 10F);
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

        public MainForm()
        {
            workouts.Add(new Workout("Full Body Strength", "Strength", 45, 320, DateTime.Today.AddDays(-1)));
            workouts.Add(new Workout("Morning Cardio", "Cardio", 30, 210, DateTime.Today.AddDays(-2)));
            workouts.Add(new Workout("Upper Body", "Strength", 40, 275, DateTime.Today.AddDays(-4)));
            workouts.Add(new Workout("Evening Walk", "Cardio", 25, 130, DateTime.Today.AddDays(-5)));

            InitializeComponent();
            BuildNavigation();
            ApplyMobileLayout();
            ShowDashboard();
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
            this.Region = new Region(Gfx.RoundRect(new Rectangle(0, 0, this.Width, this.Height), 28));

            // Slim top bar: brand + window controls, same color as the page so it feels like one canvas.
            sidebar.Dock = DockStyle.Top;
            sidebar.Height = 56;
            sidebar.Padding = new Padding(20, 4, 18, 0);
            sidebar.BackColor = aquaBg;
            menuLabel.Visible = false;
            tipPanel.Visible = false;
            navigation.Visible = false;

            logoLabel.Dock = DockStyle.Top;
            logoLabel.Height = 48;
            logoLabel.Font = new Font("Segoe UI", 17F, FontStyle.Bold);
            logoLabel.ForeColor = dark;
            logoLabel.Text = "●  fittrack";
            logoLabel.TextAlign = ContentAlignment.MiddleLeft;

            mainPanel.BackColor = aquaBg;
            mainPanel.Padding = new Padding(15, 4, 15, 0);
            headerPanel.Height = 62;
            headerPanel.BackColor = aquaBg;
            titleLabel.Font = new Font("Segoe UI", 19F, FontStyle.Bold);
            titleLabel.ForeColor = dark;
            titleLabel.Location = new Point(0, 0);
            subtitleLabel.Location = new Point(2, 38);
            subtitleLabel.Font = new Font("Segoe UI", 8.5F);
            subtitleLabel.ForeColor = muted;
            subtitleLabel.MaximumSize = new Size(380, 30);
            profileLabel.Visible = false;
            mainContent.BackColor = aquaBg;
            mainContent.AutoScroll = false;

            // Borderless window: custom minimize/close and drag-to-move.
            sidebar.Controls.Add(MakeWindowButton("✕", 372, delegate { Close(); }, Color.FromArgb(220, 70, 70)));
            sidebar.Controls.Add(MakeWindowButton("—", 340, delegate { WindowState = FormWindowState.Minimized; }, dark));
            foreach (Control c in sidebar.Controls) if (c is Label) c.BringToFront();
            sidebar.MouseDown += DragWindow;
            logoLabel.MouseDown += DragWindow;
        }

        // Fixed bottom navigation: all six tabs are always visible, no scrolling, no hamburger menu.
        private void BuildNavigation()
        {
            navBar = new NavBar();
            navBar.Dock = DockStyle.Bottom;
            navBar.Height = 70;
            navBar.Names = new string[] { "Home", "Train", "History", "Goals", "Stats", "Settings" };
            navBar.Icons = new string[] { "⌂", "♥", "↺", "◎", "◔", "⚙" };
            Action[] pages = new Action[] { ShowDashboard, ShowWorkouts, ShowHistory, ShowGoals, ShowProgress, ShowSettings };
            navBar.ItemClicked += delegate(int index)
            {
                navBar.Selected = index;
                pages[index]();
            };
            this.Controls.Add(navBar);
        }

        private void SetActiveNavigation(string selected)
        {
            int index = Array.IndexOf(navKeys, selected);
            if (index >= 0 && navBar != null)
                navBar.Selected = index;
        }

        private void PreparePage(string title, string subtitle)
        {
            titleLabel.Text = title;
            subtitleLabel.Text = subtitle;
            mainContent.Controls.Clear();
        }

        private Panel MakeCard(int x, int y, int width, int height)
        {
            Panel panel = new ModernCard();
            panel.Location = new Point(x, y);
            panel.Size = new Size(width, height);
            panel.BackColor = Color.White;
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
            label.Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular);
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
            button.HoverFill = primary ? ColorTranslator.FromHtml("#20E7C0") : ColorTranslator.FromHtml("#D8F7F1");
            button.ForeColor = primary ? dark : ColorTranslator.FromHtml("#416B67");
            button.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
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
            card.Controls.Add(MakeLabel(value, 12, 30, width - 20, 32, 17, true, dark));
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

            // Hero: today's goal at a glance.
            Panel hero = MakeCard(0, 0, CW, 120);
            hero.BackColor = ColorTranslator.FromHtml("#123D3A");
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
            hero.Controls.Add(MakeButton("Goals", CW - 92, 22, 72, 30, ShowGoals, true));

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
            chartCard.Controls.Add(chart);

            int recentCount = Math.Min(3, workouts.Count);
            Panel recent = MakeCard(0, 419, CW, recentCount == 0 ? 110 : 44 + recentCount * 54 + 6);
            recent.Controls.Add(MakeLabel("Recent workouts", 18, 12, 220, 24, 11, true, dark));
            recent.Controls.Add(MakeButton("See all", CW - 84, 10, 66, 28, ShowHistory, false));
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
            dialog.Region = new Region(Gfx.RoundRect(new Rectangle(0, 0, dialog.Width, dialog.Height), 28));
            dialog.MouseDown += DragWindow;

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
                workouts.Insert(0, new Workout(name.Text.Trim(), category.Text, (int)duration.Value, (int)calories.Value, DateTime.Now));
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
                row.BackColor = ColorTranslator.FromHtml("#F0FFFC");

                ModernCard badge = new ModernCard();
                badge.Radius = 10;
                badge.Location = new Point(9, 8);
                badge.Size = new Size(32, 32);
                badge.BackColor = ColorTranslator.FromHtml("#D9FFF5");
                string initial = item.Category.Length > 0 ? item.Category.Substring(0, 1).ToUpper() : "•";
                Label initialLabel = MakeLabel(initial, 0, 0, 32, 32, 10, true, ColorTranslator.FromHtml("#078F78"));
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
                dot.BackColor = done ? teal : ColorTranslator.FromHtml("#EAF6F3");
                Label letter = MakeLabel(day.ToString("ddd").Substring(0, 1), 0, 0, 38, 38, 9, true, done ? dark : muted);
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
            string initial = profileName.Trim().Length > 0 ? profileName.Trim().Substring(0, 1).ToUpper() : "F";
            Label initialLabel = MakeLabel(initial, 0, 0, 64, 64, 20, true, dark);
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
                dailyGoal = (int)goalInput.Value;
                showSavedNotice = true;
                ShowSettings();
            }, true));

            if (showSavedNotice)
            {
                Label notice = MakeLabel("✓  Preferences saved", 20, 374, CW - 40, 20, 8, true, ColorTranslator.FromHtml("#078F78"));
                notice.TextAlign = ContentAlignment.MiddleCenter;
                p.Controls.Add(notice);
                showSavedNotice = false;
            }

            Panel tip = MakeCard(0, 420, CW, 86);
            tip.BackColor = ColorTranslator.FromHtml("#D9FFF5");
            tip.Controls.Add(MakeLabel("STAY CONSISTENT", 20, 14, 200, 18, 7, true, dark));
            tip.Controls.Add(MakeLabel("Small steps every day lead to big results. Keep moving!", 20, 36, CW - 40, 36, 9, false, muted));
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

    // Borderless, softly rounded surface.
    public class ModernCard : Panel
    {
        public int Radius = 22;

        public ModernCard()
        {
            DoubleBuffered = true;
            BackColor = Color.White;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Padding = new Padding(0);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Color outer = Parent != null ? Parent.BackColor : SystemColors.Control;
            using (SolidBrush clear = new SolidBrush(outer))
                e.Graphics.FillRectangle(clear, ClientRectangle);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush brush = new SolidBrush(BackColor))
            using (GraphicsPath path = Gfx.RoundRect(new Rectangle(0, 0, Width, Height), Radius))
                e.Graphics.FillPath(brush, path);
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
        public Color MutedColor = Color.FromArgb(107, 133, 131);
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
            g.Clear(Color.White);
            int count = Names.Length;
            if (count == 0) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            int slotWidth = Width / count;

            using (Font iconFont = new Font("Segoe UI Symbol", 14F))
            using (Font labelFont = new Font("Segoe UI", 7.5F, FontStyle.Bold))
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
            e.Graphics.Clear(Parent != null ? Parent.BackColor : SystemColors.Control);
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
            e.Graphics.Clear(Parent != null ? Parent.BackColor : SystemColors.Control);
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

        public MiniBarChart()
        {
            DoubleBuffered = true;
            BackColor = Color.White;
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
            using (SolidBrush labelBrush = new SolidBrush(Color.FromArgb(105, 119, 111)))
            using (Pen linePen = new Pen(Color.FromArgb(232, 237, 233)))
            using (Font labelFont = new Font("Segoe UI", 8F))
            using (Font valueFont = new Font("Segoe UI", 8F, FontStyle.Bold))
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