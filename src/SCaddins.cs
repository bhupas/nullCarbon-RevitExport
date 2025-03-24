// (C) Copyright 2014-2020 by Andrew Nicholas (andrewnicholas@iinet.net.au)
//
// This file is part of SCaddins.
//
// SCaddins is free software: you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// SCaddins is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public License
// along with SCaddins.  If not, see <http://www.gnu.org/licenses/>.

// [assembly: System.CLSCompliant(true)]
namespace SCaddins
{
    using System;
    using System.Collections.Generic;
    using System.Dynamic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Reflection;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Autodesk.Revit.Attributes;
    using Autodesk.Revit.UI;
    using Newtonsoft.Json;
    using Properties;

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    [Journaling(JournalingMode.NoCommandData)]
    public class SCaddinsApp : IExternalApplication
    {
        // ReSharper disable once InconsistentNaming
        private static Common.Bootstrapper bootstrapper;
        // ReSharper disable once InconsistentNaming
        private static Common.WindowManager windowManager;
        private RibbonPanel ribbonPanel;
        private PushButton scheduleExporterPushButton;
        private PushButton aboutPushButton;

        public static Version Version => Assembly.GetExecutingAssembly().GetName().Version;

        public static Common.WindowManager WindowManager
        {
            get
            {
                if (bootstrapper == null)
                {
                    bootstrapper = new Common.Bootstrapper();
                }
                if (windowManager != null)
                {
                    return windowManager;
                }
                else
                {
                    windowManager = new Common.WindowManager(new Common.BasicDialogService());
                    return windowManager;
                }
            }

            set => windowManager = value;
        }

        ////[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times")]
        public static void CheckForUpdates(bool newOnly)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var uri = new Uri("https://api.github.com/repos/bhupas/nullCarbon-RevitExport/releases/latest");
            var latestVersion = new LatestVersion();
#if NET48
            var webRequest = WebRequest.Create(uri) as HttpWebRequest;
            if (webRequest == null)
            {
                return;
            }

            webRequest.ContentType = "application/json";
            webRequest.UserAgent = "Nothing";
            var latestAsJson = "nothing to see here";
            if (latestAsJson == null) {
                throw new ArgumentNullException(nameof(latestAsJson));
            }

            using (var s = webRequest.GetResponse().GetResponseStream())
            using (var sr = new StreamReader(s))
            {
                latestAsJson = sr.ReadToEnd();
            }
            latestVersion = JsonConvert.DeserializeObject<LatestVersion>(latestAsJson);
#else
            var latestAsJson = string.Empty;
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("User-Agent", "C# App");
                var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/bhupas/nullCarbon-RevitExport/releases/latest");
                var response = httpClient.Send(request);
                using (var reader = new StreamReader(response.Content.ReadAsStream()))
                {
                    latestAsJson = reader.ReadToEnd();
                }
                try
                {
                    latestVersion = System.Text.Json.JsonSerializer.Deserialize<LatestVersion>(latestAsJson);
                }
                catch (Exception ex)
                {
                    SCaddinsApp.WindowManager.ShowErrorMessageBox("Json Error", ex.Message);
                    return;
                }
            }
#endif
            var installedVersion = Version;
            var latestAvailableVersion = new Version(latestVersion.tag_name.Replace("v", string.Empty).Trim());
            var info = latestVersion.body;

            var downloadLink = latestVersion.assets.FirstOrDefault().browser_download_url;
            if (string.IsNullOrEmpty(downloadLink))
            {
                downloadLink = Constants.DownloadLink;
            }

            if (latestAvailableVersion <= installedVersion && newOnly)
            {
                return;
            }
            dynamic settings = new ExpandoObject();
            settings.Height = 640;
            settings.Width = 480;
            settings.Title = "SCaddins Version Information";
            settings.ShowInTaskbar = false;
            settings.ResizeMode = System.Windows.ResizeMode.NoResize;
            settings.SizeToContent = System.Windows.SizeToContent.WidthAndHeight;
            var upgradeViewModel = new Common.ViewModels.UpgradeViewModel(installedVersion, latestAvailableVersion, info, downloadLink);
            WindowManager.ShowDialogAsync(upgradeViewModel, null, settings);
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

#if REVIT2024 || REVIT2025
        public void ChangeTheme()
        {

#if NET48
            var dll = new Uri(Assembly.GetAssembly(typeof(SCaddinsApp)).CodeBase).LocalPath;
#else
            var dll = new Uri(Assembly.GetAssembly(typeof(SCaddinsApp)).Location).LocalPath;
#endif


            UITheme theme = UIThemeManager.CurrentTheme;
            switch (theme)
            {
                case UITheme.Dark:
                    AssignPushButtonImage(scheduleExporterPushButton, @"SCaddins.Assets.Ribbon.scexport-rvt-16-dark.png", 16, dll);
                    AssignPushButtonImage(scheduleExporterPushButton, @"SCaddins.Assets.Ribbon.scexport-rvt-dark.png", 32, dll);
                    AssignPushButtonImage(aboutPushButton, @"SCaddins.Assets.Ribbon.scexport-rvt-16-dark.png", 16, dll);
                    AssignPushButtonImage(aboutPushButton, @"SCaddins.Assets.Ribbon.scexport-rvt-dark.png", 32, dll);
                    break;
                case UITheme.Light:
                    AssignPushButtonImage(scheduleExporterPushButton, @"SCaddins.Assets.Ribbon.scexport-rvt-16.png", 16, dll);
                    AssignPushButtonImage(scheduleExporterPushButton, @"SCaddins.Assets.Ribbon.scexport-rvt.png", 32, dll);
                    AssignPushButtonImage(aboutPushButton, @"SCaddins.Assets.Ribbon.scexport-rvt-16.png", 16, dll);
                    AssignPushButtonImage(aboutPushButton, @"SCaddins.Assets.Ribbon.scexport-rvt.png", 32, dll);
                    break;
            }
            ribbonPanel.Visible = false;
            ribbonPanel.Visible = true;
        }
#endif

        public Result OnStartup(UIControlledApplication application)
        {
#if REVIT2024 || REVIT2025
            application.ThemeChanged += Application_ThemeChanged;
#endif

            ribbonPanel = TryGetPanel(application, "nullCarbon");

            if (ribbonPanel == null)
            {
                return Result.Failed;
            }

#if NET48
            var scdll = new Uri(Assembly.GetAssembly(typeof(SCaddinsApp)).CodeBase).LocalPath;
#else
            var scdll = new Uri(Assembly.GetAssembly(typeof(SCaddinsApp)).Location).LocalPath;
#endif

            // Only add the Export Schedules and About buttons
            var exportButton = LoadScheduleExporter(scdll);
            scheduleExporterPushButton = ribbonPanel.AddItem(exportButton) as PushButton;

            // Explicitly set the icon for Export button
            AssignPushButtonImage(scheduleExporterPushButton, @"SCaddins.Assets.Ribbon.scexport-rvt-16.png", 16, scdll);
            AssignPushButtonImage(scheduleExporterPushButton, @"SCaddins.Assets.Ribbon.scexport-rvt.png", 32, scdll);

            // Add About button
            var aboutButton = LoadAbout(scdll);
            aboutPushButton = ribbonPanel.AddItem(aboutButton) as PushButton;

            // Explicitly set the icon for About button
            AssignPushButtonImage(aboutPushButton, "SCaddins.Assets.Ribbon.scexport-rvt-16.png", 16, scdll);
            AssignPushButtonImage(aboutPushButton, "SCaddins.Assets.Ribbon.scexport-rvt.png", 32, scdll);

#if REVIT2024 || REVIT2025
            ChangeTheme();
#else
            AssignPushButtonImage(scheduleExporterPushButton, @"SCaddins.Assets.Ribbon.table-rvt-16.png", 16, scdll);
#endif

            return Result.Succeeded;
        }

#if REVIT2024 || REVIT2025
        private void Application_ThemeChanged(object sender, Autodesk.Revit.UI.Events.ThemeChangedEventArgs e)
        {
            ChangeTheme();
        }
#endif

        private static RibbonPanel TryGetPanel(UIControlledApplication application, string name)
        {
            if (application == null || string.IsNullOrEmpty(name))
            {
                return null;
            }
            List<RibbonPanel> loadedPanels = application.GetRibbonPanels();
            foreach (RibbonPanel p in loadedPanels)
            {
                if (p.Name.Equals(name, StringComparison.InvariantCulture))
                {
                    return p;
                }
            }
            return application.CreateRibbonPanel(name);
        }

        private static void AssignPushButtonImage(PushButton pushButton, string iconName, int size, string dll)
        {
            if (size == -1)
            {
                size = 32;
            }
            ImageSource image = LoadPNGImageSource(iconName, dll);
            if (image != null && pushButton != null)
            {
                if (size == 32)
                {
                    pushButton.LargeImage = image;
                }
                else
                {
                    pushButton.Image = image;
                }
            }
        }

        private static void AssignPushButtonImage(ButtonData pushButtonData, string iconName, int size, string dll)
        {
            if (size == -1)
            {
                size = 32;
            }
            ImageSource image = LoadPNGImageSource(iconName, dll);
            if (image != null && pushButtonData != null)
            {
                if (size == 32)
                {
                    pushButtonData.LargeImage = image;
                }
                else
                {
                    pushButtonData.Image = image;
                }
            }
        }

        private static PushButtonData LoadAbout(string dll)
        {
            var pbd = new PushButtonData(
                              "SCaddinsAbout", Resources.About, dll, "SCaddins.Common.About");
            AssignPushButtonImage(pbd, "SCaddins.Assets.Ribbon.scexport-rvt-16.png", 16, dll);
            AssignPushButtonImage(pbd, "SCaddins.Assets.Ribbon.scexport-rvt.png", 32, dll);
            pbd.ToolTip = "About nullCarbon";
            return pbd;
        }

        private static ImageSource LoadPNGImageSource(string sourceName, string path)
        {
            try
            {
                Assembly assembly = Assembly.LoadFrom(Path.Combine(path));
                var icon = assembly.GetManifestResourceStream(sourceName);
                var decoder = new PngBitmapDecoder(icon, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
                ImageSource source = decoder.Frames[0];
                return source;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                return null;
            }
        }

        private static PushButtonData LoadScheduleExporter(string dll)
        {
            var pbd = new PushButtonData(
                              "Export Schedules", "nullCarbon Export", dll, "SCaddins.ExportSchedules.Command");
            pbd.ToolTip = "Export Schedules to nullCarbon";
            // Pre-assign images to the button data using SCexport icons
            AssignPushButtonImage(pbd, @"SCaddins.Assets.Ribbon.scexport-rvt-16.png", 16, dll);
            AssignPushButtonImage(pbd, @"SCaddins.Assets.Ribbon.scexport-rvt.png", 32, dll);
            return pbd;
        }
    }
}

/* vim: set ts=4 sw=4 nu expandtab: */