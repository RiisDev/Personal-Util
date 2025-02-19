using System;
using System.Net.WebSockets;
using System.Text.Json;
using Script.LightweightDriver.InternalServices;

namespace Script.LightweightDriver.DriverHandler
{
    internal class DriverMethods(ClientWebSocket socket) : IDriverMethods
    {
        internal static string GetJavaScriptReturnValue(string message)
        {
            string[] split = message.Split("value\":");
            string data = split[1][1..];
            string toBeParsed = data[..data.LastIndexOf('"')];
            string parsedData = toBeParsed
                .Replace("\\n", "\n")  // New line
                .Replace("\\t", "\t")  // Tab
                .Replace("\\\"", "\"") // Escaped quote
                .Replace("\\\\", "\\");// Backslash
            return parsedData;
        }

        public async Task<string> GetPageTitleAsync()
        {
            int id = DriverService.GetRandomId();
            Dictionary<string, object> navigateEvent = new()
            {
                {"id", id },
                {"method", "Runtime.evaluate"},
                {"params", new Dictionary<string, string> {{"expression", "document.title"}}}
            };

            string dataReturn = await SocketService.SendAndWait(socket, JsonSerializer.Serialize(navigateEvent), id);
            string formattedString = GetJavaScriptReturnValue(dataReturn);
            return formattedString;
        }

        public async Task<string> GetPageBodyAsync()
        {
            int id = DriverService.GetRandomId();
            Dictionary<string, object> navigateEvent = new()
            {
                {"id", id },
                {"method", "Runtime.evaluate"},
                {"params", new Dictionary<string, string> {{"expression", "document.documentElement.outerHTML" } }}
            };

            string dataReturn = await SocketService.SendAndWait(socket, JsonSerializer.Serialize(navigateEvent), id);

            string formattedString = GetJavaScriptReturnValue(dataReturn);// Backslash
            return formattedString;
        }

        public async Task NavigateAsync(string url, bool javascriptNavigate = true)
        {
            Dictionary<string, object> navigateEvent = new()
            {
                { "id", DriverService.GetRandomId() },
            };

            if (javascriptNavigate)
            {
                navigateEvent.Add("method", "Runtime.evaluate");
                navigateEvent.Add("params", new Dictionary<string, string> {{ "expression", $"document.location.href = \"{url}\"" }});
            }
            else
            {
                navigateEvent.Add("method", "Page.navigate");
                navigateEvent.Add("params", new Dictionary<string, object> { { "url", url } });
            }

            await SocketService.SendAsync(socket, JsonSerializer.Serialize(navigateEvent));
        }

        public async Task ReloadAsync(bool javascriptReload = true)
        {
            Dictionary<string, object> navigateEvent = new()
            {
                { "id", DriverService.GetRandomId() },
            };

            if (javascriptReload)
            {
                navigateEvent.Add("method", "Runtime.evaluate");
                navigateEvent.Add("params", new Dictionary<string, string> { { "expression", "document.location.reload();" } });
            }
            else
            {
                navigateEvent.Add("method", "Page.reload");
            }

            await SocketService.SendAsync(socket, JsonSerializer.Serialize(navigateEvent));
        }

        public async Task NavigateForwardsAsync(bool javascriptNavigate = true)
        {
            throw new NotImplementedException();
        }

        public async Task NavigateBackwardsAsync(bool javascriptNavigate = true)
        {
            throw new NotImplementedException();
        }

        public async Task ExecuteScriptAsync(string script)
        {
            Dictionary<string, object> navigateEvent = new()
            {
                {"id", DriverService.GetRandomId() },
                {"method", "Runtime.evaluate"},
                {"params", new Dictionary<string, string> {{"expression", script}}}
            };
            
            await SocketService.SendAsync(socket, JsonSerializer.Serialize(navigateEvent));
        }
    }
}
