using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MissionPlanner.Utilities
{
    /// <summary>
    /// Named aircraft used for per-vehicle preflight requirements.
    /// Edit <c>vfsVehicles.txt</c> next to the exe (one name per line), or add names in the picker.
    /// </summary>
    public static class FlightVehicleCatalog
    {
        public const string SettingsKey = "preflight_vehicle_profile";
        public const string CustomListKey = "preflight_vehicle_list";

        static readonly string[] BuiltInDefaults =
        {
            "VFS Copter 1",
            "VFS Copter 2",
            "VFS Fixed Wing 1",
            "Training Quad"
        };

        public static IReadOnlyList<string> GetVehicles()
        {
            var list = new List<string>();

            var file = Path.Combine(Settings.GetRunningDirectory(), "vfsVehicles.txt");
            if (File.Exists(file))
            {
                foreach (var line in File.ReadAllLines(file))
                {
                    var name = line?.Trim();
                    if (!string.IsNullOrEmpty(name) && !name.StartsWith("#") &&
                        !list.Contains(name, StringComparer.OrdinalIgnoreCase))
                        list.Add(name);
                }
            }

            foreach (var custom in Settings.Instance.GetList(CustomListKey) ?? new List<string>())
            {
                var name = custom?.Trim();
                if (!string.IsNullOrEmpty(name) && !list.Contains(name, StringComparer.OrdinalIgnoreCase))
                    list.Add(name);
            }

            if (list.Count == 0)
                list.AddRange(BuiltInDefaults);

            return list;
        }

        public static void RememberCustomVehicle(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;

            name = name.Trim();
            var existing = Settings.Instance.GetList(CustomListKey)?.ToList() ?? new List<string>();
            if (!existing.Any(v => string.Equals(v, name, StringComparison.OrdinalIgnoreCase)))
            {
                existing.Add(name);
                Settings.Instance.SetList(CustomListKey, existing);
            }
        }

        public static string GetSelectedVehicle()
        {
            if (!string.IsNullOrWhiteSpace(FlightPreflightSession.SelectedVehicle))
                return FlightPreflightSession.SelectedVehicle;

            if (Settings.Instance.ContainsKey(SettingsKey))
            {
                var saved = Settings.Instance[SettingsKey]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(saved))
                    return saved;
            }

            return null;
        }

        public static void SetSelectedVehicle(string name)
        {
            name = name?.Trim();
            if (string.IsNullOrEmpty(name))
                return;

            FlightPreflightSession.SelectedVehicle = name;
            Settings.Instance[SettingsKey] = name;
            RememberCustomVehicle(name);
        }
    }
}
