using System;
using System.Drawing;
using System.Windows.Forms;
using MissionPlanner.Utilities;

namespace MissionPlanner.Controls.PreFlight
{
    /// <summary>
    /// Startup picker: Stage 2 or Stage 3.
    /// </summary>
    public sealed class FlightStageMenuForm : Form
    {
        public FlightStageMenuForm()
        {
            Text = "Select vehicle";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(360, 160);
            BackColor = Color.FromArgb(32, 32, 32);
            ForeColor = Color.White;

            var title = new Label
            {
                Text = "Select vehicle",
                Font = new Font(Font.FontFamily, 12f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(20, 18)
            };

            var btn2 = MakeStageButton("Stage 2", 56, FlightOperationStage.Stage2);
            var btn3 = MakeStageButton("Stage 3", 104, FlightOperationStage.Stage3);

            Controls.Add(title);
            Controls.Add(btn2);
            Controls.Add(btn3);
        }

        Button MakeStageButton(string caption, int top, FlightOperationStage stage)
        {
            var btn = new Button
            {
                Text = caption,
                Location = new Point(20, top),
                Size = new Size(320, 40),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(55, 55, 60),
                Font = new Font(Font.FontFamily, 11f, FontStyle.Bold)
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(90, 90, 100);
            btn.Click += (s, e) =>
            {
                FlightPreflightSession.SetStage(stage);
                Settings.Instance["preflight_last_stage"] = ((int)stage).ToString();
                DialogResult = DialogResult.OK;
                Close();
            };
            return btn;
        }
    }
}
