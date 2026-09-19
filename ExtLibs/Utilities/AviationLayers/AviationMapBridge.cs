using System;

namespace MissionPlanner.Utilities.AviationLayers
{
    /// <summary>
    /// GMap polygon/marker apply runs in MissionPlanner.exe (net472) to avoid GMap.NET type mismatch with netstandard2.0 Utilities.
    /// </summary>
    public static class AviationMapBridge
    {
        public static Action<IFlightMapLayer> ApplyLayerToMap { get; set; }
    }
}
