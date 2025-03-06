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
        private const string BaseUrl = "https://backend.nullcarbon.dk";

        public static async Task<List<Team>> GetTeams(string accessToken)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    var response = await client.GetAsync($"{BaseUrl}/teams/api/teams/");

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var teamsResponse = JsonConvert.DeserializeObject<TeamsResponse>(content);
                        return teamsResponse.Results;
                    }
                    else
                    {
                        throw new Exception($"Failed to get teams: {response.StatusCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting teams: {ex.Message}", ex);
            }
        }

        public static async Task<List<Building>> GetBuildings(string accessToken, string teamSlug)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    var response = await client.GetAsync($"{BaseUrl}/lca/{teamSlug}/building/");

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var buildingsResponse = JsonConvert.DeserializeObject<BuildingsResponse>(content);
                        return buildingsResponse.Results;
                    }
                    else
                    {
                        throw new Exception($"Failed to get buildings: {response.StatusCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting buildings: {ex.Message}", ex);
            }
        }

        public static async Task<List<Report>> GetReports(string accessToken, string buildingId)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    var response = await client.GetAsync($"{BaseUrl}/lca/{buildingId}/reports/");

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var reportsResponse = JsonConvert.DeserializeObject<ReportsResponse>(content);
                        return reportsResponse.Results;
                    }
                    else
                    {
                        throw new Exception($"Failed to get reports: {response.StatusCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting reports: {ex.Message}", ex);
            }
        }

        public static async Task<bool> UploadExcelFile(
            string accessToken,
            string teamSlug,
            string buildingId,
            string reportId,
            byte[] fileData,
            string fileName)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                    using (var content = new MultipartFormDataContent())
                    {
                        var fileContent = new ByteArrayContent(fileData);
                        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

                        content.Add(fileContent, "file", fileName);
                        content.Add(new StringContent(reportId), "report_id"); // Include the report ID

                        // Endpoint for uploading Excel files
                        var response = await client.PostAsync($"{BaseUrl}/lca/{teamSlug}/building/{buildingId}/upload-excel/", content);

                        return response.IsSuccessStatusCode;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error uploading Excel file: {ex.Message}", ex);
            }
        }
    }
}