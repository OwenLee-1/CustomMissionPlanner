using System;
using System.Windows.Forms;
using GMap.NET;
using GMap.NET.WindowsForms;
using MissionPlanner.GCSViews;

namespace MissionPlanner.Utilities
{
    /// <summary>
    /// Single coordinator for Flight Data map overlays (weather tiles + restriction layers).
    /// Waits until GMap is started before ReloadMap; debounces viewport fetches.
    /// </summary>
    public sealed class MapOverlayController : IDisposable
    {
        readonly FlightRestrictionsOverlay.OverlaySet _overlays;
        readonly Func<FlightRestrictionsOverlay.RefreshOptions> _buildRefreshOptions;
        readonly Timer _refreshDebounce;

        GMapControl _map;
        bool _mapReady;
        bool _disposed;

        public MapOverlayController(
            FlightRestrictionsOverlay.OverlaySet overlays,
            Func<FlightRestrictionsOverlay.RefreshOptions> buildRefreshOptions)
        {
            _overlays = overlays ?? throw new ArgumentNullException(nameof(overlays));
            _buildRefreshOptions = buildRefreshOptions ?? throw new ArgumentNullException(nameof(buildRefreshOptions));

            _refreshDebounce = new Timer { Interval = 650 };
            _refreshDebounce.Tick += (s, e) =>
            {
                _refreshDebounce.Stop();
                RefreshRestrictionLayersNow();
            };
        }

        public void Attach(GMapControl map)
        {
            if (map == null)
                return;

            DetachMapHandlers();
            _map = map;
            _map.OnMapZoomChanged += Map_ViewChanged;
            _map.OnPositionChanged += Map_PositionChanged;
            _map.Load += Map_Load;
        }

        void Map_Load(object sender, EventArgs e)
        {
            TryMarkMapReady();
        }

        void Map_ViewChanged()
        {
            ScheduleRestrictionRefresh();
        }

        void Map_PositionChanged(PointLatLng point)
        {
            ScheduleRestrictionRefresh();
        }

        /// <summary>Call after the Flight Data form is shown if Load already fired.</summary>
        public void TryMarkMapReady()
        {
            if (_mapReady || _map == null)
                return;

            if (_map.Core != null && _map.Core.IsStarted)
            {
                _mapReady = true;
                ApplyAllSettings();
            }
        }

        public void ApplyAllSettings()
        {
            if (_map == null)
                return;

            FlightRestrictionsOverlay.SetOverlayVisibility(_overlays,
                MainV2.ShowTFR,
                MainV2.ShowAirspace,
                MainV2.ShowUasFacilityMap,
                MainV2.ShowNotams,
                MainV2.ShowSpecialUseAirspace);

            NoFly.NoFly.SetZonesVisible(MainV2.ShowNoFly);

            MapOverlayHelper.ApplyWeatherRadar(_map, MainV2.ShowWeather);

            if (FlightPlanner.instance?.MainMap != null)
                MapOverlayHelper.ApplyWeatherRadar(FlightPlanner.instance.MainMap, MainV2.ShowWeather);

            if (_mapReady)
                ScheduleRestrictionRefresh(forceImmediate: true);
        }

        public void ScheduleRestrictionRefresh(bool forceImmediate = false)
        {
            if (!_mapReady || _map?.ViewArea == null || _map.ViewArea.IsEmpty)
                return;

            if (forceImmediate)
            {
                _refreshDebounce.Stop();
                RefreshRestrictionLayersNow();
                return;
            }

            _refreshDebounce.Stop();
            _refreshDebounce.Start();
        }

        void RefreshRestrictionLayersNow()
        {
            if (_map?.ViewArea == null || _map.ViewArea.IsEmpty)
                return;

            var anyLayer = MainV2.ShowTFR || MainV2.ShowAirspace || MainV2.ShowUasFacilityMap ||
                           MainV2.ShowNotams || MainV2.ShowSpecialUseAirspace;

            if (!anyLayer)
            {
                FlightRestrictionsOverlay.SetOverlayVisibility(_overlays, false, false, false, false, false);
                return;
            }

            var options = _buildRefreshOptions();
            if (options == null)
                return;

            FlightRestrictionsOverlay.RequestRefresh(_map.ViewArea, _overlays, options);
        }

        void DetachMapHandlers()
        {
            if (_map == null)
                return;

            _map.OnMapZoomChanged -= Map_ViewChanged;
            _map.OnPositionChanged -= Map_PositionChanged;
            _map.Load -= Map_Load;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _refreshDebounce?.Stop();
            _refreshDebounce?.Dispose();
            DetachMapHandlers();
            _map = null;
        }
    }
}
