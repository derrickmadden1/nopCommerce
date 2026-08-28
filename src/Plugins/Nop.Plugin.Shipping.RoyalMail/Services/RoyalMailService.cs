using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nop.Services.Logging;
using Nop.Services.Shipping.Tracking;

namespace Nop.Plugin.Shipping.RoyalMail.Services;

/// <summary>
/// Service to communicate with Royal Mail Shipping & Tracking API
/// </summary>
public class RoyalMailService
{
    #region Fields

    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private readonly RoyalMailSettings _settings;

    #endregion

    #region Ctor

    public RoyalMailService(HttpClient httpClient,
        ILogger logger,
        RoyalMailSettings settings)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = settings;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Gets shipment events for a given tracking number from Royal Mail Tracking API
    /// </summary>
    /// <param name="trackingNumber">Tracking number</param>
    /// <returns>A list of shipment status events</returns>
    public async Task<IList<ShipmentStatusEvent>> GetShipmentEventsAsync(string trackingNumber)
    {
        var result = new List<ShipmentStatusEvent>();

        if (string.IsNullOrWhiteSpace(trackingNumber))
            return result;

        if (string.IsNullOrWhiteSpace(_settings.ClientId) || string.IsNullOrWhiteSpace(_settings.ClientSecret))
        {
            await _logger.WarningAsync("Royal Mail Tracking API: Client ID or Client Secret is not configured.");
            return result;
        }

        try
        {
            var baseUrl = _settings.UseSandbox
                ? RoyalMailDefaults.SandboxApiBaseUrl
                : RoyalMailDefaults.ProductionApiBaseUrl;

            var requestUrl = $"{baseUrl.TrimEnd('/')}/tracking/v2/events/{Uri.EscapeDataString(trackingNumber.Trim())}";

            using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            request.Headers.Add("X-IBM-Client-Id", _settings.ClientId.Trim());
            request.Headers.Add("X-IBM-Client-Secret", _settings.ClientSecret.Trim());
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Add("User-Agent", RoyalMailDefaults.UserAgent);

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                await _logger.ErrorAsync($"Royal Mail Tracking API error ({response.StatusCode}): {errorContent}");
                return result;
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var apiResponse = JsonSerializer.Deserialize<RoyalMailTrackingResponse>(responseJson, options);

            if (apiResponse?.MailPieces != null)
            {
                foreach (var piece in apiResponse.MailPieces)
                {
                    if (piece.Events != null)
                    {
                        foreach (var ev in piece.Events)
                        {
                            var statusName = !string.IsNullOrWhiteSpace(ev.EventName) ? ev.EventName : ev.EventCode ?? "Event";
                            var statusEvent = new ShipmentStatusEvent
                            {
                                EventName = statusName,
                                Status = statusName,
                                Location = ev.LocationName ?? piece.Summary?.StatusLocation ?? string.Empty,
                                CountryCode = "GB"
                            };

                            if (DateTime.TryParse(ev.EventDateTime, out var parsedDate))
                                statusEvent.Date = parsedDate;

                            result.Add(statusEvent);
                        }
                    }
                    else if (piece.Summary != null)
                    {
                        var statusEvent = new ShipmentStatusEvent
                        {
                            EventName = piece.Summary.StatusDescription ?? "Status summary",
                            Status = piece.Summary.StatusDescription ?? "Status summary",
                            Location = piece.Summary.StatusLocation ?? string.Empty,
                            CountryCode = "GB"
                        };

                        if (DateTime.TryParse(piece.Summary.StatusDateTime, out var parsedDate))
                            statusEvent.Date = parsedDate;

                        result.Add(statusEvent);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync($"Exception fetching Royal Mail tracking events for '{trackingNumber}': {ex.Message}", ex);
        }

        return result;
    }

    #endregion

    #region Nested DTO Classes

    private class RoyalMailTrackingResponse
    {
        [JsonPropertyName("mailPieces")]
        public List<MailPieceDto> MailPieces { get; set; }
    }

    private class MailPieceDto
    {
        [JsonPropertyName("mailPieceId")]
        public string MailPieceId { get; set; }

        [JsonPropertyName("summary")]
        public StatusSummaryDto Summary { get; set; }

        [JsonPropertyName("events")]
        public List<TrackingEventDto> Events { get; set; }
    }

    private class StatusSummaryDto
    {
        [JsonPropertyName("statusDescription")]
        public string StatusDescription { get; set; }

        [JsonPropertyName("statusLocation")]
        public string StatusLocation { get; set; }

        [JsonPropertyName("statusDateTime")]
        public string StatusDateTime { get; set; }
    }

    private class TrackingEventDto
    {
        [JsonPropertyName("eventCode")]
        public string EventCode { get; set; }

        [JsonPropertyName("eventName")]
        public string EventName { get; set; }

        [JsonPropertyName("eventDateTime")]
        public string EventDateTime { get; set; }

        [JsonPropertyName("locationName")]
        public string LocationName { get; set; }
    }

    #endregion
}
