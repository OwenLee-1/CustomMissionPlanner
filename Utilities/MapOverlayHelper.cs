using System;
using System.Windows.Forms;
using GMap.NET.MapProviders;
using GMap.NET.WindowsForms;
using MissionPlanner.Maps;

namespace MissionPlanner.Utilities
{
    public static class MapOverlayHelper
    {
        public static void ApplyWeatherRadar(GMapControl map, bool enabled)
        {
            if (map == null)
                return;

            var resolved = MapProviderWithTileOverlay.Resolve(map.MapProvider, enabled);
            if (map.MapProvider != resolved)
            {
                map.MapProvider = resolved;
                SafeReloadMap(map);
            }
            else if (map.MapProvider is MapProviderWithTileOverlay overlay)
            {
                overlay.WeatherRadarEnabled = enabled;
                if (enabled)
                    RainViewerRadarProvider.EnsureRadarPath();
                SafeReloadMap(map);
            }

            if (enabled)
            {
                System.Threading.Tasks.Task.Run(() => RainViewerRadarProvider.EnsureRadarPath())
                    .ContinueWith(_ =>
                    {
                        try
                        {
                            if (map.IsDisposed)
                                return;
                            if (map.InvokeRequired)
                                map.BeginInvoke((Action)(() => map.Invalidate()));
                            else
                                map.Invalidate();
                        }
                        catch
                        {
                        }
                    });
            }
        }

        static void SafeReloadMap(GMapControl map)
        {
            if (map?.Core != null && map.Core.IsStarted)
                map.ReloadMap();
        }

        /// <summary>
        /// Use when the user picks a new base map type from the map dropdown.
        /// </summary>
        public static GMapProvider ResolveMapProvider(GMapProvider selected, bool weatherRadar)
        {
            return MapProviderWithTileOverlay.Resolve(selected, weatherRadar);
        }
    }
}
