using Caliburn.Micro;
using SCaddins.ExportSchedules;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;

namespace SCaddins.ExportSchedules.ViewModels
{
    public class ExportSchedulesViewModel : Screen
    {
        private bool isLoggedIn;

        public ExportSchedulesViewModel(List<Schedule> schedules, string exportDir)
        {
            Schedules = new BindableCollection<ScheduleItemViewModel>(
                schedules.Select(s => new ScheduleItemViewModel(s)));
            ExportDir = exportDir;
            IsLoggedIn = !string.IsNullOrEmpty(TokenCache.AccessToken);

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
                dynamic settings = new System.Dynamic.ExpandoObject();
                settings.Height = 480;
                settings.Width = 768;
                settings.Icon = new System.Windows.Media.Imaging.BitmapImage(
                    new Uri("pack://application:,,,/SCaddins;component/Assets/table.png"));
                settings.Title = "nullCarbon-LCA-Export";
                settings.ShowInTaskbar = false;
                settings.SizeToContent = System.Windows.SizeToContent.Manual;
                settings.ResizeMode = System.Windows.ResizeMode.CanResizeWithGrip;
                settings.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
                return settings;
            }
        }

        public string ExportDir { get; set; }

        public bool ExportIsEnabled =>
            Schedules.Any(item => item.IsSelected) && Directory.Exists(ExportDir);

        public string ExportLabel
        {
            get
            {
                int count = Schedules.Count(item => item.IsSelected);
                return ExportIsEnabled ? $"Export {count} Schedule(s)" : "Export";
            }
        }

        public bool IsLoggedIn
        {
            get => isLoggedIn;
            set
            {
                isLoggedIn = value;
                NotifyOfPropertyChange(() => IsLoggedIn);
                NotifyOfPropertyChange(() => LoginButtonText);
            }
        }

        public string LoginButtonText => IsLoggedIn ? "Logged in" : "Login";

        public BindableCollection<ScheduleItemViewModel> Schedules { get; set; }

        public void Export()
        {
            var selectedSchedules = Schedules
                .Where(item => item.IsSelected)
                .Select(item => item.Schedule)
                .ToList();

            var outputMsg = Utilities.Export(selectedSchedules, ExportDir);
            SCaddinsApp.WindowManager.ShowMessageBox(outputMsg);
            TryCloseAsync(true);
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

        public async void LoginCommand()
        {
            if (IsLoggedIn)
            {
                // If already logged in, do nothing or show a message
                SCaddinsApp.WindowManager.ShowMessageBox("You are already logged in.");
                return;
            }

            // Clear previous tokens to ensure a fresh login attempt.
            TokenCache.AccessToken = null;
            TokenCache.RefreshToken = null;

            // Create the LoginViewModel and show the login dialog.
            var vm = new LoginViewModel();
            await SCaddinsApp.WindowManager.ShowDialogAsync(vm);

            // After the dialog closes, check if a token was returned.
            if (!string.IsNullOrEmpty(TokenCache.AccessToken))
            {
                IsLoggedIn = true;
                SCaddinsApp.WindowManager.ShowMessageBox("You are logged in!");
            }
            else
            {
                SCaddinsApp.WindowManager.ShowMessageBox("Login failed or was canceled.");
            }
        }

        public void SignUpCommand()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://app.nullcarbon.dk/sign-up",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                SCaddinsApp.WindowManager.ShowMessageBox($"Could not open the website: {ex.Message}");
            }
        }
    }
}