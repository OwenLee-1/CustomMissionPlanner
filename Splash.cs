using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using MissionPlanner.Utilities;

namespace MissionPlanner
{
    public partial class Splash : Form
    {
        public Splash()
        {
            InitializeComponent();

            string strVersion = File.Exists("version.txt")
                ? File.ReadAllText("version.txt").Trim()
                : Assembly.GetExecutingAssembly().GetName().Version.ToString();

            TXT_version.Text = "Version: VFS " + strVersion;

            // Use theme color for bottom line instead of hardcoded green
            label1.ForeColor = ThemeManager.BannerColor2;

            Console.WriteLine(strVersion);

            Console.WriteLine("Splash .ctor");
        }
    }
}