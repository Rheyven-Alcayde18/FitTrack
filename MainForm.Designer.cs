using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace FitTrack
{
    partial class MainForm
    {
        private IContainer components = null;
        private Panel sidebar;
        private Label logoLabel;
        private Label menuLabel;
        private FlowLayoutPanel navigation;
        private Panel tipPanel;
        private Label tipTitleLabel;
        private Label tipTextLabel;
        private Panel mainPanel;
        private Panel headerPanel;
        private Label titleLabel;
        private Label subtitleLabel;
        private Label profileLabel;
        private Panel mainContent;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new Container();
            this.sidebar = new Panel();
            this.logoLabel = new Label();
            this.menuLabel = new Label();
            this.navigation = new FlowLayoutPanel();
            this.tipPanel = new Panel();
            this.tipTitleLabel = new Label();
            this.tipTextLabel = new Label();
            this.mainPanel = new Panel();
            this.headerPanel = new Panel();
            this.titleLabel = new Label();
            this.subtitleLabel = new Label();
            this.profileLabel = new Label();
            this.mainContent = new Panel();
            this.sidebar.SuspendLayout();
            this.tipPanel.SuspendLayout();
            this.mainPanel.SuspendLayout();
            this.headerPanel.SuspendLayout();
            this.SuspendLayout();
            // Form
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.BackColor = Color.FromArgb(233, 255, 255);
            this.ClientSize = new Size(430, 850);
            this.MinimumSize = new Size(390, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "FitTrack - Personal Fitness Tracker";
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, ((byte)(0)));
            // sidebar
            this.sidebar.BackColor = Color.White;
            this.sidebar.Dock = DockStyle.Left;
            this.sidebar.Padding = new Padding(16);
            this.sidebar.Width = 430;
            this.sidebar.Controls.Add(this.navigation);
            this.sidebar.Controls.Add(this.menuLabel);
            this.sidebar.Controls.Add(this.logoLabel);
            this.sidebar.Controls.Add(this.tipPanel);
            // logoLabel
            this.logoLabel.Dock = DockStyle.Top;
            this.logoLabel.Font = new Font("Segoe UI", 20F, FontStyle.Bold);
            this.logoLabel.ForeColor = Color.FromArgb(23, 59, 58);
            this.logoLabel.Height = 65;
            this.logoLabel.Text = "✓  FitTrack";
            this.logoLabel.TextAlign = ContentAlignment.MiddleLeft;
            // menuLabel
            this.menuLabel.Dock = DockStyle.Top;
            this.menuLabel.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            this.menuLabel.ForeColor = Color.FromArgb(107, 133, 131);
            this.menuLabel.Height = 35;
            this.menuLabel.Text = "MENU";
            this.menuLabel.TextAlign = ContentAlignment.MiddleLeft;
            // navigation
            this.navigation.Dock = DockStyle.Top;
            this.navigation.FlowDirection = FlowDirection.TopDown;
            this.navigation.Height = 48;
            this.navigation.WrapContents = false;
            this.navigation.AutoScroll = true;
            this.navigation.Name = "navigation";
            // tipPanel
            this.tipPanel.BackColor = Color.FromArgb(234, 255, 249);
            this.tipPanel.Dock = DockStyle.Bottom;
            this.tipPanel.Height = 105;
            this.tipPanel.Controls.Add(this.tipTextLabel);
            this.tipPanel.Controls.Add(this.tipTitleLabel);
            // tipTitleLabel
            this.tipTitleLabel.AutoSize = true;
            this.tipTitleLabel.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            this.tipTitleLabel.ForeColor = Color.FromArgb(23, 59, 58);
            this.tipTitleLabel.Location = new Point(12, 12);
            this.tipTitleLabel.Text = "STAY CONSISTENT";
            // tipTextLabel
            this.tipTextLabel.Font = new Font("Segoe UI", 9F);
            this.tipTextLabel.ForeColor = Color.FromArgb(107, 133, 131);
            this.tipTextLabel.Location = new Point(12, 38);
            this.tipTextLabel.Size = new Size(175, 55);
            this.tipTextLabel.Text = "Small steps every day lead to big results. Keep moving!";
            // mainPanel
            this.mainPanel.BackColor = Color.FromArgb(233, 255, 255);
            this.mainPanel.Dock = DockStyle.Fill;
            this.mainPanel.Padding = new Padding(25, 20, 25, 20);
            this.mainPanel.Controls.Add(this.mainContent);
            this.mainPanel.Controls.Add(this.headerPanel);
            // headerPanel
            this.headerPanel.BackColor = Color.FromArgb(233, 255, 255);
            this.headerPanel.Dock = DockStyle.Top;
            this.headerPanel.Height = 80;
            this.headerPanel.Controls.Add(this.profileLabel);
            this.headerPanel.Controls.Add(this.subtitleLabel);
            this.headerPanel.Controls.Add(this.titleLabel);
            // titleLabel
            this.titleLabel.AutoSize = true;
            this.titleLabel.Font = new Font("Segoe UI", 22F, FontStyle.Bold);
            this.titleLabel.ForeColor = Color.FromArgb(23, 59, 58);
            this.titleLabel.Location = new Point(0, 0);
            this.titleLabel.Text = "Dashboard";
            // subtitleLabel
            this.subtitleLabel.AutoSize = true;
            this.subtitleLabel.Font = new Font("Segoe UI", 9F);
            this.subtitleLabel.ForeColor = Color.FromArgb(107, 133, 131);
            this.subtitleLabel.Location = new Point(2, 43);
            this.subtitleLabel.Text = "Welcome back! Here is your fitness overview.";
            // profileLabel
            this.profileLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.profileLabel.AutoSize = true;
            this.profileLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            this.profileLabel.ForeColor = Color.FromArgb(23, 59, 58);
            this.profileLabel.Location = new Point(850, 18);
            this.profileLabel.Text = "●  MY PROFILE";
            // mainContent
            this.mainContent.AutoScroll = true;
            this.mainContent.BackColor = Color.FromArgb(233, 255, 255);
            this.mainContent.Dock = DockStyle.Fill;
            this.mainContent.Name = "mainContent";
            // Add root controls
            this.Controls.Add(this.mainPanel);
            this.Controls.Add(this.sidebar);
            this.sidebar.ResumeLayout(false);
            this.tipPanel.ResumeLayout(false);
            this.tipPanel.PerformLayout();
            this.mainPanel.ResumeLayout(false);
            this.headerPanel.ResumeLayout(false);
            this.headerPanel.PerformLayout();
            this.ResumeLayout(false);
        }
    }
}
