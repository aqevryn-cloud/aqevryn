using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aqevryn.Api;

public class ApiServer
{
    private readonly int _port;
    private readonly string _version;
    private readonly DateTime _startTime;

    public ApiServer(int port = 8000, string version = "0.1.0")
    {
        _port = port;
        _version = version;
        _startTime = DateTime.UtcNow;
    }

    public async Task StartAsync()
    {
        var listener = new System.Net.HttpListener();
        listener.Prefixes.Add($"http://+:{_port}/");
        try { listener.Start(); }
        catch
        {
            // Fallback to localhost only if + fails
            listener = new System.Net.HttpListener();
            listener.Prefixes.Add($"http://localhost:{_port}/");
            listener.Start();
        }

        Console.WriteLine($"API server listening on port {_port}");

        while (true)
        {
            try
            {
                var ctx = await listener.GetContextAsync();
                await HandleRequestAsync(ctx);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"API error: {ex.Message}");
            }
        }
    }

    private async Task HandleRequestAsync(System.Net.HttpListenerContext ctx)
    {
        ctx.Response.ContentType = "application/json";
        ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");

        var path = ctx.Request.Url?.AbsolutePath?.ToLower() ?? "/";

        try
        {
            switch (path)
            {
                case "/health":
                    await WriteJsonAsync(ctx.Response, new
                    {
                        status = "ok",
                        version = _version,
                        uptime_seconds = (int)(DateTime.UtcNow - _startTime).TotalSeconds,
                        timestamp = _startTime.ToString("o")
                    });
                    break;

                case "/status":
                    await WriteJsonAsync(ctx.Response, new
                    {
                        application = new
                        {
                            name = "Aqevryn",
                            version = _version,
                            uptime_seconds = (int)(DateTime.UtcNow - _startTime).TotalSeconds
                        },
                        pipeline = new { auto_publish = false, min_publication_score = 90 },
                        llm = new { provider = Environment.GetEnvironmentVariable("LLM_PROVIDER") ?? "not configured" }
                    });
                    break;

                default:
                    ctx.Response.StatusCode = 404;
                    await WriteJsonAsync(ctx.Response, new { error = "Not found", path });
                    break;
            }
        }
        catch (Exception ex)
        {
            ctx.Response.StatusCode = 500;
            await WriteJsonAsync(ctx.Response, new { error = ex.Message });
        }
        ctx.Response.Close();
    }

    private static async Task WriteJsonAsync(System.Net.HttpListenerResponse response, object data)
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
    }
}