using System.Net;

namespace Script.Util.Expanders
{
    public static class WebUtil
    {
        public static (HttpClient, CookieContainer) BuildClient()
        {
            CookieContainer container = new();
            HttpClient client = new(new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
                CookieContainer = container,
                AllowAutoRedirect = true
            });

            return (client, container);
        }

        // Why is this not a base Uri method??
        public static string GetRootDomain(string url)
        {
            Uri uri = new(url);
            string host = uri.Host;
            string[] parts = host.Split('.');

            return parts.Length > 2 && parts[^2].Length <= 3
                ? string.Join(".", parts[^3..])
                : string.Join(".", parts[^2..]);
        }

        public static string GetRootDomain(this Uri uri) => GetRootDomain(uri.AbsoluteUri); // Just to make it easier to invoke ig
    }
}
