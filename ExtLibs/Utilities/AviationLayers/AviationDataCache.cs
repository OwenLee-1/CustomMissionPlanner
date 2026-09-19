using System;
using System.Threading;

namespace MissionPlanner.Utilities.AviationLayers
{
    public sealed class AviationDataCache<T>
    {
        readonly object _lock = new object();
        T _value;
        DateTime _fetchedUtc = DateTime.MinValue;
        string _cacheKey = "";

        public TimeSpan TimeToLive { get; set; } = TimeSpan.FromMinutes(5);

        public bool TryGet(string key, out T value, out DateTime fetchedUtc)
        {
            lock (_lock)
            {
                if (_value != null && _cacheKey == key &&
                    DateTime.UtcNow - _fetchedUtc < TimeToLive)
                {
                    value = _value;
                    fetchedUtc = _fetchedUtc;
                    return true;
                }
            }

            value = default;
            fetchedUtc = DateTime.MinValue;
            return false;
        }

        public void Set(string key, T value)
        {
            lock (_lock)
            {
                _cacheKey = key ?? "";
                _value = value;
                _fetchedUtc = DateTime.UtcNow;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _value = default;
                _cacheKey = "";
                _fetchedUtc = DateTime.MinValue;
            }
        }

        public static string BoundsKey(double left, double bottom, double right, double top, int precision = 3)
        {
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{0:F3},{1:F3},{2:F3},{3:F3}",
                left, bottom, right, top);
        }
    }
}
