using SmartX.API.Models;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;

namespace SmartX.Client.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;

        public ApiService()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("http://localhost:5090")
            };
        }

        public async Task<List<Sensor>> GetSensorsAsync()
        {
            var sensors = await _httpClient
                .GetFromJsonAsync<List<Sensor>>("api/Sensors");

            return sensors ?? new List<Sensor>();
        }

        public async Task<List<TelemetryPacket<float>>> GetSoilMoistureAsync()
        {
            var telemetry = await _httpClient
                .GetFromJsonAsync<List<TelemetryPacket<float>>>(
                    "api/Telemetry/soil-moisture");

            return telemetry ?? new List<TelemetryPacket<float>>();
        }

        public async Task<List<TelemetryPacket<int>>> GetPowerAsync()
        {
            var telemetry = await _httpClient
                .GetFromJsonAsync<List<TelemetryPacket<int>>>(
                    "api/Telemetry/power");

            return telemetry ?? new List<TelemetryPacket<int>>();
        }

        public async Task<List<TelemetryPacket<bool>>> GetValveAsync()
        {
            var telemetry = await _httpClient
                .GetFromJsonAsync<List<TelemetryPacket<bool>>>(
                    "api/Telemetry/valve");

            return telemetry ?? new List<TelemetryPacket<bool>>();
        }

        public async Task<List<Alert>> GetAlertsAsync()
        {
            var alerts = await _httpClient
                .GetFromJsonAsync<List<Alert>>("api/Alerts");

            return alerts ?? new List<Alert>();
        }

        public async Task<List<TelemetryPacket<float>>> GetSoilMoistureHistoryAsync(string? deviceIdentifier = null)
        {
            var url = "api/Telemetry/soil-moisture/history";

            if (!string.IsNullOrWhiteSpace(deviceIdentifier))
            {
                url += $"?deviceIdentifier={Uri.EscapeDataString(deviceIdentifier)}";
            }

            var telemetry = await _httpClient.GetFromJsonAsync<List<TelemetryPacket<float>>>(url);

            return telemetry ?? new List<TelemetryPacket<float>>();
        }

        public async Task<List<TelemetryPacket<int>>> GetPowerHistoryAsync(string? deviceIdentifier = null)
        {
            var url = "api/Telemetry/power/history";

            if (!string.IsNullOrWhiteSpace(deviceIdentifier))
            {
                url += $"?deviceIdentifier={Uri.EscapeDataString(deviceIdentifier)}";
            }

            var telemetry = await _httpClient.GetFromJsonAsync<List<TelemetryPacket<int>>>(url);

            return telemetry ?? new List<TelemetryPacket<int>>();
        }

        public async Task<List<TelemetryPacket<bool>>> GetValveHistoryAsync(string? deviceIdentifier = null)
        {
            var url = "api/Telemetry/valve/history";

            if (!string.IsNullOrWhiteSpace(deviceIdentifier))
            {
                url += $"?deviceIdentifier={Uri.EscapeDataString(deviceIdentifier)}";
            }

            var telemetry = await _httpClient.GetFromJsonAsync<List<TelemetryPacket<bool>>>(url);

            return telemetry ?? new List<TelemetryPacket<bool>>();
        }

        public async Task<TelemetryStatistics?> GetTelemetryStatisticsAsync()
        {
            return await _httpClient.GetFromJsonAsync<TelemetryStatistics>("api/TelemetryStats");
        }

        public async Task<(bool Success, string Message)> RegisterSensorAsync(Sensor sensor)
        {
            var response = await _httpClient.PostAsJsonAsync(
                "api/Sensors",
                sensor);

            if (response.IsSuccessStatusCode)
            {
                return (true, "Sensor registered successfully.");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                var error = await response.Content
                    .ReadFromJsonAsync<Dictionary<string, string>>();

                return (
                    false,
                    error?.GetValueOrDefault(
                        "message",
                        "A sensor with this device identifier already exists.")
                    ?? "A sensor with this device identifier already exists."
                );
            }

            return (false, $"Sensor registration failed. HTTP {(int)response.StatusCode}."
            );
        }

        public async Task<bool> UploadAttachmentAsync(Guid sensorId, string filePath)
        {
            using var form = new MultipartFormDataContent();

            await using var fileStream = File.OpenRead(filePath);

            using var fileContent = new StreamContent(fileStream);

            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

            form.Add(fileContent, "file", Path.GetFileName(filePath));

            var response = await _httpClient.PostAsync($"api/sensors/{sensorId}/attachments", form);

            return response.IsSuccessStatusCode;


        }

        public async Task<byte[]> DownloadAttachmentAsync(Guid sensorId, Guid attachmentId)
        {
            var response = await _httpClient.GetAsync($"api/sensors/{sensorId}/attachments/{attachmentId}/download");

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsByteArrayAsync();
        }

        public async Task<List<Attachment>> GetAttachmentsAsync(Guid sensorId)
        {
            var attachments = await _httpClient.GetFromJsonAsync<List<Attachment>>($"api/sensors/{sensorId}/attachments");

            return attachments ?? new List<Attachment>();
        }

        public async Task<TelemetrySearchResult<float>?> SearchSoilMoistureAsync(string deviceIdentifier)
        {
            var url = $"api/TelemetrySearch/soil-moisture?deviceIdentifier=" + $"{Uri.EscapeDataString(deviceIdentifier)}";

            return await _httpClient
                .GetFromJsonAsync<TelemetrySearchResult<float>>(url);
        }

        public async Task<TelemetrySearchResult<int>?> SearchPowerAsync(string deviceIdentifier)
        {
            var url =
                $"api/TelemetrySearch/power?deviceIdentifier=" +
                $"{Uri.EscapeDataString(deviceIdentifier)}";

            return await _httpClient
                .GetFromJsonAsync<TelemetrySearchResult<int>>(url);
        }

        public async Task<TelemetrySearchResult<bool>?> SearchValveAsync(string deviceIdentifier)
        {
            var url =
                $"api/TelemetrySearch/valve?deviceIdentifier=" +
                $"{Uri.EscapeDataString(deviceIdentifier)}";

            return await _httpClient
                .GetFromJsonAsync<TelemetrySearchResult<bool>>(url);
        }
    }
}