using System.Diagnostics;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Script.WebDrivers.HeavyDriver;

public class DriverHandler
{
    public delegate void FuckMe();

    public static event FuckMe? OnFucked;

    internal ClientWebSocket Socket = null!;
    internal SocketHandler SocketHandler = null!;

    internal static readonly Dictionary<int, TaskCompletionSource<string>> PendingRequests = new();

    internal static event Events.FrameChangedEvent? OnFrameNavigation;
    internal static event Events.FrameChangedEvent? OnFrameLoaded;
    internal static event Events.FrameChangedEvent? OnDocumentNavigate;

    internal static readonly Regex FrameNavigatedRegex = new("\"id\":\"([^\"]+)\".*?\"url\":\"([^\"]+)\"", RegexOptions.Compiled);
    internal static readonly Regex NavigatedWithinDocument = new("\"frameId\":\"([^\"]+)\".*?\"url\":\"([^\"]+)\"", RegexOptions.Compiled);
    internal static readonly Regex FrameStoppedLoadingRegex = new("\"frameId\":\"([^\"]+)\"", RegexOptions.Compiled);

    internal static void DoDriverCheck(string browserProcess, string browserExecutable, bool killBrowser)
    {
        List<Process> browserProcesses = Process.GetProcessesByName(browserProcess).ToList();

        if (browserProcesses.Any() && !killBrowser)
            throw new Exception($"{browserProcesses.First().ProcessName} is currently running, it must be closed or Initialize must be started with 'true'");
        if (browserProcesses.Any() && killBrowser)
            browserProcesses.ToList().ForEach(x => x.Kill());

        if (!File.Exists(browserExecutable))
            throw new Exception($"Browser executable not found at {browserExecutable}");
    }

    internal static Task CheckForEvent(string message)
    {
        Match match;
        switch (message)
        {
            case var _ when message.Contains("Page.frameNavigated"):
                match = FrameNavigatedRegex.Match(message);
                if (match.Success)
                    OnFrameNavigation?.Invoke(match.Groups[2].Value, match.Groups[1].Value);
                break;
            case var _ when message.Contains("Page.frameStoppedLoading"):
                match = FrameStoppedLoadingRegex.Match(message);
                if (match.Success)
                    OnFrameLoaded?.Invoke(null, match.Groups[1].Value);
                break;
            case var _ when message.Contains("navigatedWithinDocument"):
                match = NavigatedWithinDocument.Match(message);
                if (match.Success)
                    OnDocumentNavigate?.Invoke(match.Groups[2].Value, match.Groups[1].Value);
                break;
        }

        return Task.CompletedTask;
    }

    internal static async Task HandleMessage(string message)
    {
        await CheckForEvent(message);

        if (OnFucked is not null && message.Contains("\"result\":{}}")) OnFucked.Invoke();

        Dictionary<string, object>? json = JsonSerializer.Deserialize<Dictionary<string, object>>(message);

        if (json == null || !json.ContainsKey("id")) return;

        int id = int.Parse(json["id"].ToString()!);
        if (!PendingRequests.TryGetValue(id, out TaskCompletionSource<string>? tcs)) return;

        tcs.SetResult(message);
        PendingRequests.Remove(id);
    }

    internal static async Task ListenAsync(ClientWebSocket? socket)
    {
        byte[] buffer = new byte[8192];

        while (socket is not null && socket.State == WebSocketState.Open)
        {
            using MemoryStream memoryStream = new();
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                memoryStream.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            await HandleMessage(Encoding.UTF8.GetString(memoryStream.ToArray()));
        }
    }


    internal static async Task<string?> WaitForPage(string title, int port, ClientWebSocket? socket, int maxRetries = 250, bool needsReturn = false)
    {
        using HttpClient httpClient = new();
        int retries = 0;
        string foundSocket = "";
        do
        {
            retries++;
            List<EdgeDev>? debugResponse = await httpClient.GetFromJsonAsync<List<EdgeDev>>($"http://localhost:{port}/json");

            switch (debugResponse)
            {
                case null:
                case var _ when debugResponse.Count == 0:
                case var _ when !debugResponse.Any(x => x.Title.Contains(title)):
                    continue;
            }

            if (needsReturn) foundSocket = debugResponse.First(x => x.Title.Contains(title)).WebSocketDebuggerUrl;

            break;

        } while (retries <= maxRetries);

        return foundSocket;
    }

    internal static async Task<string?> GetPageUrl(int port)
    {
        using HttpClient httpClient = new();
        List<EdgeDev>? debugResponse = await httpClient.GetFromJsonAsync<List<EdgeDev>>($"http://localhost:{port}/json");
        return debugResponse?.FirstOrDefault(x => x.Type == "page")?.Url;
    }

    internal static async Task<string?> GetPageTitle(int port)
    {
        using HttpClient httpClient = new();
        List<EdgeDev>? debugResponse = await httpClient.GetFromJsonAsync<List<EdgeDev>>($"http://localhost:{port}/json");
        return debugResponse?.FirstOrDefault(x => x.Type == "page")?.Title;
    }

    internal static async Task<bool> PageExists(string pageTitle, int port)
    {
        using HttpClient httpClient = new();
        int retries = 0;
        do
        {
            retries++;
            List<EdgeDev>? debugResponse = await httpClient.GetFromJsonAsync<List<EdgeDev>>($"http://localhost:{port}/json");

            if (debugResponse is null) continue;
            if (debugResponse.Count == 0) continue;
            if (debugResponse.Any(x => x.Title.Contains(pageTitle))) break;
        } while (retries <= 150);

        return retries < 150;
    }

    internal async Task<(Process?, string)> StartDriver(string browserExecutable, int port)
    {
        DoDriverCheck(Path.GetFileNameWithoutExtension(browserExecutable), browserExecutable, true);

        ProcessStartInfo processInfo = new()
        {
            FileName = browserExecutable,
            Arguments = $"--remote-debugging-port={port} --incognito --disable-gpu --disable-extensions --disable-hang-monitor --disable-breakpad --disable-client-side-phishing-detection --no-sandbox --disable-site-isolation-trials --disable-features=IsolateOrigins,SitePerProcess --disable-accelerated-2d-canvas --disable-accelerated-compositing --disable-smooth-scrolling --disable-application-cache --disable-background-networking --disable-site-engagement --disable-webgl --disable-predictive-service --disable-perf --disable-media-internals --disable-ppapi --disable-software-rasterizer https://www.google.com/",
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Minimized
        };

        Process? driverProcess = Process.Start(processInfo);

        Debug.WriteLine($"Debug: http://localhost:{port}/json");

        //Task.Run(() => Win32.HideDriver(driverProcess!)); // Todo make sure this isn't just spammed, find a way to detect if it's hidden already
        AppDomain.CurrentDomain.ProcessExit += (_, _) => driverProcess?.Kill();

        string? socketUrl = await WaitForPage("Google", port, null, 999999, true);

        if (string.IsNullOrEmpty(socketUrl))
            throw new Exception("Failed to start driver");


        Socket = new ClientWebSocket();
        await Socket.ConnectAsync(new Uri(socketUrl), CancellationToken.None);
        SocketHandler = new SocketHandler(Socket, port);

        Task.Run(() => ListenAsync(Socket));

        await SocketHandler.InitiatePageEvents(Socket);

        return (driverProcess, socketUrl);
    }
}

public record Cookie(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("domain")] string Domain,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("expires")] double? Expires,
    [property: JsonPropertyName("size")] int? Size,
    [property: JsonPropertyName("httpOnly")] bool? HttpOnly,
    [property: JsonPropertyName("secure")] bool? Secure,
    [property: JsonPropertyName("session")] bool? Session,
    [property: JsonPropertyName("priority")] string Priority,
    [property: JsonPropertyName("sameParty")] bool? SameParty,
    [property: JsonPropertyName("sourceScheme")] string SourceScheme,
    [property: JsonPropertyName("sourcePort")] int? SourcePort,
    [property: JsonPropertyName("sameSite")] string SameSite,
    [property: JsonPropertyName("partitionKey")] PartitionKey PartitionKey
);

public record PartitionKey(
    [property: JsonPropertyName("topLevelSite")] string TopLevelSite,
    [property: JsonPropertyName("hasCrossSiteAncestor")] bool? HasCrossSiteAncestor
);

internal record Result(
    [property: JsonPropertyName("cookies")] IReadOnlyList<Cookie> Cookies
);

internal record CookieRoot(
    [property: JsonPropertyName("id")] int? Id,
    [property: JsonPropertyName("result")] Result Result
);


internal record EdgeDev(
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("devtoolsFrontendUrl")] string DevtoolsFrontendUrl,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("webSocketDebuggerUrl")] string WebSocketDebuggerUrl,
    [property: JsonPropertyName("parentId")] string ParentId
);