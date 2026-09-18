using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace MissionPlanner.Utilities
{
    /// <summary>
    /// Saves crash/exception reports locally and optionally POSTs to a configured URL.
    /// </summary>
    public static class ErrorReporter
    {
        const int MaxSavedReports = 50;

        public static string ReportsDirectory =>
            Path.Combine(Settings.GetUserDataDirectory(), "ErrorReports");

        public static bool EnableGoogleAnalytics =>
            string.Equals(
                ConfigurationManager.AppSettings["ErrorReportEnableGoogleAnalytics"],
                "true",
                StringComparison.OrdinalIgnoreCase);

        static string SubmitUrl =>
            ConfigurationManager.AppSettings["ErrorReportSubmitUrl"]?.Trim();

        static bool UseLegacyOborneEndpoint =>
            string.Equals(
                ConfigurationManager.AppSettings["ErrorReportUseLegacyOborneEndpoint"],
                "true",
                StringComparison.OrdinalIgnoreCase);

        public static string SaveReport(Exception ex, string userMessage, string processInfo)
        {
            Directory.CreateDirectory(ReportsDirectory);
            EnsureReadme();

            var stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
            var id = Guid.NewGuid().ToString("N").Substring(0, 8);
            var fileName = "error_" + stamp + "_" + id + ".txt";
            var path = Path.Combine(ReportsDirectory, fileName);

            var body = BuildReportText(ex, userMessage, processInfo);
            File.WriteAllText(path, body, Encoding.UTF8);

            var indexPath = Path.Combine(ReportsDirectory, "_index.log");
            File.AppendAllText(
                indexPath,
                DateTime.Now.ToString("u") + " | " + fileName + " | " + ex.GetType().Name + ": " +
                ex.Message + Environment.NewLine,
                Encoding.UTF8);

            TrimOldReports();
            return path;
        }

        public static string BuildReportText(Exception ex, string userMessage, string processInfo)
        {
            var asm = Assembly.GetExecutingAssembly().GetName();
            var sb = new StringBuilder();
            sb.AppendLine("=== Mission Planner / TitanPlanner Error Report ===");
            sb.AppendLine("Time (local): " + DateTime.Now.ToString("O"));
            sb.AppendLine("OS: " + Environment.OSVersion);
            sb.AppendLine("Assembly: " + asm.Name + " " + asm.Version);
            sb.AppendLine("Product version: " + Application.ProductVersion);
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(userMessage))
            {
                sb.AppendLine("User message:");
                sb.AppendLine(userMessage);
                sb.AppendLine();
            }

            sb.AppendLine("Exception type: " + ex.GetType().FullName);
            sb.AppendLine("Message: " + ex.Message);
            sb.AppendLine();
            sb.AppendLine("Stack trace:");
            sb.AppendLine(ex.StackTrace ?? "(none)");
            sb.AppendLine();

            if (ex.InnerException != null)
            {
                sb.AppendLine("Inner exception:");
                sb.AppendLine(ex.InnerException.ToString());
                sb.AppendLine();
            }

            if (ex.Data != null && ex.Data.Count > 0)
            {
                sb.AppendLine("Exception data:");
                foreach (System.Collections.DictionaryEntry de in ex.Data)
                    sb.AppendLine("  " + de.Key + ": " + de.Value);
                sb.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(processInfo))
            {
                sb.AppendLine("Thread stacks:");
                sb.AppendLine(processInfo);
            }

            return sb.ToString();
        }

        /// <summary>
        /// POST report body to ErrorReportSubmitUrl, or legacy Mission Planner endpoint if enabled.
        /// </summary>
        public static bool TrySubmitRemote(string reportBody, out string error)
        {
            error = null;

            if (!string.IsNullOrEmpty(SubmitUrl))
            {
                try
                {
                    Download.PostAsync(SubmitUrl, reportBody).GetAwaiter().GetResult();
                    return true;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    return false;
                }
            }

            if (UseLegacyOborneEndpoint)
            {
                try
                {
                    Download.PostAsync("http://vps.oborne.me/mail.php", reportBody).GetAwaiter().GetResult();
                    return true;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    return false;
                }
            }

            error = "No remote endpoint configured (set ErrorReportSubmitUrl in app.config).";
            return false;
        }

        public static void OpenReportsFolder()
        {
            Directory.CreateDirectory(ReportsDirectory);
            Process.Start("explorer.exe", ReportsDirectory);
        }

        static void EnsureReadme()
        {
            var readme = Path.Combine(ReportsDirectory, "README.txt");
            if (File.Exists(readme))
                return;

            File.WriteAllText(readme,
                "Each error_*.txt file is a full exception report from the app." + Environment.NewLine +
                "_index.log lists reports newest at the bottom." + Environment.NewLine +
                Environment.NewLine +
                "To send reports to your own server, set ErrorReportSubmitUrl in app.config" +
                " (next to MissionPlanner.exe) to a URL that accepts POST text." + Environment.NewLine,
                Encoding.UTF8);
        }

        static void TrimOldReports()
        {
            try
            {
                var files = Directory.GetFiles(ReportsDirectory, "error_*.txt")
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .Skip(MaxSavedReports)
                    .ToList();
                foreach (var f in files)
                    File.Delete(f);
            }
            catch
            {
            }
        }
    }
}
