﻿using Caliburn.Micro;
using SCaddins.ExportSchedules.Models;
using SCaddins.ExportSchedules.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Windows.Data;
using System.ComponentModel;

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
        private string scheduleFilterText;
        private string scheduleTypeFilter;
        private ICollectionView scheduleCollectionView;
        private bool _canSelectSchedules;
        // Removed _selectAllSchedules field

        public ExportSchedulesViewModel(List<Schedule> schedules, string exportDir)
        {
            Schedules = new BindableCollection<ScheduleItemViewModel>(
                schedules.Select(s => new ScheduleItemViewModel(s)));

            // Set up collection view for filtering
            scheduleCollectionView = CollectionViewSource.GetDefaultView(Schedules);
            scheduleCollectionView.Filter = ScheduleFilter;

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
                        NotifyOfPropertyChange(() => ExportOnlineIsEnabled);
                        // Removed UpdateSelectAllState() call
                    }
                };
            }

            // If already logged in, load teams
            if (IsLoggedIn)
            {
                LoadTeamsAsync();
            }
        }

        // Removed UpdateSelectAllState() method

        public static dynamic DefaultWindowSettings
        {
            get
            {
                dynamic settings = new System.Dynamic.ExpandoObject();
                settings.Height = 750;
                settings.Width = 1050;
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

        // Schedule filtering properties
        public string ScheduleFilterText
        {
            get => scheduleFilterText;
            set
            {
                if (scheduleFilterText != value)
                {
                    scheduleFilterText = value;
                    NotifyOfPropertyChange(() => ScheduleFilterText);
                    scheduleCollectionView.Refresh();
                    // Removed UpdateSelectAllState() call
                }
            }
        }

        public string ScheduleTypeFilter
        {
            get => scheduleTypeFilter;
            set
            {
                if (scheduleTypeFilter != value)
                {
                    scheduleTypeFilter = value;
                    NotifyOfPropertyChange(() => ScheduleTypeFilter);
                    scheduleCollectionView.Refresh();
                    // Removed UpdateSelectAllState() call
                }
            }
        }

        // Removed SelectAllSchedules property

        private bool ScheduleFilter(object item)
        {
            if (item is ScheduleItemViewModel scheduleItem)
            {
                // Filter by text
                bool textMatch = string.IsNullOrEmpty(ScheduleFilterText) ||
                                 scheduleItem.Schedule.RevitName.IndexOf(ScheduleFilterText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 scheduleItem.Schedule.Type.IndexOf(ScheduleFilterText, StringComparison.OrdinalIgnoreCase) >= 0;

                // Filter by type
                bool typeMatch = string.IsNullOrEmpty(ScheduleTypeFilter) ||
                                 ScheduleTypeFilter == "All Types" ||
                                 scheduleItem.Schedule.Type == ScheduleTypeFilter;

                return textMatch && typeMatch;
            }
            return true;
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

        public string LoginButtonText => IsLoggedIn ? "Sign Out" : "Login";

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
                        Debug.WriteLine($"Selected team: {selectedTeam.Name}, Slug: {selectedTeam.Slug}");
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
                    Debug.WriteLine($"SelectedReport changed to: {(value == null ? "null" : value.Name)}");

                    // Update the CanSelectSchedules property
                    CanSelectSchedules = selectedReport != null;

                    NotifyOfPropertyChange(() => SelectedReport);
                    NotifyOfPropertyChange(() => ExportOnlineIsEnabled);
                }
            }
        }

        public bool CanSelectSchedules
        {
            get { return _canSelectSchedules; }
            private set
            {
                if (_canSelectSchedules != value)
                {
                    _canSelectSchedules = value;
                    Debug.WriteLine($"CanSelectSchedules changed to: {value}");
                    NotifyOfPropertyChange(() => CanSelectSchedules);
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

        // Added method to select/deselect all visible schedules
        public void SelectNoneVisible()
        {
            foreach (var item in Schedules)
            {
                if (scheduleCollectionView.Filter(item))
                {
                    item.IsSelected = false;
                }
            }
            NotifyOfPropertyChange(() => ExportIsEnabled);
            NotifyOfPropertyChange(() => ExportOnlineIsEnabled);
            NotifyOfPropertyChange(() => ExportLabel);
        }

        // Online functionality methods
        private async void LoadTeamsAsync()
        {
            try
            {
                StatusMessage = "Indlæser hold...";
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
                StatusMessage = $"Fejl ved indlæsning af hold: {ex.Message}";
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
                StatusMessage = $"Indlæser bygninger for hold '{teamSlug}'...";
                IsLoading = true;

                Debug.WriteLine($"Loading buildings for team slug: {teamSlug}");

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

                Debug.WriteLine($"Retrieved {buildings.Count} buildings");

                Buildings.Clear();
                foreach (var building in buildings)
                {
                    Debug.WriteLine($"Adding building: {building.Name} (ID: {building.Id})");
                    Buildings.Add(building);
                }

                // If there's only one building, select it automatically
                if (Buildings.Count == 1)
                {
                    SelectedBuilding = Buildings[0];
                }
                else if (Buildings.Count > 0)
                {
                    Debug.WriteLine($"Multiple buildings found: {Buildings.Count}");
                }
                else
                {
                    Debug.WriteLine("No buildings found");
                }

                StatusMessage = Buildings.Count > 0
                    ? $"Indlæst {Buildings.Count} bygninger"
                    : "Ingen bygninger fundet";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in LoadBuildingsAsync: {ex}");
                StatusMessage = $"Fejl ved indlæsning af bygninger: {ex.Message}";
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
                StatusMessage = $"Indlæser rapporter for bygning...";
                IsLoading = true;

                Debug.WriteLine($"Loading reports for building ID: {buildingId}");

                var reports = await ApiService.GetReports(TokenCache.AccessToken, buildingId);

                Debug.WriteLine($"Retrieved {reports.Count} reports");

                Reports.Clear();
                foreach (var report in reports)
                {
                    Debug.WriteLine($"Adding report: {report.Name} (ID: {report.Id})");
                    Reports.Add(report);
                }

                // If there's only one report, select it automatically
                if (Reports.Count == 1)
                {
                    Debug.WriteLine("Auto-selecting the only report: " + Reports[0].Name);
                    SelectedReport = Reports[0];

                    // Force update after a delay to ensure UI catches up
                    await System.Threading.Tasks.Task.Delay(100);
                    Execute.OnUIThread(() =>
                    {
                        // Force selection again to ensure it's set
                        SelectedReport = Reports[0];
                        Debug.WriteLine($"Force refreshed SelectedReport: {SelectedReport.Name}");
                        Debug.WriteLine($"CanSelectSchedules after refresh: {CanSelectSchedules}");
                    });
                }

                StatusMessage = Reports.Count > 0
                    ? $"Indlæst {Reports.Count} rapporter"
                    : "Ingen rapporter fundet";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in LoadReportsAsync: {ex}");
                StatusMessage = $"Fejl ved indlæsning af rapporter: {ex.Message}";
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
                // Get selected schedules
                var selectedSchedules = Schedules
                    .Where(item => item.IsSelected)
                    .Select(item => item.Schedule)
                    .ToList();

                if (selectedSchedules.Count == 0)
                {
                    StatusMessage = "Ingen skemaer valgt til eksport";
                    return;
                }

                // Generate schedule names for status message
                string scheduleNames = string.Join(", ", selectedSchedules.Select(s => s.RevitName));

                // If the list is too long, truncate it
                if (scheduleNames.Length > 80)
                {
                    int count = selectedSchedules.Count;
                    scheduleNames = scheduleNames.Substring(0, 80) + $"... and {count - 3} more";
                }

                StatusMessage = $"Vent venligst... Eksporterer skemaer: {scheduleNames}";
                IsLoading = true;

                // Generate a unique filename based on the document name
                string docTitle = selectedSchedules[0]?.RevitViewSchedule?.Document?.Title;
                if (string.IsNullOrWhiteSpace(docTitle))
                {
                    docTitle = "UnknownProject";
                }

                // Match the filename format used in Utilities.Export
                string mergedExcelFileName = docTitle + " nullCarbon-LCA-Export.xlsx";
                string mergedExcelFilePath = Path.Combine(ExportDir, mergedExcelFileName);

                // Update status with exporting message
                StatusMessage = "Vent venligst... Opretter Excel-fil";

                // Use the existing export functionality to create the Excel file
                var exportMsg = Utilities.Export(selectedSchedules, ExportDir);

                // Check if the file was created successfully
                if (!File.Exists(mergedExcelFilePath))
                {
                    throw new Exception("Failed to create Excel file for upload.");
                }

                // Get file size for information
                var fileInfo = new FileInfo(mergedExcelFilePath);
                string fileSize = (fileInfo.Length / 1024.0 / 1024.0).ToString("F2"); // Size in MB

                // Update status with uploading message
                StatusMessage = $"Vent venligst... Uploader til nullCarbon ({fileSize} MB)";

                // Read the file into a byte array
                byte[] fileData = File.ReadAllBytes(mergedExcelFilePath);

                // Upload the file to the nullCarbon backend
                StatusMessage = $"Uploader fil... Dette kan tage flere minutter for store filer";

                bool uploadSuccess = await ApiService.UploadExcelFile(
                    TokenCache.AccessToken,
                    SelectedReport.Id,
                    fileData,
                    mergedExcelFileName
                );

                if (uploadSuccess)
                {
                    StatusMessage = "Eksport og upload gennemført med succes.";
                    SCaddinsApp.WindowManager.ShowMessageBox(
                        "Skemaerne er blevet eksporteret og uploadet med succes\n\n" +
                        "Bemærk: Det kan tage et par minutter, før dataene vises i nullCarbon web-applikationen.");
                }
                else
                {
                    StatusMessage = "Upload mislykkedes.";
                    SCaddinsApp.WindowManager.ShowMessageBox("Skemaerne blev eksporteret, men kunne ikke uploades til serveren");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Fejl under eksport og upload: {ex.Message}";
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
                // If already logged in, sign out
                TokenCache.AccessToken = null;
                TokenCache.RefreshToken = null;
                IsLoggedIn = false;
                // Sign out popup message has been removed
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

            }
            else
            {
                SCaddinsApp.WindowManager.ShowMessageBox("Loginfejl eller blev annulleret");
            }
        }
    }
}