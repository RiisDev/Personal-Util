using Script.Util;
using Script.WebDrivers.HeavyDriver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Script.Bypasses
{
    public class BypassVip
    {
        private static readonly int Port = Win32.GetFreePort();
        private readonly DriverHandler _handler = new();
        public async Task Bypass(string url)
        {
            await _handler.StartDriver(@"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe", Port);
            await SocketHandler.NavigateTo($"https://bypass.city/bypass?bypass={url}", "Bypass in Progress", Port, _handler.Socket, true);
        }
    }
}
