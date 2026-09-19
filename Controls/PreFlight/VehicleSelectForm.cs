using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MissionPlanner.Utilities;

namespace MissionPlanner.Controls.PreFlight
{
    /// <summary>
    /// Startup picker: which aircraft is flying this session (drives per-vehicle requirements).
    /// </summary>
    public sealed class VehicleSelectForm : Form
    {
        readonly ComboBox _cmbVehicles;
        readonly TextBox _txtCustom;

        public string SelectedVehicle { get; private set; }

        public VehicleSelectForm()
        {
            Text = "Select vehicle";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(420, 210);
            BackColor = Color.FromArgb(32, 32, 32);
            ForeColor = Color.White;

            var title = new Label
            {
                Text = "Which vehicle are you flying?",
                Font = new Font(Font.FontFamily, 11f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(16, 14)
            };

            var hint = new Label
            {
                Text = "Requirements and checklists are keyed to this aircraft.\r\nEdit vfsVehicles.txt next to the app to change the list.",
                Location = new Point(16, 42),
                Size = new Size(388, 36),
                ForeColor = Color.LightGray
            };

            _cmbVehicles = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(16, 88),
                Width = 388,
                BackColor = Color.FromArgb(48, 48, 54),
                ForeColor = Color.White
            };

            foreach (var v in FlightVehicleCatalog.GetVehicles())
                _cmbVehicles.Items.Add(v);

            var last = FlightVehicleCatalog.GetSelectedVehicle();
            if (!string.IsNullOrEmpty(last))
            {
                var idx = _cmbVehicles.Items.Cast<object>()
                    .Select((o, i) => new { o, i })
                    .FirstOrDefault(x => string.Equals(x.o.ToString(), last, StringComparison.OrdinalIgnoreCase));
                if (idx != null)
                    _cmbVehicles.SelectedIndex = idx.i;
            }

            if (_cmbVehicles.SelectedIndex < 0 && _cmbVehicles.Items.Count > 0)
                _cmbVehicles.SelectedIndex = 0;

            var lblCustom = new Label
            {
                Text = "Or type a new vehicle name:",
                AutoSize = true,
                Location = new Point(16, 122),
                ForeColor = Color.LightGray
            };

            _txtCustom = new TextBox
            {
                Location = new Point(16, 142),
                Width = 388,
                BackColor = Color.FromArgb(48, 48, 54),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            var btnOk = new Button
            {
                Text = "Continue",
                Location = new Point(314, 172),
                Size = new Size(90, 28)
            };
            btnOk.Click += (s, e) => Confirm();

            var btnCancel = new Button
            {
                Text = "Skip",
                DialogResult = DialogResult.Cancel,
                Location = new Point(218, 172),
                Size = new Size(88, 28)
            };

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Controls.Add(title);
            Controls.Add(hint);
            Controls.Add(_cmbVehicles);
            Controls.Add(lblCustom);
            Controls.Add(_txtCustom);
            Controls.Add(btnCancel);
            Controls.Add(btnOk);
        }

        void Confirm()
        {
            var name = _txtCustom.Text?.Trim();
            if (string.IsNullOrEmpty(name))
                name = _cmbVehicles.SelectedItem?.ToString()?.Trim();

            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show(this, "Select or enter a vehicle name.", Text, MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            SelectedVehicle = name;
            FlightVehicleCatalog.SetSelectedVehicle(name);
            DialogResult = DialogResult.OK;
            Close();
        }

        /// <summary>Show Stage 2 / Stage 3 picker (startup flow).</summary>
        public static void PromptStartupFlow(IWin32Window owner)
        {
            if (FlightPreflightSession.Stage != FlightOperationStage.Unselected)
                return;

            using (var stageDlg = new FlightStageMenuForm())
                stageDlg.ShowDialog(owner);
        }
    }
}
