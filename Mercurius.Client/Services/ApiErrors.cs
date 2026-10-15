using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Mercurius.Client.Services
{
    // Turns a failed API response into a message fit for the page: the API's own { error = "..." }
    // text when it sent one, else the caller's fallback.
    public static class ApiErrors
    {
        public static async Task<string> ReadAsync(HttpResponseMessage response, string fallback)
        {
            if (response.StatusCode == HttpStatusCode.Forbidden) return "You don't have permission to do that.";
            try
            {
                var body = await response.Content.ReadFromJsonAsync<JsonElement>();
                if (body.ValueKind == JsonValueKind.Object)
                {
                    if (body.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
                    {
                        return error.GetString() ?? fallback;
                    }
                    // Model-binding failures come back as ProblemDetails: { title, errors: { field: [msg] } }.
                    if (body.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var field in errors.EnumerateObject())
                        {
                            if (field.Value.ValueKind == JsonValueKind.Array && field.Value.GetArrayLength() > 0)
                            {
                                return field.Value[0].GetString() ?? fallback;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
            {
                // Not a JSON body (e.g. a 413 from the server's request size limit).
            }
            return response.StatusCode == HttpStatusCode.RequestEntityTooLarge ? "The file is too large." : fallback;
        }
    }
}
