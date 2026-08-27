using System.Text.Json;
using Aqevryn.Common;

namespace Aqevryn.Sources;

public class HttpClientHelper
{
    private readonly int _maxRetries;
    private readonly int _timeoutSeconds;

    public HttpClientHelper(int maxRetries = 3, int timeoutSeconds = 30)
    {
        _maxRetries = maxRetries;
        _timeoutSeconds = timeoutSeconds;
    }

    public async Task<string> FetchStringAsync(string url, Dictionary<string, string>? headers = null)
    {
        var lastException = (Exception?)null;

        for (var retry = 0; retry < _maxRetries; retry++)
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(_timeoutSeconds) };
                http.DefaultRequestHeaders.Add("User-Agent", "Aqevryn/1.0");

                if (headers != null)
                {
                    foreach (var h in headers)
                        http.DefaultRequestHeaders.TryAddWithoutValidation(h.Key, h.Value);
                }

                var response = await http.GetAsync(url);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex) when (retry < _maxRetries - 1)
            {
                lastException = ex;
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retry)));
            }
            catch (Exception ex)
            {
                throw new SourceError($"Request to {url} failed after {_maxRetries} attempts: {ex.Message}", ex);
            }
        }

        throw new SourceError($"Request to {url} failed after {_maxRetries} attempts: {lastException?.Message ?? "unknown error"}");
    }

    public async Task<JsonDocument> FetchJsonAsync(string url, Dictionary<string, string>? headers = null)
    {
        var content = await FetchStringAsync(url, headers);
        return JsonDocument.Parse(content);
    }
}