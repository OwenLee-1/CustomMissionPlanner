using System;

namespace MissionPlanner.Utilities.AviationLayers
{
    public static class AviationLayerErrors
    {
        /// <summary>True for user pan/zoom superseding an in-flight HTTP request (Flurl wraps these).</summary>
        public static bool IsBenignCancel(Exception ex)
        {
            for (var e = ex; e != null; e = e.InnerException)
            {
                if (e is OperationCanceledException)
                    return true;
            }

            var msg = ex?.Message ?? "";
            return msg.IndexOf("task was canceled", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   msg.IndexOf("operation was canceled", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static string Shorten(Exception ex)
        {
            if (ex == null)
                return "Unknown error";

            var msg = ex.Message ?? ex.GetType().Name;
            if (ex.InnerException != null && msg.Length < 40)
                msg = ex.InnerException.Message ?? msg;

            if (msg.IndexOf("could not be resolved", StringComparison.OrdinalIgnoreCase) >= 0 ||
                msg.IndexOf("No such host", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Server not reachable (DNS/network)";

            if (msg.IndexOf("An error occurred while sending the request", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Network request failed (check internet / firewall)";

            if (msg.Length > 100)
                return msg.Substring(0, 97) + "...";

            return msg;
        }
    }
}
