using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FitTrack
{
    public partial class MainForm : Form
    {
        // Modern fitness-app palette: deep ink, soft canvas, and energetic lime.
        private Color aquaBg = ColorTranslator.FromHtml("#E9FFFF");
        private Color mint = ColorTranslator.FromHtml("#4DFED1");
        private Color teal = ColorTranslator.FromHtml("#0BDBB6");
        private Color dark = ColorTranslator.FromHtml("#173B3A");
        private Color muted = ColorTranslator.FromHtml("#6B8583");
        private Color panelWhite = Color.White;

        private List<Workout> workouts = new List<Workout>();
        private Stack<Workout> undoStack = new Stack<Workout>();
        private string historySortStatus = "Choose a sorting algorithm to arrange the workout records in ascending order.";

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
            holder.BackColor = ColorTranslator.FromHtml("#EFFAF8");
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
            // Phone-sized app canvas with a compact brand header and bottom-style navigation.
            this.ClientSize = new Size(430, 850);
            this.MinimumSize = new Size(390, 700);
            this.MaximumSize = new Size(470, 1000);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = aquaBg;
            this.FormBorderStyle = FormBorderStyle.None;
            this.MaximizeBox = false;
            this.Region = new Region(Gfx.RoundRect(new Rectangle(0, 0, this.Width, this.Height), 28));

            sidebar.Dock = DockStyle.Top;
            sidebar.Height = 104;
            sidebar.Width = 430;
            sidebar.Padding = new Padding(18, 5, 18, 5);
            sidebar.BackColor = Color.White;
            menuLabel.Visible = false;
            tipPanel.Visible = false;

            logoLabel.Dock = DockStyle.Top;
            logoLabel.Height = 45;
            logoLabel.Font = new Font("Segoe UI", 18F, FontStyle.Bold);
            logoLabel.ForeColor = dark;
            logoLabel.Text = "●  fittrack";
            logoLabel.TextAlign = ContentAlignment.MiddleLeft;

            navigation.Dock = DockStyle.Bottom;
            navigation.Height = 48;
            navigation.FlowDirection = FlowDirection.LeftToRight;
            navigation.WrapContents = false;
            navigation.AutoScroll = true;
            navigation.HorizontalScroll.Enabled = true;
            navigation.HorizontalScroll.Visible = false;
            navigation.Padding = new Padding(0, 3, 0, 0);
            navigation.Margin = new Padding(0);
            navigation.BackColor = Color.White;

            mainPanel.BackColor = aquaBg;
            mainPanel.Padding = new Padding(15, 12, 15, 12);
            headerPanel.Height = 76;
            headerPanel.BackColor = aquaBg;
            titleLabel.Font = new Font("Segoe UI", 19F, FontStyle.Bold);
            titleLabel.ForeColor = dark;
            titleLabel.Location = new Point(0, 0);
            subtitleLabel.Location = new Point(2, 43);
            subtitleLabel.Font = new Font("Segoe UI", 8.5F);
            subtitleLabel.ForeColor = muted;
            subtitleLabel.MaximumSize = new Size(360, 30);
            profileLabel.Visible = false;
            mainContent.BackColor = aquaBg;

            // Borderless window: custom minimize/close and drag-to-move.
            sidebar.Controls.Add(MakeWindowButton("✕", 372, delegate { Close(); }, Color.FromArgb(220, 70, 70)));
            sidebar.Controls.Add(MakeWindowButton("—", 340, delegate { WindowState = FormWindowState.Minimized; }, dark));
            foreach (Control c in sidebar.Controls) if (c is Label) c.BringToFront();
            sidebar.MouseDown += DragWindow;
            logoLabel.MouseDown += DragWindow;

            foreach (Control control in navigation.Controls)
            {
                Button button = control as Button;
                if (button != null)
                {
                    button.Width = 78;
                    button.Height = 37;
                    button.Margin = new Padding(0, 1, 4, 1);
                    button.Font = new Font("Segoe UI", 8.2F, FontStyle.Bold);
                    string fullName = button.Tag.ToString();
                    button.Text = fullName == "Dashboard" ? "Home" :
                                  fullName == "My Workouts" ? "Train" :
                                  fullName == "Workout History" ? "History" :
                                  fullName == "Fitness Goals" ? "Goals" :
                                  fullName == "Progress" ? "Stats" : "Settings";
                    button.TextAlign = ContentAlignment.MiddleCenter;
                    button.FlatAppearance.BorderSize = 0;
                    button.BackColor = Color.White;
                }
            }
        }

        private void BuildNavigation()
        {
            AddNavigationButton("Dashboard", ShowDashboard);
            AddNavigationButton("My Workouts", ShowWorkouts);
            AddNavigationButton("Workout History", ShowHistory);
            AddNavigationButton("Fitness Goals", ShowGoals);
            AddNavigationButton("Progress", ShowProgress);
            AddNavigationButton("Settings", ShowSettings);
        }

        private void AddNavigationButton(string text, Action clickAction)
        {
            Button button = new Button();
            button.Text = text;
            button.Width = 190;
            button.Height = 44;
            button.Margin = new Padding(0, 2, 0, 5);
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            button.ForeColor = muted;
            button.BackColor = panelWhite;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Cursor = Cursors.Hand;
            button.Tag = text;
            button.Click += delegate
            {
                SetActiveNavigation(text);
                clickAction();
            };
            navigation.Controls.Add(button);
        }

        private void SetActiveNavigation(string selected)
        {
            foreach (Control control in navigation.Controls)
            {
                Button button = control as Button;
                if (button != null)
                {
                    bool active = button.Tag.ToString() == selected;
                    button.BackColor = active ? ColorTranslator.FromHtml("#D9FFF5") : panelWhite;
                    button.ForeColor = active ? ColorTranslator.FromHtml("#078F78") : muted;
                }
            }
        }

        private void PreparePage(string title, string subtitle)
        {
            titleLabel.Text = title;
            subtitleLabel.Text = subtitle;
            mainContent.Controls.Clear();
            mainContent.AutoScrollPosition = new Point(0, 0);
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

        private Control MakeButton(string text, int x, int y, int width, int height, Action action, bool primary)
        {
            RoundButton button = new RoundButton();
            button.Text = text;
            button.Location = new Point(x, y);
            button.Size = new Size(width, height);
            button.Fill = primary ? teal : ColorTranslator.FromHtml("#EFFAF8");
            button.HoverFill = primary ? ColorTranslator.FromHtml("#20E7C0") : ColorTranslator.FromHtml("#D8F7F1");
            button.ForeColor = primary ? dark : ColorTranslator.FromHtml("#416B67");
            button.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            button.Click += delegate { action(); };
            return button;
        }

        private void AddStat(string heading, string value, string foot, int x, int width)
        {
            Panel card = MakeCard(x, 0, width, 112);
            card.Controls.Add(MakeLabel(heading, 13, 12, width - 24, 22, 7, true, muted));
            card.Controls.Add(MakeLabel(value, 13, 37, width - 24, 36, 20, true, dark));
            card.Controls.Add(MakeLabel(foot, 13, 78, width - 24, 22, 7, false, muted));
        }

        private void ShowDashboard()
        {
            PreparePage("Today", "A little progress adds up. Keep moving.");
            SetActiveNavigation("Dashboard");
            int w = Math.Max(340, mainContent.ClientSize.Width - 8);
            int gap = 9;
            int cardW = (w - gap) / 2;
            DateTime weekStart = DateTime.Today.AddDays(-6);
            List<Workout> thisWeek = workouts.Where(item => item.Date.Date >= weekStart && item.Date.Date <= DateTime.Today).ToList();
            int sessions = thisWeek.Count;
            int minutes = thisWeek.Sum(item => item.Duration);
            int calories = thisWeek.Sum(item => item.Calories);
            int average = sessions == 0 ? 0 : minutes / sessions;

            Panel welcome = MakeCard(0, 0, w - 8, 104);
            welcome.BackColor = ColorTranslator.FromHtml("#123D3A");
            welcome.Controls.Add(MakeLabel("YOUR FITNESS, YOUR PACE", 17, 13, welcome.Width - 34, 18, 7, true, ColorTranslator.FromHtml("#4DFED1")));
            welcome.Controls.Add(MakeLabel("Make today count.", 17, 34, welcome.Width - 34, 34, 17, true, Color.White));
            welcome.Controls.Add(MakeLabel("Track your sessions and celebrate every win.", 18, 72, welcome.Width - 34, 20, 7, false, ColorTranslator.FromHtml("#D8FFF6")));

            AddStat("WORKOUTS THIS WEEK", sessions.ToString(), "Completed sessions", 0, cardW);
            AddStat("ACTIVE MINUTES", minutes.ToString(), "Minutes this week", cardW + gap, cardW);
            AddStat("CALORIES BURNED", calories.ToString("N0"), "Estimated total", 0, cardW);
            AddStat("AVG. SESSION", average + " min", "Per workout", cardW + gap, cardW);
            Control[] stats = mainContent.Controls.Cast<Control>().Where(c => c is ModernCard).Skip(1).Take(4).ToArray();
            if (stats.Length == 4)
            {
                stats[0].Location = new Point(0, 116);
                stats[1].Location = new Point(cardW + gap, 116);
                stats[2].Location = new Point(0, 237);
                stats[3].Location = new Point(cardW + gap, 237);
            }

            Panel chartCard = MakeCard(0, 358, w - 8, 245);
            chartCard.Controls.Add(MakeLabel("Activity this week", 16, 13, 240, 25, 12, true, dark));
            chartCard.Controls.Add(MakeLabel("Workout minutes by day", 16, 38, 240, 18, 7, false, muted));
            MiniBarChart chart = new MiniBarChart();
            chart.Location = new Point(12, 66);
            chart.Size = new Size(chartCard.Width - 28, 165);
            chart.Values = Enumerable.Range(0, 7).Select(offset => workouts.Where(item => item.Date.Date == weekStart.AddDays(offset).Date).Sum(item => item.Duration)).ToArray();
            chart.Labels = Enumerable.Range(0, 7).Select(offset => weekStart.AddDays(offset).ToString("ddd").Substring(0, 1)).ToArray();
            chart.Accent = ColorTranslator.FromHtml("#0BDBB6");
            chartCard.Controls.Add(chart);

            Panel goalCard = MakeCard(0, 616, w - 8, 145);
            goalCard.Controls.Add(MakeLabel("Daily movement goal", 16, 12, 220, 22, 11, true, dark));
            int todayMinutes = workouts.Where(item => item.Date.Date == DateTime.Today).Sum(item => item.Duration);
            int goalPercent = Math.Min(100, todayMinutes * 100 / 45);
            goalCard.Controls.Add(MakeLabel(todayMinutes + " / 45 min", 16, 40, 170, 30, 17, true, dark));
            SoftProgress progress = new SoftProgress();
            progress.Location = new Point(16, 78);
            progress.Size = new Size(goalCard.Width - 32, 12);
            progress.Value = goalPercent;
            goalCard.Controls.Add(progress);
            goalCard.Controls.Add(MakeLabel(goalPercent + "% completed today", 16, 99, 190, 18, 7, false, muted));
            goalCard.Controls.Add(MakeButton("Goals", goalCard.Width - 92, 96, 75, 30, ShowGoals, false));

            Panel recent = MakeCard(0, 774, w - 8, Math.Max(170, Math.Min(245, workouts.Count * 62 + 55)));
            recent.Controls.Add(MakeLabel("Recent workouts", 16, 12, 200, 24, 12, true, dark));
            recent.Controls.Add(MakeButton("See all", recent.Width - 85, 8, 68, 29, ShowHistory, false));
            AddWorkoutRows(recent, 45, 3);
        }

        private void ShowWorkouts()
        {
            PreparePage("My Workouts", "Build your routine and record each workout session.");
            SetActiveNavigation("My Workouts");
            int w = Math.Max(340, mainContent.ClientSize.Width - 8);
            Panel top = MakeCard(0, 0, w - 8, 95);
            top.Controls.Add(MakeLabel("Your exercise library", 18, 13, top.Width - 150, 25, 12, true, dark));
            top.Controls.Add(MakeButton("+ Add", top.Width - 100, 10, 82, 32, AddWorkoutDialog, true));
            top.Controls.Add(MakeLabel("Add a workout to your personal fitness log.", 18, 49, top.Width - 35, 22, 8, false, muted));

            Panel list = MakeCard(0, 112, w - 8, Math.Max(300, workouts.Count * 62 + 75));
            list.Controls.Add(MakeLabel("Workout sessions", 18, 14, 300, 25, 13, true, dark));
            AddWorkoutRows(list, 52, workouts.Count);
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

        private void AddWorkoutRows(Panel parent, int startY, int count)
        {
            int y = startY;
            foreach (Workout item in workouts.Take(count))
            {
                ModernCard row = new ModernCard();
                row.Radius = 16;
                row.Location = new Point(15, y);
                row.Size = new Size(Math.Max(250, parent.Width - 30), 55);
                row.BackColor = ColorTranslator.FromHtml("#F0FFFC");
                row.Controls.Add(MakeLabel("●", 12, 12, 25, 25, 15, true, ColorTranslator.FromHtml("#0BDBB6")));
                row.Controls.Add(MakeLabel(item.Name, 42, 7, row.Width - 52, 22, 8, true, dark));
                row.Controls.Add(MakeLabel(item.Category + "  •  " + item.Duration + " min  •  " + item.Calories + " kcal", 42, 29, row.Width - 52, 20, 7, false, muted));
                parent.Controls.Add(row);
                y += 62;
            }
            if (count == 0)
                parent.Controls.Add(MakeLabel("No workouts recorded yet. Add your first session!", 20, y, 400, 25, 9, false, muted));
        }

        private void ShowHistory()
        {
            PreparePage("Workout History", "Review sessions and sort your workout records.");
            SetActiveNavigation("Workout History");
            int w = Math.Max(340, mainContent.ClientSize.Width - 8);

            Panel card = MakeCard(0, 0, w - 8, 610);
            card.Controls.Add(MakeLabel("Completed sessions (" + workouts.Count + ")", 18, 14, 350, 25, 12, true, dark));
            card.Controls.Add(MakeLabel("Sort workouts by", 18, 48, 250, 22, 9, true, dark));

            ComboBox sortBy = new ComboBox();
            sortBy.DropDownStyle = ComboBoxStyle.DropDownList;
            sortBy.Items.AddRange(new object[] { "Duration (minutes)", "Calories burned", "Workout name", "Date" });
            sortBy.SelectedIndex = 0;
            card.Controls.Add(MakeInput(sortBy, 18, 70, card.Width - 36));

            Label sortStatus = MakeLabel(historySortStatus, 18, 157, card.Width - 40, 36, 7, false, muted);
            card.Controls.Add(sortStatus);

            card.Controls.Add(MakeButton("Bubble Sort", 18, 111, 104, 32, delegate
            {
                ApplySort("Bubble Sort", sortBy, sortStatus);
                ShowHistory();
            }, true));
            card.Controls.Add(MakeButton("Selection", 130, 111, 104, 32, delegate
            {
                ApplySort("Selection Sort", sortBy, sortStatus);
                ShowHistory();
            }, false));
            card.Controls.Add(MakeButton("Insertion", 242, 111, 104, 32, delegate
            {
                ApplySort("Insertion Sort", sortBy, sortStatus);
                ShowHistory();
            }, false));

            Panel listPanel = new Panel();
            listPanel.Location = new Point(15, 199);
            listPanel.Size = new Size(card.Width - 45, 320);
            listPanel.AutoScroll = true;
            listPanel.BackColor = Color.White;
            card.Controls.Add(listPanel);
            AddWorkoutRows(listPanel, 8, workouts.Count);

            card.Controls.Add(MakeButton("Undo last addition", 18, 548, 160, 36, delegate
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
            historySortStatus = algorithm + " applied successfully. Sorted by " + criterion + ".";
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
            PreparePage("Fitness Goals", "Set realistic targets and celebrate your progress.");
            SetActiveNavigation("Fitness Goals");
            int w = Math.Max(340, mainContent.ClientSize.Width - 8);
            Panel p = MakeCard(0, 0, w - 8, 465);
            p.Controls.Add(MakeLabel("Your weekly goals", 18, 15, 300, 25, 12, true, dark));
            AddGoal(p, "Workout sessions", "4 of 5 sessions", 80, 58);
            AddGoal(p, "Active minutes", "140 of 180 minutes", 78, 145);
            AddGoal(p, "Calories burned", "935 of 1,200 kcal", 78, 232);
            AddGoal(p, "Consistency streak", "3 of 7 days", 43, 319);
            p.Controls.Add(MakeButton("Update goal targets", 18, 405, 175, 36, delegate
            {
                MessageBox.Show("Goal editing can be added in the next development step.", "Fitness goals");
            }, false));
        }

        private void AddGoal(Panel panel, string title, string value, int percent, int y)
        {
            panel.Controls.Add(MakeLabel(title, 18, y, panel.Width - 36, 22, 9, true, dark));
            panel.Controls.Add(MakeLabel(value, 18, y + 23, panel.Width - 36, 19, 8, false, muted));
            SoftProgress bar = new SoftProgress();
            bar.Location = new Point(18, y + 48);
            bar.Size = new Size(panel.Width - 36, 13);
            bar.Value = percent;
            panel.Controls.Add(bar);
        }

        private void ShowProgress()
        {
            PreparePage("Progress", "See how your activity changes over time.");
            SetActiveNavigation("Progress");
            int w = Math.Max(340, mainContent.ClientSize.Width - 8);
            Panel p = MakeCard(0, 0, w - 8, 375);
            p.Controls.Add(MakeLabel("Workout duration overview", 18, 15, 350, 25, 13, true, dark));
            p.Controls.Add(MakeLabel("Sample weekly activity data (minutes)", 18, 43, 350, 20, 8, false, muted));
            MiniBarChart chart = new MiniBarChart();
            chart.Location = new Point(25, 80);
            chart.Size = new Size(p.Width - 55, 245);
            chart.Values = new int[] { 25, 45, 20, 55, 35, 65, 40 };
            chart.Labels = new string[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
            chart.Accent = teal;
            p.Controls.Add(chart);

            Panel summary = MakeCard(0, 395, w - 8, 100);
            summary.Controls.Add(MakeLabel("Your summary", 18, 14, 250, 25, 12, true, dark));
            int total = workouts.Sum(delegate(Workout item) { return item.Duration; });
            summary.Controls.Add(MakeLabel("You have recorded " + workouts.Count + " workouts with " + total + " total active minutes.", 18, 45, summary.Width - 36, 42, 8, false, muted));
        }

        private void ShowSettings()
        {
            PreparePage("Settings", "Manage your profile and application preferences.");
            SetActiveNavigation("Settings");
            int w = Math.Max(340, mainContent.ClientSize.Width - 8);
            Panel p = MakeCard(0, 0, w - 8, 330);
            p.Controls.Add(MakeLabel("Personal profile", 18, 15, 300, 25, 13, true, dark));
            p.Controls.Add(MakeLabel("Name", 18, 65, 150, 22, 9, true, dark));
            TextBox name = new TextBox();
            name.Text = "Fitness User";
            p.Controls.Add(MakeInput(name, 18, 90, p.Width - 36));
            p.Controls.Add(MakeLabel("Fitness level", 18, 145, 150, 22, 9, true, dark));
            ComboBox level = new ComboBox();
            level.DropDownStyle = ComboBoxStyle.DropDownList;
            level.Items.AddRange(new object[] { "Beginner", "Intermediate", "Advanced" });
            level.SelectedIndex = 0;
            p.Controls.Add(MakeInput(level, 18, 170, p.Width - 36));
            p.Controls.Add(MakeButton("Save preferences", 18, 235, 160, 38, delegate
            {
                MessageBox.Show("Preferences saved for this session.");
            }, true));
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

        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
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
                    e.Graphics.DrawString(Values[i].ToString(), valueFont, labelBrush, x, Math.Max(0, y - 18));
                }
            }
        }
    }
}