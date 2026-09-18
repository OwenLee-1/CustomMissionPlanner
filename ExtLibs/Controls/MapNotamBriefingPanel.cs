using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MissionPlanner.Utilities;

namespace MissionPlanner.Controls
{
    /// <summary>
    /// Side panel listing NOTAM/TFR and restriction counts for the current map view.
    /// </summary>
    public class MapNotamBriefingPanel : UserControl
    {
        private readonly Label _lblHeader;
        private readonly Label _lblSummary;
        private readonly ListView _list;
        private readonly Button _btnZoom;
        private readonly Button _btnOpen;
        private List<NotamBriefingItem> _items = new List<NotamBriefingItem>();

        public event Action<NotamBriefingItem> ZoomToItem;
        public event Action<NotamBriefingItem> OpenDetail;

        public MapNotamBriefingPanel()
        {
            BackColor = Color.FromArgb(245, 245, 245);
            MinimumSize = new Size(240, 120);

            _lblHeader = new Label
            {
                Dock = DockStyle.Top,
                Height = 22,
                Text = "NOTAM / TFR briefing",
                Font = new Font(Font.FontFamily, 9f, FontStyle.Bold),
                Padding = new Padding(6, 4, 4, 0)
            };

            _lblSummary = new Label
            {
                Dock = DockStyle.Top,
                Height = 36,
                AutoSize = false,
                Text = "Pan or zoom the map to load restrictions in view.",
                ForeColor = Color.DimGray,
                Padding = new Padding(6, 0, 6, 4)
            };

            var buttonRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 32,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(4, 2, 4, 4)
            };

            _btnZoom = new Button
            {
                Text = "Zoom to",
                AutoSize = true,
                Enabled = false
            };
            _btnZoom.Click += (s, e) => InvokeSelected(ZoomToItem);

            _btnOpen = new Button
            {
                Text = "FAA detail",
                AutoSize = true,
                Enabled = false
            };
            _btnOpen.Click += (s, e) =>
            {
                var item = SelectedItem();
                if (item == null)
                    return;
                OpenDetail?.Invoke(item);
                if (!string.IsNullOrEmpty(item.DetailUrl))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(item.DetailUrl) { UseShellExecute = true });
                    }
                    catch
                    {
                    }
                }
            };

            buttonRow.Controls.Add(_btnZoom);
            buttonRow.Controls.Add(_btnOpen);

            _list = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                HideSelection = false,
                MultiSelect = false
            };
            _list.Columns.Add("ID", 72);
            _list.Columns.Add("Type", 64);
            _list.Columns.Add("Description", 180);
            _list.SelectedIndexChanged += (s, e) => UpdateButtons();
            _list.DoubleClick += (s, e) => InvokeSelected(ZoomToItem);

            Controls.Add(_list);
            Controls.Add(buttonRow);
            Controls.Add(_lblSummary);
            Controls.Add(_lblHeader);
        }

        public void SetBriefingItems(IList<NotamBriefingItem> items)
        {
            _items = items?.ToList() ?? new List<NotamBriefingItem>();
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var item in _items)
            {
                var row = new ListViewItem(item.DisplayId);
                row.SubItems.Add(item.Type);
                row.SubItems.Add(Truncate(item.Title, 120));
                row.Tag = item;
                _list.Items.Add(row);
            }

            _list.EndUpdate();
            _lblHeader.Text = _items.Count == 0
                ? "NOTAM / TFR briefing"
                : $"NOTAM / TFR briefing ({_items.Count})";
            UpdateButtons();
        }

        public void SetSummary(FlightRestrictionsOverlay.RestrictionBriefingSummary summary)
        {
            if (summary == null)
                return;

            var parts = new List<string>();
            if (summary.TfrNotamCount > 0)
                parts.Add($"{summary.TfrNotamCount} TFR/NOTAM in view");
            if (summary.UasGridCells > 0)
                parts.Add($"{summary.UasGridCells} LAANC grid cells");
            if (summary.SpecialUseAreas > 0)
                parts.Add($"{summary.SpecialUseAreas} special-use areas");

            _lblSummary.Text = parts.Count == 0
                ? "No FAA graphic restrictions in the current map area."
                : string.Join(" · ", parts);
        }

        public void ClearBriefing()
        {
            SetBriefingItems(new List<NotamBriefingItem>());
            _lblSummary.Text = "Enable TFRs or NOTAMs to refresh this list.";
        }

        private void UpdateButtons()
        {
            var hasSelection = _list.SelectedItems.Count > 0;
            _btnZoom.Enabled = hasSelection;
            _btnOpen.Enabled = hasSelection && SelectedItem()?.DetailUrl != null;
        }

        private void InvokeSelected(Action<NotamBriefingItem> handler)
        {
            var item = SelectedItem();
            if (item == null)
                return;
            handler?.Invoke(item);
        }

        private NotamBriefingItem SelectedItem()
        {
            if (_list.SelectedItems.Count == 0)
                return null;
            return _list.SelectedItems[0].Tag as NotamBriefingItem;
        }

        private static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max)
                return text;
            return text.Substring(0, max - 1) + "…";
        }
    }
}
