using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// Visar endast kända felmeddelandefält, aldrig hela API-svaret eller headers.
static class SafeApiError
{
    public static async Task<string> Read(HttpContent content, HttpRequestHeaders headers)
    {
        try
        {
            await using var stream = await content.ReadAsStreamAsync();
            var buffer = new byte[8193]; var length = 0;
            while (length < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(length));
                if (read == 0) break;
                length += read;
            }
            if (length > 8192) return "Tjänsten lämnade ett för stort felsvar för att visa säkert.";
            using var document = JsonDocument.Parse(buffer.AsMemory(0, length));
            var root = document.RootElement;
            var error = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("error", out var nested) ? nested : root;
            var parts = new List<string>();
            if (error.ValueKind == JsonValueKind.String) parts.Add(error.GetString()!);
            if (error.ValueKind == JsonValueKind.Object)
            {
                foreach (var name in new[] { "code", "message", "detail" })
                    if (error.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String) parts.Add(value.GetString()!);
                if (error.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array)
                    foreach (var detail in details.EnumerateArray().Take(3))
                        if (detail.ValueKind == JsonValueKind.Object)
                        {
                            // Endast felorsak; aldrig value/input/received som kan innehålla hemligheter.
                            foreach (var name in new[] { "message", "msg" })
                                if (detail.TryGetProperty(name, out var message) && message.ValueKind == JsonValueKind.String) parts.Add(message.GetString()!);
                        }
            }
            if (parts.Count == 0) return "Tjänsten gav ingen läsbar felorsak.";
            var text = string.Join(" · ", parts);
            if (headers.TryGetValues("x-api-key", out var keys))
                foreach (var key in keys.Where(key => !string.IsNullOrEmpty(key))) text = text.Replace(key, "[dold nyckel]", StringComparison.Ordinal);
            text = Regex.Replace(text, @"(?i)sk_[a-z0-9_-]+", "[dold nyckel]");
            text = Regex.Replace(text, @"(?i)bearer\s+\S+", "[dold autentisering]");
            text = Regex.Replace(text, @"(?i)https?://\S+", "[dold adress]");
            text = Regex.Replace(text, @"[\p{Cc}\p{Cf}\s]+", " ").Trim();
            return "Tjänstens fel: " + (text.Length <= 500 ? text : text[..500] + " …");
        }
        catch (JsonException) { return "Tjänsten lämnade inget läsbart JSON-fel."; }
    }
}
