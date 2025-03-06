using Caliburn.Micro;
using SCaddins.ExportSchedules.Models;
using SCaddins.ExportSchedules.Services;
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
        private Team selectedTeam;
        private Building selectedBuilding;
        private Report selectedReport;
        private bool isLoading;
        private string statusMessage;

        public ExportSchedulesViewModel(List<Schedule> schedules, string exportDir)
        {
            Schedules = new BindableCollection<ScheduleItemViewModel>(
                schedules.Select(s => new ScheduleItemViewModel(s)));
            ExportDir = exportDir;
            IsLoggedIn = !string.IsNullOrEmpty(TokenCache.AccessToken);
            Teams = new BindableCollection<Team>();
            Buildings = new BindableCollection<Building>();
            Reports = new BindableCollection<Report>();

            foreach (var item in Schedules)
            {
                item.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(ScheduleItemViewModel.IsSelected))
                    {
                        NotifyOfPropertyChange(() => ExportIsEnabled);
                        NotifyOfPropertyChange(() => ExportLabel);
                        NotifyOfPropertyChange(() => ExportOnlineIsEnabled);
                    }
                };
            }

            // If already logged in, load teams
            if (IsLoggedIn)
            {
                LoadTeamsAsync();
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

        // Original properties
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
                if (isLoggedIn != value)
                {
                    isLoggedIn = value;
                    NotifyOfPropertyChange(() => IsLoggedIn);
                    NotifyOfPropertyChange(() => LoginButtonText);
                    NotifyOfPropertyChange(() => ExportOnlineIsEnabled);

                    // If logging in, load teams
                    if (value)
                    {
                        LoadTeamsAsync();
                    }
                    else
                    {
                        // If logging out, clear teams, buildings, and reports
                        Teams.Clear();
                        Buildings.Clear();
                        Reports.Clear();
                        SelectedTeam = null;
                        SelectedBuilding = null;
                        SelectedReport = null;
                    }
                }
            }
        }

        public string LoginButtonText => IsLoggedIn ? "Logged in" : "Login";

        public BindableCollection<ScheduleItemViewModel> Schedules { get; set; }

        // Team/building/report selection properties
        public BindableCollection<Team> Teams { get; private set; }

        public BindableCollection<Building> Buildings { get; private set; }

        public BindableCollection<Report> Reports { get; private set; }

        public Team SelectedTeam
        {
            get => selectedTeam;
            set
            {
                if (selectedTeam != value)
                {
                    selectedTeam = value;
                    NotifyOfPropertyChange(() => SelectedTeam);
                    NotifyOfPropertyChange(() => ExportOnlineIsEnabled);

                    // Clear buildings and reports when team changes
                    Buildings.Clear();
                    Reports.Clear();
                    SelectedBuilding = null;
                    SelectedReport = null;

                    // Load buildings for the selected team
                    if (selectedTeam != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Selected team: {selectedTeam.Name}, Slug: {selectedTeam.Slug}");
                        LoadBuildingsAsync(selectedTeam.Slug);
                    }
                }
            }
        }

        public Building SelectedBuilding
        {
            get => selectedBuilding;
            set
            {
                if (selectedBuilding != value)
                {
                    selectedBuilding = value;
                    NotifyOfPropertyChange(() => SelectedBuilding);
                    NotifyOfPropertyChange(() => ExportOnlineIsEnabled);

                    // Clear reports when building changes
                    Reports.Clear();
                    SelectedReport = null;

                    // Load reports for the selected building
                    if (selectedBuilding != null)
                    {
                        LoadReportsAsync(selectedBuilding.Id);
                    }
                }
            }
        }

        public Report SelectedReport
        {
            get => selectedReport;
            set
            {
                if (selectedReport != value)
                {
                    selectedReport = value;
                    NotifyOfPropertyChange(() => SelectedReport);
                    NotifyOfPropertyChange(() => ExportOnlineIsEnabled);
                }
            }
        }

        public bool IsLoading
        {
            get => isLoading;
            set
            {
                if (isLoading != value)
                {
                    isLoading = value;
                    NotifyOfPropertyChange(() => IsLoading);
                }
            }
        }

        public string StatusMessage
        {
            get => statusMessage;
            set
            {
                if (statusMessage != value)
                {
                    statusMessage = value;
                    NotifyOfPropertyChange(() => StatusMessage);
                }
            }
        }

        public bool ExportOnlineIsEnabled =>
            IsLoggedIn &&
            SelectedTeam != null &&
            SelectedBuilding != null &&
            SelectedReport != null &&
            Schedules.Any(item => item.IsSelected) &&
            Directory.Exists(ExportDir) &&
            !IsLoading;

        // Original methods
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
                    NotifyOfPropertyChange(() => ExportOnlineIsEnabled);
                }
            }
        }

        // Online functionality methods
        private async void LoadTeamsAsync()
        {
            try
            {
                StatusMessage = "Loading teams...";
                IsLoading = true;

                var teams = await ApiService.GetTeams(TokenCache.AccessToken);

                Teams.Clear();
                foreach (var team in teams)
                {
                    Teams.Add(team);
                }

                // If there's only one team, select it automatically
                if (Teams.Count == 1)
                {
                    SelectedTeam = Teams[0];
                }

                StatusMessage = "";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading teams: {ex.Message}";
                SCaddinsApp.WindowManager.ShowMessageBox($"Failed to load teams: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async void LoadBuildingsAsync(string teamSlug)
        {
            try
            {
                StatusMessage = $"Loading buildings for team '{teamSlug}'...";
                IsLoading = true;

                System.Diagnostics.Debug.WriteLine($"Loading buildings for team slug: {teamSlug}");

                // Validate arguments
                if (string.IsNullOrEmpty(teamSlug))
                {
                    throw new ArgumentException("Team slug cannot be empty");
                }

                if (string.IsNullOrEmpty(TokenCache.AccessToken))
                {
                    throw new InvalidOperationException("Access token is missing");
                }

                var buildings = await ApiService.GetBuildings(TokenCache.AccessToken, teamSlug);

                System.Diagnostics.Debug.WriteLine($"Retrieved {buildings.Count} buildings");

                Buildings.Clear();
                foreach (var building in buildings)
                {
                    System.Diagnostics.Debug.WriteLine($"Adding building: {building.Name} (ID: {building.Id})");
                    Buildings.Add(building);
                }

                // If there's only one building, select it automatically
                if (Buildings.Count == 1)
                {
                    SelectedBuilding = Buildings[0];
                }
                else if (Buildings.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Multiple buildings found: {Buildings.Count}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("No buildings found");
                }

                StatusMessage = Buildings.Count > 0
                    ? $"Loaded {Buildings.Count} buildings"
                    : "No buildings found";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in LoadBuildingsAsync: {ex}");
                StatusMessage = $"Error loading buildings: {ex.Message}";
                SCaddinsApp.WindowManager.ShowMessageBox($"Failed to load buildings: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async void LoadReportsAsync(string buildingId)
        {
            try
            {
                StatusMessage = $"Loading reports for building...";
                IsLoading = true;

                System.Diagnostics.Debug.WriteLine($"Loading reports for building ID: {buildingId}");

                var reports = await ApiService.GetReports(TokenCache.AccessToken, buildingId);

                System.Diagnostics.Debug.WriteLine($"Retrieved {reports.Count} reports");

                Reports.Clear();
                foreach (var report in reports)
                {
                    System.Diagnostics.Debug.WriteLine($"Adding report: {report.Name} (ID: {report.Id})");
                    Reports.Add(report);
                }

                // If there's only one report, select it automatically
                if (Reports.Count == 1)
                {
                    SelectedReport = Reports[0];
                }

                StatusMessage = Reports.Count > 0
                    ? $"Loaded {Reports.Count} reports"
                    : "No reports found";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in LoadReportsAsync: {ex}");
                StatusMessage = $"Error loading reports: {ex.Message}";
                SCaddinsApp.WindowManager.ShowMessageBox($"Failed to load reports: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async void ExportOnline()
        {
            if (!ExportOnlineIsEnabled)
            {
                return;
            }

            try
            {
                StatusMessage = "Exporting and uploading...";
                IsLoading = true;

                // Get selected schedules
                var selectedSchedules = Schedules
                    .Where(item => item.IsSelected)
                    .Select(item => item.Schedule)
                    .ToList();

                // Generate a unique filename based on the document name
                string docTitle = selectedSchedules[0]?.RevitViewSchedule?.Document?.Title;
                if (string.IsNullOrWhiteSpace(docTitle))
                {
                    docTitle = "UnknownProject";
                }
                string excelFileName = $"{docTitle}_nullCarbon-LCA-Export.xlsx";
                string excelFilePath = Path.Combine(ExportDir, excelFileName);

                // Use the existing export functionality to create the Excel file
                var exportMsg = Utilities.Export(selectedSchedules, ExportDir);

                // Check if the file was created successfully
                if (!File.Exists(excelFilePath))
                {
                    throw new Exception("Failed to create Excel file for upload.");
                }

                // Read the file into a byte array
                byte[] fileData = File.ReadAllBytes(excelFilePath);

                // Upload the file to the nullCarbon backend
                bool uploadSuccess = await ApiService.UploadExcelFile(
                    TokenCache.AccessToken,
                    SelectedTeam.Slug,
                    SelectedBuilding.Id,
                    SelectedReport.Id,
                    fileData,
                    excelFileName
                );

                if (uploadSuccess)
                {
                    StatusMessage = "Export and upload completed successfully.";
                    SCaddinsApp.WindowManager.ShowMessageBox("The schedules have been exported and uploaded successfully.");
                }
                else
                {
                    StatusMessage = "Upload failed.";
                    SCaddinsApp.WindowManager.ShowMessageBox("The schedules were exported but could not be uploaded to the server.");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error during export and upload: {ex.Message}";
                SCaddinsApp.WindowManager.ShowMessageBox($"Export and upload failed: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
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
                IsLoggedIn = true; // This will trigger LoadTeamsAsync()
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