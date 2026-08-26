using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Aqevryn.Common;

namespace Aqevryn.Sources;

public class HttpClientHelper
{
    private readonly HttpClient _httpClient;
    private readonly int _maxRetries;
    private readonly int _timeoutSeconds;

    public HttpClientHelper(int maxRetries = 3, int timeoutSeconds = 60)
    {
        _maxRetries = maxRetries;
        _timeoutSeconds = timeoutSeconds;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Aqevryn/1.0");
    }

    public async Task<string> FetchStringAsync(string url, Dictionary<string, string>? headers = null)
    {
        var retries = 0;
        while (true)
        {
            try
            {
                if (headers != null)
                {
                    foreach (var h in headers)
                        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(h.Key, h.Value);
                }
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex) when (retries < _maxRetries - 1)
            {
                retries++;
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retries)));
            }
        }
    }

    public async Task<JsonDocument> FetchJsonAsync(string url, Dictionary<string, string>? headers = null)
    {
        var content = await FetchStringAsync(url, headers);
        return JsonDocument.Parse(content);
    }
}