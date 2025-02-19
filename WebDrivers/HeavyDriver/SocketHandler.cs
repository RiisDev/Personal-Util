using Script.WebDrivers.LightweightDriver.DriverHandler;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using static System.Net.WebRequestMethods;

namespace Script.WebDrivers.HeavyDriver
{
    public class SocketHandler
    {
        public SocketHandler(ClientWebSocket socket, int port)
        {
            Socket = socket;
            DriverPort = port;
        }

        internal static ClientWebSocket Socket = null!;
        internal static int DriverPort;

        internal static async Task InitiatePageEvents(ClientWebSocket socket)
        {
            int fucked = -1;
            DriverHandler.OnFucked += () => fucked++;


            Random hookRandomizer = DriverService.GetRandom();

            List<Dictionary<string, object>> eventList =
            [
                new Dictionary<string, object>
                {
                    { "id", hookRandomizer.Next() },
                    { "method", "Page.enable" }
                },
                new Dictionary<string, object>
                {
                    { "id", hookRandomizer.Next() },
                    { "method", "Network.enable" }
                },
                new Dictionary<string, object>
                {
                    { "id", hookRandomizer.Next() },
                    { "method", "Network.setBlockedURLs" },
                    { "params", new Dictionary<string, object> {
                        { "urls", new[] {
                            "*i0.wp.com/*",
                            "*assets.nintendo.com/*",
                            "*images.vfl.ru",
                            "*nsw2u.net/wp-content/themes/poster/images/*",
                            "*nsw2u.net/wp-content/plugins*",
                            "*pixel.wp.com/*",

                        }}
                    }}
                },


                new Dictionary<string, object> // Fire this at the end so user can get expected output
                {
                    { "id", hookRandomizer.Next() },
                    { "method", "Page.reload" }
                },
            ];

            for (int eventId = 0; eventId < eventList.Count; eventId++)
            {
                await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(eventList[eventId]))), WebSocketMessageType.Text, true, CancellationToken.None);
                while (fucked != eventId) await Task.Delay(50);
            }

            await Task.Delay(1000);
        }

        internal static async Task NavigateTo(string url, string pageTitle, int port, ClientWebSocket socket, bool waitForPage = true)
        {
            Dictionary<string, object> dataToSend = new()
            {
                { "id",new Random((int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()).Next() },
                { "method", "Runtime.evaluate" },
                { "params", new Dictionary<string, string> { {"expression", $"document.location.href = \"{url}\""} }
                }
            };

            await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(dataToSend))), WebSocketMessageType.Text, true, CancellationToken.None);

            if (waitForPage)
                await DriverHandler.WaitForPage(pageTitle, port, socket, 999999);
        }

        internal static async Task<string?> ExecuteOnPageWithResponse(string pageTitle, int port, Dictionary<string, object> dataToSend, string expectedOutput, ClientWebSocket socket, bool output = false, bool skipCheck = false)
        {
            if (pageTitle != "") await DriverHandler.WaitForPage(pageTitle, port, socket);

            int id = (int)dataToSend["id"];
            TaskCompletionSource<string> tcs = new();
            DriverHandler.PendingRequests[id] = tcs;

            await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(dataToSend))), WebSocketMessageType.Text, true, CancellationToken.None);

            string response = await tcs.Task;

            if (output) Debug.WriteLine(response);
            if (!response.Contains(expectedOutput) && !skipCheck) throw new Exception("Expected output not found");

            return response;
        }
    }
}
