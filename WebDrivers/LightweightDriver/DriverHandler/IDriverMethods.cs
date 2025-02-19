using System.Net.WebSockets;

namespace Script.WebDrivers.LightweightDriver.DriverHandler
{
    internal interface IDriverMethods
    {
        Task<string> GetPageTitleAsync();

        Task<string> GetPageBodyAsync();

        Task NavigateAsync(string url, bool javascriptNavigate = true);

        Task ReloadAsync(bool javascriptReload);

        Task NavigateForwardsAsync(bool javascriptNavigate = true);

        Task NavigateBackwardsAsync(bool javascriptNavigate = true);

        Task ExecuteScriptAsync(string script);
    }
}
