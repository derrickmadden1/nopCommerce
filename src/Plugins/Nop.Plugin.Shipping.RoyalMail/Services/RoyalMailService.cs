using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
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

            // Sanitize tracking number (alphanumeric only)
            var cleanTrackingNumber = new string(trackingNumber.Where(char.IsLetterOrDigit).ToArray());
            if (string.IsNullOrWhiteSpace(cleanTrackingNumber))
                return result;

            // Royal Mail Tracking API v2 endpoint: /mailpieces/v2/{mailPieceId}/events
            var requestUrl = $"{baseUrl.TrimEnd('/')}/mailpieces/v2/{Uri.EscapeDataString(cleanTrackingNumber)}/events";

            using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            request.Headers.Add("X-IBM-Client-Id", _settings.ClientId.Trim());
            request.Headers.Add("X-IBM-Client-Secret", _settings.ClientSecret.Trim());
            request.Headers.TryAddWithoutValidation("X-Accept-RMG-Terms", "yes");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Add("User-Agent", RoyalMailDefaults.UserAgent);

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                await _logger.WarningAsync($"Royal Mail Tracking API response ({response.StatusCode}) for '{trackingNumber}': {errorContent}");
                return result;
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            // Royal Mail v2 endpoint returns a "mailPieces" property that can be an object or an array of objects
            if (root.TryGetProperty("mailPieces", out var mailPiecesProp) || root.TryGetProperty("mailpieces", out mailPiecesProp))
            {
                if (mailPiecesProp.ValueKind == JsonValueKind.Object)
                {
                    ParseMailPieceElement(mailPiecesProp, result);
                }
                else if (mailPiecesProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var piece in mailPiecesProp.EnumerateArray())
                    {
                        ParseMailPieceElement(piece, result);
                    }
                }
            }
            else if (root.TryGetProperty("events", out var eventsProp) && eventsProp.ValueKind == JsonValueKind.Array)
            {
                ParseEventsArray(eventsProp, null, result);
            }
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync($"Exception fetching Royal Mail tracking events for '{trackingNumber}': {ex.Message}", ex);
        }

        return result;
    }

    #endregion

    #region Helpers

    private static void ParseMailPieceElement(JsonElement piece, List<ShipmentStatusEvent> result)
    {
        string summaryLocation = null;
        string summaryDescription = null;
        DateTime? summaryDate = null;

        if (piece.TryGetProperty("summary", out var summaryProp) && summaryProp.ValueKind == JsonValueKind.Object)
        {
            if (summaryProp.TryGetProperty("statusLocation", out var loc))
                summaryLocation = loc.GetString();
            if (summaryProp.TryGetProperty("statusDescription", out var desc))
                summaryDescription = desc.GetString();
            if (summaryProp.TryGetProperty("statusDateTime", out var dt) && DateTime.TryParse(dt.GetString(), out var parsedSummaryDate))
                summaryDate = parsedSummaryDate;
        }

        bool hasEvents = false;
        if (piece.TryGetProperty("events", out var eventsProp) && eventsProp.ValueKind == JsonValueKind.Array)
        {
            hasEvents = ParseEventsArray(eventsProp, summaryLocation, result);
        }

        if (!hasEvents && !string.IsNullOrWhiteSpace(summaryDescription))
        {
            result.Add(new ShipmentStatusEvent
            {
                EventName = summaryDescription,
                Status = summaryDescription,
                Location = summaryLocation ?? string.Empty,
                CountryCode = "GB",
                Date = summaryDate
            });
        }
    }

    private static bool ParseEventsArray(JsonElement eventsProp, string fallbackLocation, List<ShipmentStatusEvent> result)
    {
        bool found = false;
        foreach (var ev in eventsProp.EnumerateArray())
        {
            found = true;
            string eventName = null;
            if (ev.TryGetProperty("eventName", out var nameProp))
                eventName = nameProp.GetString();
            if (string.IsNullOrWhiteSpace(eventName) && ev.TryGetProperty("eventCode", out var codeProp))
                eventName = codeProp.GetString();
            if (string.IsNullOrWhiteSpace(eventName))
                eventName = "Event";

            string location = null;
            if (ev.TryGetProperty("locationName", out var locProp))
                location = locProp.GetString();
            if (string.IsNullOrWhiteSpace(location))
                location = fallbackLocation ?? string.Empty;

            DateTime? eventDate = null;
            if (ev.TryGetProperty("eventDateTime", out var dateProp) && DateTime.TryParse(dateProp.GetString(), out var parsedDate))
                eventDate = parsedDate;

            result.Add(new ShipmentStatusEvent
            {
                EventName = eventName,
                Status = eventName,
                Location = location,
                CountryCode = "GB",
                Date = eventDate
            });
        }

        return found;
    }

    #endregion
}
