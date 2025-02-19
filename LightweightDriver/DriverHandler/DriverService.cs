using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using Script.LightweightDriver.InternalServices;

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

namespace Script.LightweightDriver.DriverHandler
{
    public record CreationReturn(
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("devtoolsFrontendUrl")] string DevtoolsFrontendUrl,
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("url")] string Url,
        [property: JsonPropertyName("webSocketDebuggerUrl")] string WebSocketDebuggerUrl
    );

    internal class DriverService
    {

        internal static int GetRandomId() => new Random((int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()).Next();
        internal static Random GetRandom() => new((int)DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        internal static int FreeTcpPort()
        {
            TcpListener l = new(IPAddress.Loopback, 0);
            l.Start();
            int port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }

        internal static async Task<ClientWebSocket?> StartDriver(string browserExecutable, int port, string starterUrl = "https://google.ca/")
        {
            if (!Uri.IsWellFormedUriString(starterUrl, UriKind.RelativeOrAbsolute))
                throw new ArgumentException("Invalid URL", nameof(starterUrl));

            using HttpClient client = new();
            await StartBrowserProcess(browserExecutable, port);
            HttpResponseMessage response = await CreateBrowserPageAsync(client, port, starterUrl);
            CreationReturn creationReturn = await GetCreationReturnAsync(response);
            ClientWebSocket webSocket = await ConnectWebSocketAsync(creationReturn.WebSocketDebuggerUrl, starterUrl);

            return webSocket;
        }

        private static async Task StartBrowserProcess(string browserExecutable, int port)
        {
            Process.GetProcessesByName("msedge").ToList().ForEach(p => p.Kill());

            string? wsUrl = null;

            ProcessStartInfo processInfo = new()
            {
                FileName = browserExecutable,
                Arguments = $"--remote-debugging-port={port} --headless=chrome --incognito --window-size=1280,1280 --enable-javascript --ignore-certificate-errors --enable-features=NetworkService --no-sandbox --disable-gpu --disable-dev-shm-usage",
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Minimized,
                RedirectStandardError = true
            };

            Process driverProcess = Process.Start(processInfo)
                ?? throw new Exception("Browser process could not be created");

            AppDomain.CurrentDomain.ProcessExit += (_, _) => driverProcess.Kill();
            driverProcess.Exited += (_, _) => throw new Exception("Browser process exited unexpectedly");
            driverProcess.ErrorDataReceived += (_, e) => wsUrl = e.Data?.Split(" ").Last();
            driverProcess.BeginErrorReadLine();

            while (string.IsNullOrEmpty(wsUrl)) await Task.Delay(15);
        }

        private static async Task<HttpResponseMessage> CreateBrowserPageAsync(HttpClient client, int port, string starterUrl)
        {
            HttpResponseMessage response = await client.PutAsync($"http://localhost:{port}/json/new?{starterUrl}", new StringContent(""));

            if (!response.IsSuccessStatusCode)
                throw new Exception("Failed to create browser page");

            return response;
        }

        private static async Task<CreationReturn> GetCreationReturnAsync(HttpResponseMessage response)
        {
            string content = await response.Content.ReadAsStringAsync();
            CreationReturn? creationReturn = JsonSerializer.Deserialize<CreationReturn>(content);

            if (creationReturn is null)
                throw new Exception("Failed to deserialize page creation socket");

            return creationReturn;
        }

        private static async Task<ClientWebSocket> ConnectWebSocketAsync(string webSocketDebuggerUrl, string starterUrl)
        {
            ClientWebSocket webSocket = new()
            {
                Options = { RemoteCertificateValidationCallback = (_, _, _, _) => true }
            };

            await webSocket.ConnectAsync(new Uri(webSocketDebuggerUrl), default);

            Task.Run(() => SocketService.ListenAsync(webSocket));

            await EventsService.HookEvents(webSocket);

            return webSocket;
        }

    }
}
