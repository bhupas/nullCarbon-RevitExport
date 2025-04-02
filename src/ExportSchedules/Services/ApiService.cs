using Newtonsoft.Json;
using SCaddins.ExportSchedules.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace SCaddins.ExportSchedules.Services
{
    public class ApiService
    {
        private const string BaseUrl = "https://nullcarbonstaging.germanywestcentral.cloudapp.azure.com/backend";
        private static readonly HttpClient _httpClient;

        // Static constructor to initialize the shared HttpClient
        static ApiService()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(5) // Increased to 5 minutes (300 seconds)
            };
        }

        /// <summary>
        /// Sets the authorization token for API requests
        /// </summary>
        public static void SetAuthToken(string accessToken)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);
        }

        /// <summary>
        /// Get all teams for the authenticated user
        /// </summary>
        public static async Task<List<Team>> GetTeams(string accessToken)
        {
            SetAuthToken(accessToken);

            try
            {
                var response = await _httpClient.GetAsync($"{BaseUrl}/teams/api/teams/");

                await EnsureSuccessStatusCodeWithDetailedErrorAsync(
                    response, "Failed to get teams");

                var content = await response.Content.ReadAsStringAsync();
                var teamsResponse = JsonConvert.DeserializeObject<TeamsResponse>(content);
                return teamsResponse.Results ?? new List<Team>();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving teams: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get all buildings for a team
        /// </summary>
        public static async Task<List<Building>> GetBuildings(string accessToken, string teamSlug)
        {
            if (string.IsNullOrEmpty(teamSlug))
            {
                throw new ArgumentException("Team slug cannot be empty", nameof(teamSlug));
            }

            SetAuthToken(accessToken);

            try
            {
                var response = await _httpClient.GetAsync($"{BaseUrl}/lca/{teamSlug}/building/");

                await EnsureSuccessStatusCodeWithDetailedErrorAsync(
                    response, $"Failed to get buildings for team '{teamSlug}'");

                var content = await response.Content.ReadAsStringAsync();
                var buildingsResponse = JsonConvert.DeserializeObject<BuildingsResponse>(content);
                return buildingsResponse.Results ?? new List<Building>();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving buildings: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get all reports for a building
        /// </summary>
        public static async Task<List<Report>> GetReports(string accessToken, string buildingId)
        {
            if (string.IsNullOrEmpty(buildingId))
            {
                throw new ArgumentException("Building ID cannot be empty", nameof(buildingId));
            }

            SetAuthToken(accessToken);

            try
            {
                var response = await _httpClient.GetAsync($"{BaseUrl}/lca/{buildingId}/reports/");

                await EnsureSuccessStatusCodeWithDetailedErrorAsync(
                    response, $"Failed to get reports for building '{buildingId}'");

                var content = await response.Content.ReadAsStringAsync();
                var reportsResponse = JsonConvert.DeserializeObject<ReportsResponse>(content);
                return reportsResponse.Results ?? new List<Report>();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving reports: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Upload an Excel file to a report
        /// </summary>
        public static async Task<bool> UploadExcelFile(
            string accessToken,
            string reportId,
            byte[] fileData,
            string fileName,
            string processor = "Revit")
        {
            if (string.IsNullOrEmpty(reportId))
            {
                throw new ArgumentException("Report ID cannot be empty", nameof(reportId));
            }

            if (fileData == null || fileData.Length == 0)
            {
                throw new ArgumentException("File data cannot be empty", nameof(fileData));
            }

            SetAuthToken(accessToken);

            try
            {
                // Show file size in the debug output
                System.Diagnostics.Debug.WriteLine($"Uploading file {fileName}, size: {fileData.Length / 1024} KB");

                using (var content = new MultipartFormDataContent())
                {
                    // Add the excel_file parameter
                    var fileContent = new ByteArrayContent(fileData);
                    fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                    content.Add(fileContent, "excel_file", fileName);

                    // Add the processor parameter
                    content.Add(new StringContent(processor), "processor");

                    // Add progress reporting for large uploads
                    System.Diagnostics.Debug.WriteLine("Starting upload...");
                    var response = await _httpClient.PostAsync($"{BaseUrl}/lca/report/{reportId}/batch-upload", content);
                    System.Diagnostics.Debug.WriteLine("Upload completed");

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        throw new Exception($"Upload failed with status {response.StatusCode}: {errorContent}");
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error uploading Excel file: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Helper method to provide more detailed error information
        /// </summary>
        private static async Task EnsureSuccessStatusCodeWithDetailedErrorAsync(
            HttpResponseMessage response, string context)
        {
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"{context}: {response.StatusCode}. Details: {errorContent}");
            }
        }
    }
}