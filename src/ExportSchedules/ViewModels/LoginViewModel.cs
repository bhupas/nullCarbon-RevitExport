using Caliburn.Micro;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SCaddins.ExportSchedules.ViewModels
{
    public class LoginViewModel : Screen
    {
        private string username;
        public string Username
        {
            get => username;
            set
            {
                username = value;
                NotifyOfPropertyChange(() => Username);
            }
        }

        // We'll collect the password from the OnPasswordChanged event 
        private string password;
        public string Password
        {
            get => password;
            set
            {
                password = value;
                NotifyOfPropertyChange(() => Password);
            }
        }

        private string statusMessage;
        public string StatusMessage
        {
            get => statusMessage;
            set
            {
                statusMessage = value;
                NotifyOfPropertyChange(() => StatusMessage);
            }
        }

        // This is called from XAML when the PasswordBox changes:
        public void OnPasswordChanged(object source)
        {
            if (source is System.Windows.Controls.PasswordBox pwd)
            {
                Password = pwd.Password;
            }
        }

public async Task Login()
{
    // Example base URL for your environment:
    string baseUrl = "https://nullcarbonstaging.germanywestcentral.cloudapp.azure.com/backend";
    string loginRoute = "/auth/jwt/create";
    string fullUrl = baseUrl + loginRoute;

    var requestData = new
    {
        username = Username,
        password = Password
    };

    var jsonString = JsonConvert.SerializeObject(requestData);
    var requestContent = new StringContent(jsonString, Encoding.UTF8, "application/json");

    // Clear old status
    StatusMessage = "";

    try
    {
        using (var client = new HttpClient())
        {
            var response = await client.PostAsync(fullUrl, requestContent);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                // Deserialize the JSON response for tokens.
                var tokenData = JsonConvert.DeserializeObject<LoginResponse>(responseBody);

                if (tokenData != null && !string.IsNullOrEmpty(tokenData.Access))
                {
                    // Store the tokens in the static TokenCache.
                    TokenCache.AccessToken = tokenData.Access;
                    TokenCache.RefreshToken = tokenData.Refresh;

                    StatusMessage = "You are logged in!";
                    // Close the dialog on successful login.
                    await TryCloseAsync(true);
                }
                else
                {
                    StatusMessage = "Login succeeded, but no token returned.";
                }
            }
            else
            {
                StatusMessage = $"Login failed: {responseBody}";
            }
        }
    }
    catch (Exception ex)
    {
        StatusMessage = $"Exception: {ex.Message}";
    }
}
    }

    // Helper class for deserializing the JSON response
    public class LoginResponse
    {
        [JsonProperty("access")]
        public string Access { get; set; }

        [JsonProperty("refresh")]
        public string Refresh { get; set; }
    }

    // Simple static cache for storing tokens in memory
    public static class TokenCache
    {
        public static string AccessToken { get; set; }
        public static string RefreshToken { get; set; }
    }
}
