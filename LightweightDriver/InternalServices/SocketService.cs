using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Script.LightweightDriver.InternalServices
{
    internal class SocketService
    {
        internal static readonly Dictionary<int, TaskCompletionSource<string>> PendingRequests = new();

        internal static Task HandleMessage(string message)
        {
            Debug.WriteLine(message);

            Task.Run(async() => { await EventsService.CheckForHandledEvent(message); });

            Dictionary<string, object>? json = JsonSerializer.Deserialize<Dictionary<string, object>>(message);

            if (json == null || !json.ContainsKey("id")) return Task.CompletedTask;

            int id = int.Parse(json["id"].ToString()!);
            if (!PendingRequests.TryGetValue(id, out TaskCompletionSource<string>? tcs)) return Task.CompletedTask;

            tcs.SetResult(message);
            PendingRequests.Remove(id);

            return Task.CompletedTask;
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

        internal static async Task<string> SendAndWait(ClientWebSocket socket, string message, int id)
        {
            PendingRequests[id] = new TaskCompletionSource<string>();
            await SendAsync(socket, message);
            return await PendingRequests[id].Task;
        }

        internal static async Task SendAsync(ClientWebSocket? socket, string message)
        {
            if (socket is null || socket.State != WebSocketState.Open) return;

            byte[] buffer = Encoding.UTF8.GetBytes(message);
            await socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
        }

    }
}
