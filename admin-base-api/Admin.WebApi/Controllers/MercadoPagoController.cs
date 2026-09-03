using Admin.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text;

namespace Admin.WebApi.Controllers
{
    [ApiController]
    [Route("api/mercadopago")]
    public class MercadoPagoController : ControllerBase
    {
        private readonly ISubscriptionService _subscriptionService;
        private readonly IPhotoCheckoutService _photoCheckoutService;
        private readonly ILogger<MercadoPagoController> _logger;

        public MercadoPagoController(ISubscriptionService subscriptionService, IPhotoCheckoutService photoCheckoutService, ILogger<MercadoPagoController> logger)
        {
            _subscriptionService = subscriptionService;
            _photoCheckoutService = photoCheckoutService;
            _logger = logger;
        }

        [AllowAnonymous]
        [HttpPost("notify")]
        public async Task<IActionResult> Notify([FromQuery] string? topic, [FromQuery] string? type, [FromQuery] long? id)
        {
            var payload = await TryReadJsonBodyAsync();
            var resolvedTopic = ResolveTopic(topic, type, payload);
            var resolvedId = ResolveId(id, payload);

            if (string.IsNullOrWhiteSpace(resolvedTopic) || resolvedId <= 0)
            {
                _logger.LogWarning("Webhook MP ignorado por payload inválido. Topic={Topic}, Type={Type}, Id={Id}, Payload={Payload}", topic, type, id, payload.ToString());
                return Ok();
            }

            var resolvedIdValue = resolvedId.GetValueOrDefault();

            try
            {
                await _subscriptionService.ProcessMercadoPagoNotificationAsync(resolvedTopic, resolvedIdValue);
                await _photoCheckoutService.ProcessMercadoPagoNotificationAsync(resolvedTopic, resolvedIdValue);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando webhook de MercadoPago. Topic={Topic}, Id={Id}", resolvedTopic, resolvedId);
                return Ok();
            }
        }

        private async Task<JsonElement?> TryReadJsonBodyAsync()
        {
            if (Request.ContentLength is null || Request.ContentLength == 0)
                return null;

            if (Request.Body == null)
                return null;

            Request.EnableBuffering();

            using var reader = new StreamReader(Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            Request.Body.Position = 0;

            if (string.IsNullOrWhiteSpace(body))
                return null;

            try
            {
                using var document = JsonDocument.Parse(body);
                return document.RootElement.Clone();
            }
            catch
            {
                return null;
            }
        }

        private static string? ResolveTopic(string? topic, string? type, JsonElement? payload)
        {
            var value = FirstNonEmpty(topic, type);
            if (!string.IsNullOrWhiteSpace(value))
                return value;

            if (TryGetStringProperty(payload, "topic", out var topicValue))
                return topicValue;

            if (TryGetStringProperty(payload, "type", out var typeValue))
                return typeValue;

            return null;
        }

        private static long? ResolveId(long? queryId, JsonElement? payload)
        {
            if (queryId.HasValue && queryId.Value > 0)
                return queryId.Value;

            if (TryGetLongProperty(payload, "id", out var idValue))
                return idValue;

            if (payload.HasValue
                && payload.Value.ValueKind == JsonValueKind.Object
                && payload.Value.TryGetProperty("data", out var data)
                && TryGetLongProperty(data, "id", out var dataId))
            {
                return dataId;
            }

            return null;
        }

        private static bool TryGetStringProperty(JsonElement? source, string propertyName, out string? value)
        {
            value = null;

            if (!source.HasValue || source.Value.ValueKind != JsonValueKind.Object || !source.Value.TryGetProperty(propertyName, out var property))
                return false;

            if (property.ValueKind == JsonValueKind.String)
            {
                value = property.GetString();
                return !string.IsNullOrWhiteSpace(value);
            }

            return false;
        }

        private static bool TryGetLongProperty(JsonElement? source, string propertyName, out long value)
        {
            value = 0;

            if (!source.HasValue || source.Value.ValueKind != JsonValueKind.Object || !source.Value.TryGetProperty(propertyName, out var property))
                return false;

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var numericValue) && numericValue > 0)
            {
                value = numericValue;
                return true;
            }

            if (property.ValueKind == JsonValueKind.String && long.TryParse(property.GetString(), out var stringValue) && stringValue > 0)
            {
                value = stringValue;
                return true;
            }

            return false;
        }

        private static bool TryGetLongProperty(JsonElement source, string propertyName, out long value)
        {
            value = 0;

            if (source.ValueKind != JsonValueKind.Object || !source.TryGetProperty(propertyName, out var property))
                return false;

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var numericValue) && numericValue > 0)
            {
                value = numericValue;
                return true;
            }

            if (property.ValueKind == JsonValueKind.String && long.TryParse(property.GetString(), out var stringValue) && stringValue > 0)
            {
                value = stringValue;
                return true;
            }

            return false;
        }

        private static string? FirstNonEmpty(params string?[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return null;
        }
    }
}
