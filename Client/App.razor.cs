using Toolbelt.Blazor;

namespace HttpClientErrorApp.Client;

public partial class App
{
    private record ActionType(string Case, Func<Task> Action);

    private readonly IEnumerable<ActionType> Actions;

    private record LogLine(DateTime? TimeStamp, string Text);

    private readonly List<LogLine> LogLines = new() { new(DateTime.Now, "Hello") };

    private const int MaxLogLines = 20;

    private bool Sending = false;

    public App()
    {
        this.Actions = new ActionType[]
        {
            new ("Server response timed out", this.ServerResponseTimedOut),
            new ("Server response HTTP 500",  this.ServerRespondHTTP500),
            new ("Server is unreachable",     this.ServerIsUnreachable),
            new ("Server exists but is not listening", this.ServerNotListen),
            new ("Server respond CORS error", this.ServerRespondCORSError),
        };
    }

    protected override void OnInitialized()
    {
        this.Interceptor.BeforeSend += this.Interceptor_BeforeSend;
        this.Interceptor.AfterSend += this.Interceptor_AfterSend;
    }

    private void Interceptor_BeforeSend(object? sender, HttpClientInterceptorEventArgs e)
    {
        this.Sending = true;
        this.WriteLine($"BEFORE SEND HTTP {e.Request.Method} {e.Request.RequestUri}");
    }

    private void Interceptor_AfterSend(object? sender, HttpClientInterceptorEventArgs e)
    {
        // Server response timed out     ➡ e.Response is null
        // Server process is not strated ➡ e.Response is null
        // Server respond HTTP 500       ➡ e.Response.StatusCode is "InternalServerError"
        // Server is unreachable         ➡ e.Response is null
        // Server is not listening       ➡ e.Response is null
        // Server respond CORS error     ➡ e.Response is null
        // Offline                       ➡ e.Response is null
        this.Sending = false;
        this.WriteLine($"AFTER  SEND HTTP {e.Request.Method} {e.Request.RequestUri} -> {(e.Response == null ? "e.Response is null" : "HTTP " + e.Response.StatusCode.ToString())}");
    }

    // Server response timed out     ➡                                   "TaskCanceledException: The request was canceled due to the configured HttpClient.Timeout of 5 seconds elapsing."
    // Server process is not strated ➡ net::ERR_CONNECTION_REFUSED    ⇒ "HttpRequestException: TypeError: Failed to fetch"
    // Offline                       ➡ net::ERR_INTERNET_DISCONNECTED ⇒ "HttpRequestException: TypeError: Failed to fetch"
    Task ServerResponseTimedOut() => this.HttpGet("/api/server-response-timed-out");

    // Server respond HTTP 500       ➡                                   "HttpRequestException: Response status code does not indicate success: 500 (Internal Server Error)."
    Task ServerRespondHTTP500() => this.HttpGet("/api/server-response-http500");

    // Server is unreachable         ➡                                   "TaskCanceledException: The request was canceled due to the configured HttpClient.Timeout of 5 seconds elapsing."
    Task ServerIsUnreachable() => this.HttpGet("https://10.0.0.1/api/server-isnot-reachable");

    // Server is not listening       ➡ net::ERR_CONNECTION_REFUSED    ⇒ "HttpRequestException: TypeError: Failed to fetch"
    Task ServerNotListen() => this.HttpGet("https://192.168.11.1/api/server-isnot-listen");

    // Server respond CORS error     ➡ net::ERR_FAILED 404            ⇒ "HttpRequestException: TypeError: Failed to fetch"
    Task ServerRespondCORSError() => this.HttpGet("https://8.8.8.8/api/server-response-CORS-error");

    private async Task HttpGet(string url)
    {
        try
        {
            this.WriteLine();
            this.WriteLine($"BEGIN HTTP GET {url}...");
            await this.HttpClient.GetStringAsync(url);
            this.WriteLine($"END   HTTP GET {url}");
        }
        catch (Exception e)
        {
            this.WriteLine($"ERROR HTTP GET {url}");
            this.WriteError(e);
        }
    }

    private void WriteError(Exception e, int indent = 0)
    {
        var indentSpace = new string(' ', count: indent * 2);
        this.WriteLine(indentSpace + $"{e.GetType().FullName}: {e.Message}");
        if (e.InnerException != null) this.WriteError(e.InnerException, indent + 1);
    }

    private void WriteLine() => this.WriteLine(timeStamp: null, "");

    private void WriteLine(string text) => this.WriteLine(timeStamp: DateTime.Now, text);

    private void WriteLine(DateTime? timeStamp, string text)
    {
        this.LogLines.Add(new(timeStamp, text));
        while (this.LogLines.Count > MaxLogLines) this.LogLines.RemoveAt(0);
        this.StateHasChanged();
    }
}