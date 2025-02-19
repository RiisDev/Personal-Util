using System.Net.Security;
using System.Text.RegularExpressions;

namespace Script.Bypasses
{
    internal class OuOBypass
    {
        HttpClientHandler handler = new()
        {
            SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13,
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };
        private static readonly HttpClient Client = new();

        public async Task<string> RecaptchaV3Async()
        {
            const string anchorUrl = "https://www.google.com/recaptcha/api2/anchor?ar=1&k=6Lcr1ncUAAAAAH3cghg6cOTPGARa8adOf-y9zv2x&co=aHR0cHM6Ly9vdW8ucHJlc3M6NDQz&hl=en&v=pCoGBhjs9s8EhFOHJFe8cqis&size=invisible&cb=ahgyd1gkfkhe";
            string urlBase = "https://www.google.com/recaptcha/";

            Match matches = Regex.Match(anchorUrl, @"([api2|enterprise]+)/anchor\?(.*)");
            urlBase += matches.Groups[1].Value + "/";
            string paramsString = matches.Groups[2].Value;

            Client.DefaultRequestHeaders.Clear();
            Client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/x-www-form-urlencoded");

            HttpResponseMessage res = await Client.GetAsync(urlBase + "anchor?" + paramsString);
            string resContent = await res.Content.ReadAsStringAsync();

            Match tokenMatch = Regex.Match(resContent, "\"recaptcha-token\" value=\"(.*?)\"");
            string token = tokenMatch.Groups[1].Value;

            Dictionary<string, string> paramDict = paramsString.Split('&')
                .Select(pair => pair.Split('='))
                .ToDictionary(pair => pair[0], pair => pair[1]);

            string postData = $"v={paramDict["v"]}&reason=q&c={token}&k={paramDict["k"]}&co={paramDict["co"]}";
            res = await Client.PostAsync(urlBase + "reload?k=" + paramDict["k"], new StringContent(postData));
            resContent = await res.Content.ReadAsStringAsync();

            Match answerMatch = Regex.Match(resContent, "\"rresp\",\"(.*?)\"");
            return answerMatch.Groups[1].Value;
        }

        public async Task<string?> OuoBypassAsync(string url)
        {
            url = url.Replace("ouo.press", "ouo.io");
            Uri uri = new(url);
            string id = url.Split('/').Last();

            Client.DefaultRequestHeaders.Clear();
            Client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/110.0.0.0 Safari/537.36");
            Client.DefaultRequestHeaders.Add("authority", "ouo.io");
            Client.DefaultRequestHeaders.Add("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
            Client.DefaultRequestHeaders.Add("accept-language", "en-GB,en-US;q=0.9,en;q=0.8");
            Client.DefaultRequestHeaders.Add("cache-control", "max-age=0");
            Client.DefaultRequestHeaders.Add("referer", "http://www.google.com/ig/adde?moduleurl=");
            Client.DefaultRequestHeaders.Add("upgrade-insecure-requests", "1");

            HttpResponseMessage res = await Client.GetAsync(url);

            string nextUrl = $"{uri.Scheme}://{uri.Host}/go/{id}";

            for (int i = 0; i < 2; i++)
            {
                if (res.Headers.Location != null)
                    break;

                string resContent = await res.Content.ReadAsStringAsync();

                // Extract form inputs
                Dictionary<string, string> inputs = Regex.Matches(resContent, @"<input.*?name=[""'](.*?)[""'].*?value=[""'](.*?)[""'].*?>")
                    .Where(m => Regex.IsMatch(m.Groups[1].Value, "token$"))
                    .ToDictionary(
                        m => m.Groups[1].Value,
                        m => m.Groups[2].Value
                    );

                inputs["x-token"] = await RecaptchaV3Async();

                FormUrlEncodedContent content = new(inputs);
                Client.DefaultRequestHeaders.Clear();
                Client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/x-www-form-urlencoded");

                res = await Client.PostAsync(nextUrl, content);

                nextUrl = $"{uri.Scheme}://{uri.Host}/xreallcygo/{id}";
            }

            return res.Headers.Location?.ToString();
        }
    }
}
