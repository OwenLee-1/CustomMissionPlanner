using System;
using System.Collections.Generic;
using System.Linq;
using GMap.NET;
using MissionPlanner.ArduPilot;
using MissionPlanner.GCSViews;

namespace MissionPlanner.Utilities
{
    public sealed class PreflightCheckOutcome
    {
        public bool Ok { get; set; }
        public string Summary { get; set; }
        public string Detail { get; set; }
    }

    public static class PreflightTelemetryChecks
    {
        public static PreflightCheckOutcome EvaluateSensors(CurrentState cs, FlightStageRequirements req)
        {
            if (cs == null || !cs.connected)
                return Fail("Not connected", "Connect the vehicle to verify sensors.");

            var issues = new List<string>();

            if (cs.sensors_present.gps && cs.sensors_enabled.gps && !cs.sensors_health.gps)
                issues.Add("GPS unhealthy");
            if (cs.sensors_present.gyro && cs.sensors_enabled.gyro && !cs.sensors_health.gyro)
                issues.Add("Gyro unhealthy");
            if (cs.sensors_present.accelerometer && cs.sensors_enabled.accelerometer && !cs.sensors_health.accelerometer)
                issues.Add("Accelerometer unhealthy");
            if (cs.sensors_present.compass && cs.sensors_enabled.compass && !cs.sensors_health.compass)
                issues.Add("Compass unhealthy");
            if (cs.sensors_present.barometer && cs.sensors_enabled.barometer && !cs.sensors_health.barometer)
                issues.Add("Baro unhealthy");
            if (cs.sensors_present.battery && cs.sensors_enabled.battery && !cs.sensors_health.battery)
                issues.Add("Battery monitor unhealthy");
            if (cs.sensors_present.rc_receiver && cs.sensors_enabled.rc_receiver && !cs.sensors_health.rc_receiver)
                issues.Add("RC receiver unhealthy");

            if (cs.gpsstatus < 3)
                issues.Add("GPS fix type " + cs.gpsstatus);
            if (cs.satcount < req.MinGpsSats)
                issues.Add("Sats " + cs.satcount + " (need ≥" + req.MinGpsSats + ")");
            if (cs.gpshdop <= 0 || cs.gpshdop > req.MaxHdop)
                issues.Add("HDOP " + cs.gpshdop.ToString("0.0"));

            if (cs.ekfstatus >= 1)
                issues.Add("EKF variance high");

            if (issues.Count == 0)
                return Ok("All reported sensors healthy", "Fix " + cs.gpsstatus + " · " + cs.satcount + " sats · HDOP " + cs.gpshdop.ToString("0.0"));

            return Fail("Sensor issues: " + string.Join(", ", issues.Take(4)), string.Join("; ", issues));
        }

        public static PreflightCheckOutcome EvaluateRadio(CurrentState cs, FlightStageRequirements req)
        {
            if (cs == null || !cs.connected)
                return Fail("Not connected", "No telemetry link.");

            var link = cs.linkqualitygcs;
            var rssi = cs.rxrssi;
            var rc = cs.ch3in;

            if (link < req.MinLinkQualityPercent)
                return Fail("Link " + link + "% (need ≥" + req.MinLinkQualityPercent + "%)",
                    "GCS link quality " + link + "% · RX RSSI " + rssi);

            var rcNote = cs.sensors_present.rc_receiver ? " · RC in " + rc.ToString("0") : "";
            return Ok("Radio nominal · link " + link + "%", "RX RSSI " + rssi + rcNote);
        }

        public static PreflightCheckOutcome EvaluateAdsbTraffic(
            PointLatLngAlt centerAlt,
            FlightStageRequirements req,
            bool adsbEnabled)
        {
            var center = centerAlt == PointLatLngAlt.Zero ? PointLatLng.Empty : (PointLatLng)centerAlt;
            if (center.IsEmpty)
                return Ok("ADS-B skipped", "No home position for traffic scan.");

            if (!adsbEnabled)
            {
                if (req.RequireAdsbTrafficReview)
                    return Fail("ADS-B not enabled", "Enable ADS-B in settings to review nearby traffic.");
                return Ok("ADS-B off", "Traffic scan disabled.");
            }

            var planes = MainV2.instance?.adsbPlanes;
            if (planes == null || planes.Count == 0)
                return Ok("No ADS-B traffic in feed", "0 aircraft in GCS ADS-B cache.");

            var proj = FlightData.instance?.gMapControl1?.MapProvider?.Projection;
            if (proj == null)
                return Ok(planes.Count + " aircraft in feed", "Enable map for distance filtering.");

            var radiusKm = req.AdsbReviewRadiusNm * 1.852;
            var nearby = planes.Values.Where(p =>
            {
                if (p == null || (p.Lat == 0 && p.Lng == 0))
                    return false;
                return proj.GetDistance(center, p) <= radiusKm;
            }).Count();

            if (nearby >= req.AdsbWarnTrafficCount)
                return Fail(nearby + " aircraft within " + req.AdsbReviewRadiusNm.ToString("0") + " NM",
                    "Review ADS-B map overlay before flight.");

            return Ok(nearby + " aircraft within " + req.AdsbReviewRadiusNm.ToString("0") + " NM",
                planes.Count + " total in ADS-B feed");
        }

        public static string BuildMissionFingerprint()
        {
            var points = FlightPlanner.instance?.pointlist;
            if (points == null || points.Count == 0)
                return "empty";

            unchecked
            {
                var hash = 17;
                hash = hash * 31 + points.Count;
                foreach (var p in points)
                {
                    hash = hash * 31 + p.Lat.GetHashCode();
                    hash = hash * 31 + p.Lng.GetHashCode();
                }

                return hash.ToString("X");
            }
        }

        static PreflightCheckOutcome Ok(string summary, string detail) =>
            new PreflightCheckOutcome { Ok = true, Summary = summary, Detail = detail };

        static PreflightCheckOutcome Fail(string summary, string detail) =>
            new PreflightCheckOutcome { Ok = false, Summary = summary, Detail = detail };
    }
}
