using System;
using System.Windows.Forms;
using GMap.NET;
using GMap.NET.WindowsForms;
using MissionPlanner.Utilities.AviationLayers;

using MissionPlanner.Utilities.AviationLayers;

namespace MissionPlanner.Utilities
{
    /// <summary>
    /// Coordinates Flight Data map aviation layers (weather tiles, NoFly, and FlightMapLayerManager).
    /// </summary>
    public sealed class MapOverlayController : IDisposable
    {
        static MapOverlayController()
        {
            AviationMapBridge.ApplyLayerToMap = AviationGMapApply.ApplyLayer;
        }
        readonly FlightMapLayerManager _manager;
        readonly Func<FlightMapVisibility> _getVisibility;
        GMapControl _map;

        public MapOverlayController(
            FlightMapLayerStack stack,
            FlightMapLayerContext context,
            Func<FlightMapVisibility> getVisibility)
        {
            _getVisibility = getVisibility ?? throw new ArgumentNullException(nameof(getVisibility));
            _manager = new FlightMapLayerManager(stack, context);
        }

        public FlightMapLayerManager Manager => _manager;

        public void Attach(GMapControl map)
        {
            _map = map;
            _manager.Attach(map);
        }

        public void TryMarkMapReady() => _manager.TryMarkMapReady();

        public void ApplyAllSettings()
        {
            var vis = _getVisibility();
            _manager.ApplyAllSettings(vis);

            if (_map != null)
            {
                NoFly.NoFly.SetZonesVisible(MainV2.ShowNoFly);
                MapOverlayHelper.ApplyWeatherRadar(_map, MainV2.ShowWeather);
                GCSViews.FlightData.SyncFlightMapZoomLimits();
            }

            if (GCSViews.FlightPlanner.instance?.MainMap != null)
                MapOverlayHelper.ApplyWeatherRadar(GCSViews.FlightPlanner.instance.MainMap, MainV2.ShowWeather);
        }

        public void ScheduleRestrictionRefresh(bool forceImmediate = false) =>
            _manager.ScheduleRefresh(forceImmediate);

        public void Dispose() => _manager.Dispose();
    }
}
