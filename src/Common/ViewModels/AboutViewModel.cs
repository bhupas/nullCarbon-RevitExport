namespace SCaddins.Common.ViewModels
{
    using System.Diagnostics;
    using System.Reflection;
    // Fully qualify the Screen reference to avoid ambiguity
    using CaliburnScreen = Caliburn.Micro.Screen;

    public class AboutViewModel : CaliburnScreen
    {
        public static string AssemblyBuildDate =>
            Properties.Resources.BuildDate.TrimEnd(System.Environment.NewLine.ToCharArray());

        public static string AssemblyCopyright
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly()
                    .GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false);
                return attributes.Length == 0 ? string.Empty : ((AssemblyCopyrightAttribute)attributes[0]).Copyright.Trim();
            }
        }

        public static string AssemblyDescription
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly()
                    .GetCustomAttributes(typeof(AssemblyDescriptionAttribute), false);
                return attributes.Length == 0 ? string.Empty : ((AssemblyDescriptionAttribute)attributes[0]).Description.Trim();
            }
        }

        public static string AssemblyTitle
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly()
                    .GetCustomAttributes(typeof(AssemblyTitleAttribute), false);
                if (attributes.Length > 0)
                {
                    var titleAttribute =
                        (AssemblyTitleAttribute)attributes[0];
                    if (!string.IsNullOrEmpty(titleAttribute.Title))
                    {
                        return titleAttribute.Title.Trim();
                    }
                }

                return System.IO.Path.GetFileNameWithoutExtension(Assembly
                        .GetExecutingAssembly().Location.Trim());
            }
        }

        public static string AssemblyVersion => Assembly.GetExecutingAssembly().GetName()
                .Version.ToString().Trim();

        public static string License => Constants.License;

        public static string AssemblyCompany
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly()
                    .GetCustomAttributes(typeof(AssemblyCompanyAttribute), false);
                if (attributes.Length == 0)
                {
                    return string.Empty;
                }
                return ((AssemblyCompanyAttribute)attributes[0]).Company.Trim();
            }
        }

        public static string AssemblyInformationalVersion => GetInformationalVersion(Assembly.GetExecutingAssembly());

        public static string AssemblyVersionExtended => AssemblyVersion + @"(" + AssemblyInformationalVersion + @") - " + AssemblyBuildDate;

        public static string AssemblyProduct()
        {
            object[] attributes = Assembly.GetExecutingAssembly()
                .GetCustomAttributes(typeof(AssemblyProductAttribute), false);
            return attributes.Length == 0 ? string.Empty : ((AssemblyProductAttribute)attributes[0]).Product.Trim();
        }

        // Simplified method that doesn't reference CheckForUpdates
        public static void CheckForUpgrades()
        {
            // Simply show information about current version instead
            SCaddinsApp.WindowManager.ShowMessageBox(
                "About SCaddins",
                "Current version: " + AssemblyVersion +
                "\nBuild date: " + AssemblyBuildDate);
        }

        public static string GetInformationalVersion(Assembly assembly) => FileVersionInfo.GetVersionInfo(assembly.Location).ProductVersion;

        public static void NavigateTo(System.Uri url)
        {
            var ps = new ProcessStartInfo(url.ToString())
            {
                UseShellExecute = true,
                Verb = "open"
            };
            Process.Start(ps);
        }
    }
}