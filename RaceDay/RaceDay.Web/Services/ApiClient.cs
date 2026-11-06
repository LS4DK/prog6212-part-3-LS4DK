using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RaceDay.Web.Services;

/// <summary>The outcome of one API call: the status, the data (if it worked) or a friendly error message (if it didn't).</summary>
public class ApiResponse<T>
{
    public HttpStatusCode StatusCode { get; init; }
    public T? Data { get; init; }
    public string? Error { get; init; }

    public bool Success => (int)StatusCode >= 200 && (int)StatusCode < 300;
    public bool Unauthorized => StatusCode == HttpStatusCode.Unauthorized;
    public bool Forbidden => StatusCode == HttpStatusCode.Forbidden;
    public bool NotFound => StatusCode == HttpStatusCode.NotFound;
}

/// <summary>
/// The only way the website reaches data: every call goes to the RaceDay API.
/// It attaches the logged-in user's API session cookie, so the API knows who is asking.
/// </summary>
public class ApiClient
{
    private const string ApiCookieName = ".RaceDay.Session";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() } // the API sends enums as names, e.g. "Run"
    };

    private readonly HttpClient _http;
    private readonly UserSession _session;
    private readonly ILogger<ApiClient> _logger;

    public ApiClient(HttpClient http, UserSession session, ILogger<ApiClient> logger)
    {
        _http = http;
        _session = session;
        _logger = logger;
    }

    public Task<ApiResponse<T>> GetAsync<T>(string url) =>
        SendAsync<T>(HttpMethod.Get, url, null);

    public Task<ApiResponse<T>> PostAsync<T>(string url, object? body = null) =>
        SendAsync<T>(HttpMethod.Post, url, body == null ? null : JsonContent.Create(body, options: JsonOptions));

    public Task<ApiResponse<T>> PutAsync<T>(string url, object body) =>
        SendAsync<T>(HttpMethod.Put, url, JsonContent.Create(body, options: JsonOptions));

    public Task<ApiResponse<object>> DeleteAsync(string url) =>
        SendAsync<object>(HttpMethod.Delete, url, null);

    /// <summary>Sends an image to an API upload endpoint as multipart form data.</summary>
    public async Task<ApiResponse<T>> UploadAsync<T>(string url, IFormFile file)
    {
        using var content = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream();

        var fileContent = new StreamContent(stream);
        var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", file.FileName);

        return await SendAsync<T>(HttpMethod.Post, url, content);
    }

    private async Task<ApiResponse<T>> SendAsync<T>(HttpMethod method, string url, HttpContent? content)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };

        if (!string.IsNullOrEmpty(_session.ApiCookie))
            request.Headers.TryAddWithoutValidation("Cookie", $"{ApiCookieName}={_session.ApiCookie}");

        try
        {
            using var response = await _http.SendAsync(request);

            // After login the API hands us its session cookie: keep it for this user
            var newCookie = ExtractSessionCookie(response);
            if (newCookie != null)
                _session.SetApiCookie(newCookie);

            var body = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                T? data = default;
                if (!string.IsNullOrWhiteSpace(body))
                    data = JsonSerializer.Deserialize<T>(body, JsonOptions);

                return new ApiResponse<T> { StatusCode = response.StatusCode, Data = data };
            }

            // The API no longer recognises this session (for example it expired): log the user out here too
            if (response.StatusCode == HttpStatusCode.Unauthorized && _session.IsLoggedIn)
                _session.SignOut();

            return new ApiResponse<T>
            {
                StatusCode = response.StatusCode,
                Error = ReadError(body) ?? FriendlyMessage(response.StatusCode)
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Could not reach the API for {Method} {Url}", method, url);
            return Unavailable<T>(HttpStatusCode.ServiceUnavailable);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "The API took too long to answer {Method} {Url}", method, url);
            return Unavailable<T>(HttpStatusCode.GatewayTimeout);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "The API sent a response we could not read for {Method} {Url}", method, url);
            return Unavailable<T>(HttpStatusCode.BadGateway);
        }
    }

    private static ApiResponse<T> Unavailable<T>(HttpStatusCode status) => new()
    {
        StatusCode = status,
        Error = "The RaceDay service is unavailable right now. Please try again in a moment."
    };

    private static string? ExtractSessionCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var headers))
            return null;

        var prefix = ApiCookieName + "=";
        foreach (var header in headers)
        {
            if (!header.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            var end = header.IndexOf(';');
            return end < 0 ? header[prefix.Length..] : header[prefix.Length..end];
        }

        return null;
    }

    // The API reports problems as { "message": "..." } or as standard validation errors
    private static string? ReadError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            if (root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                return message.GetString();

            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                var messages = errors.EnumerateObject()
                    .SelectMany(p => p.Value.EnumerateArray().Select(e => e.GetString()))
                    .Where(s => !string.IsNullOrWhiteSpace(s));
                var joined = string.Join(" ", messages);
                if (joined.Length > 0)
                    return joined;
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            // fall through to the friendly default
        }

        return null;
    }

    private static string FriendlyMessage(HttpStatusCode status) => status switch
    {
        HttpStatusCode.BadRequest => "Please check the details and try again.",
        HttpStatusCode.Unauthorized => "Please log in to continue.",
        HttpStatusCode.Forbidden => "You are not allowed to do that.",
        HttpStatusCode.NotFound => "We couldn't find what you were looking for.",
        HttpStatusCode.Conflict => "That clashes with something that already exists.",
        _ => "Something went wrong on our side. Please try again."
    };
}