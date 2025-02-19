using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json;
using Script.LightweightDriver.DriverHandler;
using Script.LightweightDriver.Events;
using static Script.LightweightDriver.InternalServices.SocketService;

namespace Script.LightweightDriver.InternalServices
{
    internal class EventsService
    {
        internal static async Task HookEvents(ClientWebSocket socket)
        {
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
                    { "method", "Console.enable" }
                },
                new Dictionary<string, object>
                {
                    { "id", hookRandomizer.Next() },
                    { "method", "Log.enable" }
                },


                new Dictionary<string, object> // Fire this at the end so user can get expected output
                {
                    { "id", hookRandomizer.Next() },
                    { "method", "Page.reload" }
                },
            ];

            foreach (Dictionary<string, object> @event in eventList)
                await SendAsync(socket, JsonSerializer.Serialize(@event));
        }

        internal static Task CheckForHandledEvent(string message)
        {

            if (message.Contains("Page."))
                return PageEvents.HandlePageEvent(message);

            return Task.CompletedTask;
        }

    }
}
