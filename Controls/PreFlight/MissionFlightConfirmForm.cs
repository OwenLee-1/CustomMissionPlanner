using System;
using System.Drawing;
using System.Windows.Forms;
using GMap.NET;
using MissionPlanner.GCSViews;
using MissionPlanner.Utilities;

namespace MissionPlanner.Controls.PreFlight
{
    /// <summary>
    /// Mission summary and PIC/GCO confirmation before arming.
    /// </summary>
    public sealed class MissionFlightConfirmForm : Form
    {
        readonly TextBox _txtSummary;
        readonly CheckBox _chkPic;
        readonly CheckBox _chkGco;
        readonly FlightStageRequirements _req;

        public MissionFlightConfirmForm(FlightStageRequirements req)
        {
            _req = req ?? new FlightStageRequirements();

            Text = "Mission confirmation";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(520, 380);
            BackColor = Color.FromArgb(32, 32, 32);
            ForeColor = Color.White;

            var lbl = new Label
            {
                Text = "Review mission and confirm roles before flight",
                Font = new Font(Font.FontFamily, 10f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(14, 12)
            };

            _txtSummary = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(14, 40),
                Size = new Size(492, 220),
                BackColor = Color.FromArgb(48, 48, 54),
                ForeColor = Color.WhiteSmoke,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9f)
            };
            _txtSummary.Text = BuildSummaryText();

            _chkPic = new CheckBox
            {
                Text = "PIC confirms mission, airworthiness, and operational readiness",
                Location = new Point(14, 272),
                AutoSize = true,
                ForeColor = Color.White,
                Enabled = _req.RequirePicSignOff
            };

            _chkGco = new CheckBox
            {
                Text = "GCO confirms comms plan, airspace briefing, and ground safety",
                Location = new Point(14, 298),
                AutoSize = true,
                ForeColor = Color.White,
                Enabled = _req.RequireGcoSignOff
            };

            var btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(320, 332),
                Size = new Size(88, 28)
            };

            var btnOk = new Button
            {
                Text = "Confirm flight",
                Location = new Point(418, 332),
                Size = new Size(88, 28)
            };
            btnOk.Click += (s, e) =>
            {
                if (_req.RequirePicSignOff && !_chkPic.Checked)
                {
                    MessageBox.Show(this, "PIC confirmation is required.", Text, MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                if (_req.RequireGcoSignOff && !_chkGco.Checked)
                {
                    MessageBox.Show(this, "GCO confirmation is required.", Text, MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                var fp = PreflightTelemetryChecks.BuildMissionFingerprint();
                FlightPreflightSession.RecordMissionConfirmation(fp, _chkPic.Checked, _chkGco.Checked);
                DialogResult = DialogResult.OK;
                Close();
            };

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Controls.Add(lbl);
            Controls.Add(_txtSummary);
            Controls.Add(_chkPic);
            Controls.Add(_chkGco);
            Controls.Add(btnCancel);
            Controls.Add(btnOk);
        }

        string BuildSummaryText()
        {
            var est = MissionFlightEstimate.Compute();
            var cs = MainV2.comPort?.MAV?.cs;
            var sensors = PreflightTelemetryChecks.EvaluateSensors(cs, _req);
            var radio = PreflightTelemetryChecks.EvaluateRadio(cs, _req);
            var homePt = cs?.Base != PointLatLngAlt.Zero ? cs.Base : (PointLatLngAlt)(cs?.Location ?? PointLatLngAlt.Zero);
            var adsb = PreflightTelemetryChecks.EvaluateAdsbTraffic(homePt, _req, MainV2.instance?.EnableADSB == true);

            var lines = new System.Text.StringBuilder();
            lines.AppendLine("Stage: " + FlightPreflightSession.Stage);
            lines.AppendLine("Vehicle profile: " + FlightPreflightProfiles.GetVehicleProfileKey());
            lines.AppendLine();
            lines.AppendLine("Mission");
            lines.AppendLine("  " + est.Summary);
            lines.AppendLine();
            lines.AppendLine("Sensors: " + (sensors.Ok ? "OK" : "CHECK") + " — " + sensors.Summary);
            lines.AppendLine("Radio:   " + (radio.Ok ? "OK" : "CHECK") + " — " + radio.Summary);
            lines.AppendLine("ADS-B:   " + (adsb.Ok ? "OK" : "CHECK") + " — " + adsb.Summary);
            lines.AppendLine();
            lines.AppendLine("UAS Facility Map altitudes do not authorize flight.");
            lines.AppendLine("Controlled airspace requires LAANC or FAA DroneZone authorization.");
            return lines.ToString();
        }

        public static bool PromptIfNeeded(IWin32Window owner)
        {
            if (FlightPreflightSession.Stage == FlightOperationStage.Unselected)
                return false;

            var req = FlightPreflightProfiles.GetRequirements(FlightPreflightSession.Stage);
            if (!req.RequireMissionConfirm)
                return true;

            var fp = PreflightTelemetryChecks.BuildMissionFingerprint();
            if (FlightPreflightSession.IsMissionConfirmationCurrent(fp, req.RequirePicSignOff, req.RequireGcoSignOff))
                return true;

            using (var dlg = new MissionFlightConfirmForm(req))
                return dlg.ShowDialog(owner) == DialogResult.OK;
        }
    }
}
