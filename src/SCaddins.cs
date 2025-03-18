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
        private PushButton nullCarbonExportButton;

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

        // Simplified version that just shows version info
        public static void CheckForUpdates(bool newOnly)
        {
            dynamic settings = new ExpandoObject();
            settings.Height = 300;
            settings.Width = 400;
            settings.Title = "SCaddins Version Information";
            settings.ShowInTaskbar = false;
            settings.ResizeMode = System.Windows.ResizeMode.NoResize;
            settings.SizeToContent = System.Windows.SizeToContent.WidthAndHeight;

            WindowManager.ShowMessageBox(
                "SCaddins Version",
                "Current version: " + Version.ToString());
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        public Result OnStartup(UIControlledApplication application)
        {
#if REVIT2024 || REVIT2025
            application.ThemeChanged += Application_ThemeChanged;
#endif

            ribbonPanel = TryGetPanel(application, "Studio.SC");

            if (ribbonPanel == null)
            {
                return Result.Failed;
            }

#if NET48
            var scdll = new Uri(Assembly.GetAssembly(typeof(SCaddinsApp)).CodeBase).LocalPath;
#else
            var scdll = new Uri(Assembly.GetAssembly(typeof(SCaddinsApp)).Location).LocalPath;
#endif

            // Add nullCarbon-LCA-Export button
            nullCarbonExportButton = ribbonPanel.AddItem(LoadNullCarbonExporter(scdll)) as PushButton;

#if REVIT2024 || REVIT2025
            ApplyThemeToButtons();
#else
            AssignPushButtonImage(nullCarbonExportButton, @"SCaddins.Assets.Ribbon.table-rvt-16.png", 16, scdll);
#endif

            return Result.Succeeded;
        }

#if REVIT2024 || REVIT2025
        private void Application_ThemeChanged(object sender, Autodesk.Revit.UI.Events.ThemeChangedEventArgs e)
        {
            ApplyThemeToButtons();
        }

        private void ApplyThemeToButtons()
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
                    AssignPushButtonImage(nullCarbonExportButton, @"SCaddins.Assets.Ribbon.table-rvt-16-dark.png", 16, dll);
                    break;
                case UITheme.Light:
                    AssignPushButtonImage(nullCarbonExportButton, @"SCaddins.Assets.Ribbon.table-rvt-16.png", 16, dll);
                    break;
            }

            ribbonPanel.Visible = false;
            ribbonPanel.Visible = true;
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

        private static PushButtonData LoadNullCarbonExporter(string dll)
        {
            var pbd = new PushButtonData(
                              "nullCarbon-LCA-Export", "nullCarbon-LCA-Export", dll, "SCaddins.ExportSchedules.Command");
            pbd.ToolTip = "Export Schedules for nullCarbon LCA";
            return pbd;
        }
    }
}