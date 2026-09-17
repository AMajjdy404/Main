using Main.Core.Models.Settings;
using Main.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Main.Application
{
    public class SmsService : ISmsService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly HttpClient _httpClient;
        private readonly SmsSettings _settings;
        private readonly ILogger<SmsService> _logger;

        public SmsService(HttpClient httpClient, IOptions<SmsSettings> options, ILogger<SmsService> logger)
        {
            _httpClient = httpClient;
            _settings = options.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger;

            if (!_httpClient.DefaultRequestHeaders.Contains("Authorization"))
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", _settings.AuthToken);
            }

            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task SendAsync(string phoneNumber, string message, string requestId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_settings.ApiUrl))
                throw new InvalidOperationException("إعدادات SMS غير مهيأة.");

            var cleanedPhone = phoneNumber
                .Trim()
                .Replace("+", "")
                .Replace(" ", "")
                .Replace("-", "");

            if (cleanedPhone.StartsWith("20") && cleanedPhone.Length == 12)
            {
                cleanedPhone = "0" + cleanedPhone[2..];
            }

            var requestBody = new
            {
                SenderName = _settings.SenderName,
                RequestID = requestId,
                PhoneNumber = cleanedPhone,
                Message = message
            };

            var json = JsonSerializer.Serialize(requestBody, JsonOptions);

            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation("SMS request → URL: {Url}, Body: {Body}", _settings.ApiUrl, json);

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.PostAsync(_settings.ApiUrl.Trim(), content, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // A raw Exception/HttpRequestException/TaskCanceledException falls into the
                // ExceptionMiddleware's generic 500 bucket, which hides ex.Message from the
                // client outside Development. InvalidOperationException is the one exception
                // type this codebase treats as "safe to show the client" (mapped to 400 with
                // ex.Message returned as-is) — use it so gateway failures are diagnosable.
                _logger.LogError(ex, "SMS request failed → URL: {Url}", _settings.ApiUrl);
                throw new InvalidOperationException($"تعذر الاتصال بخدمة إرسال الرسائل: {ex.Message}");
            }

            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("SMS failed → Status: {Status}, Reason: {Reason}, Body: {Body}",
                    response.StatusCode, response.ReasonPhrase, responseText);
                throw new InvalidOperationException($"فشل إرسال رمز التحقق عبر الرسائل النصية: {response.StatusCode} - {responseText}");
            }

            _logger.LogInformation("SMS success → {Response}", responseText);
        }
    }
}
