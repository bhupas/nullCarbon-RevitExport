namespace SCaddins.ExportSchedules.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Dynamic;
    using System.IO;
    using System.Linq;
    using Caliburn.Micro;
    using SCaddins.ExportSchedules;

    public class ExportSchedulesViewModel : Screen
    {
        public ExportSchedulesViewModel(List<Schedule> schedules, string exportDir)
        {
            // Wrap each Schedule with ScheduleItemViewModel for check box selection.
            Schedules = new BindableCollection<ScheduleItemViewModel>(
                schedules.Select(s => new ScheduleItemViewModel(s)));
            ExportDir = exportDir;

            // Subscribe to property changes on each item so that when IsSelected changes,
            // we update ExportIsEnabled and ExportLabel.
            foreach (var item in Schedules)
            {
                item.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(ScheduleItemViewModel.IsSelected))
                    {
                        NotifyOfPropertyChange(() => ExportIsEnabled);
                        NotifyOfPropertyChange(() => ExportLabel);
                    }
                };
            }
        }

        public static dynamic DefaultWindowSettings
        {
            get
            {
                dynamic settings = new ExpandoObject();
                settings.Height = 480;
                settings.Width = 768;
                settings.Icon = new System.Windows.Media.Imaging.BitmapImage(
                    new Uri("pack://application:,,,/SCaddins;component/Assets/table.png"));
                settings.Title = "nullCarbon";
                settings.ShowInTaskbar = false;
                settings.SizeToContent = System.Windows.SizeToContent.Manual;
                settings.ResizeMode = System.Windows.ResizeMode.CanResizeWithGrip;
                settings.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
                return settings;
            }
        }

        public string ExportDir { get; set; }

        // Export is enabled only if at least one schedule is selected and the directory exists.
        public bool ExportIsEnabled =>
            Schedules.Any(item => item.IsSelected) && Directory.Exists(ExportDir);

        // Button label reflects how many schedules are selected.
        public string ExportLabel
        {
            get
            {
                int count = Schedules.Count(item => item.IsSelected);
                return ExportIsEnabled ? $"Export {count} Schedule(s)" : "Export";
            }
        }

        public BindableCollection<ScheduleItemViewModel> Schedules { get; set; }

        public void Export()
        {
            var selectedSchedules = Schedules
                .Where(item => item.IsSelected)
                .Select(item => item.Schedule)
                .ToList();

            var outputMsg = Utilities.Export(selectedSchedules, ExportDir);
            SCaddinsApp.WindowManager.ShowMessageBox(outputMsg);
        }

        public void Options()
        {
            var vm = new OptionsViewModel();
            SCaddinsApp.WindowManager.ShowDialogAsync(vm, null, OptionsViewModel.DefaultWindowSettings);
        }

        public void SelectExportDir()
        {
            string directory;
            string defaultDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var dialogResult = SCaddinsApp.WindowManager.ShowDirectorySelectionDialog(defaultDir, out directory);
            if (dialogResult.HasValue && dialogResult.Value == true)
            {
                if (Directory.Exists(directory))
                {
                    ExportDir = directory;
                    NotifyOfPropertyChange(() => ExportDir);
                    NotifyOfPropertyChange(() => ExportIsEnabled);
                    NotifyOfPropertyChange(() => ExportLabel);
                }
            }
        }
    }
}