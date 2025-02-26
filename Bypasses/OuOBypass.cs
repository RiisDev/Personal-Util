using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Script.Util.Expanders;
using Script.Util.Regex;

namespace Script.Bypasses
{
    public class OuOBypass
    {
        // Credit: https://github.com/love98ooo/ouo-bypass-go/blob/master/resolve.go

        public async Task<string> RecaptchaV3Async(HttpClient client)
        {
            client.DefaultRequestHeaders.Clear();

            const string anchorUrl = "https://www.google.com/recaptcha/api2/anchor?ar=1&k=6Lcr1ncUAAAAAH3cghg6cOTPGARa8adOf-y9zv2x&co=aHR0cHM6Ly9vdW8ucHJlc3M6NDQz&hl=en&v=pCoGBhjs9s8EhFOHJFe8cqis&size=invisible&cb=ahgyd1gkfkhe";
            string urlBase = "https://www.google.com/recaptcha/";

            Match match = RegexPatterns.GetReCaptchaType.Match(anchorUrl);
            if (!match.Success || match.Groups.Count < 3)
                throw new Exception("No matches found in ANCHOR_URL");

            urlBase += $"{match.ExtractValue(1)}/";
            string paramsString = match.ExtractValue(2);

            HttpResponseMessage response = await client.GetAsync($"{urlBase}anchor?{paramsString}");

            if (!response.IsSuccessStatusCode)
                throw new Exception("reCAPTCHA status code is not 200");

            string body = await response.Content.ReadAsStringAsync();
            Match tokenMatch = RegexPatterns.GetReCaptchaType.Match(body);

            if (!tokenMatch.Success || tokenMatch.Groups.Count < 2)
                throw new Exception("No token found in response");

            string token = tokenMatch.Groups[1].Value;
            Dictionary<string, string> paramsMap = [];

            foreach (string pair in paramsString.Split('&'))
            {
                string[] parts = pair.Split('=');
                if (parts.Length == 2) 
                    paramsMap[parts[0]] = parts[1];
            }

            FormUrlEncodedContent postData = new(
            [
                new KeyValuePair<string, string>("v", paramsMap["v"]),
                new KeyValuePair<string, string>("c", token),
                new KeyValuePair<string, string>("k", paramsMap["k"]),
                new KeyValuePair<string, string>("co", paramsMap["co"]),
                new KeyValuePair<string, string>("reason", "q")
                ]
            );

            HttpResponseMessage postResponse = await client.PostAsync(urlBase + "reload?k=" + paramsMap["k"], postData);
            if (!postResponse.IsSuccessStatusCode)
                throw new Exception("Failed to get reCAPTCHA response");

            string postBody = await postResponse.Content.ReadAsStringAsync();
            Match answerMatch = RegexPatterns.GetReCaptchaResponse.Match(postBody);

            if (!answerMatch.Success || answerMatch.Groups.Count < 2)
                throw new Exception("No answer found in reCAPTCHA response");

            return answerMatch.ExtractValue(1);
        }

        public async Task<string?> OuoBypassAsync(string ouoURL)
        {
            (HttpClient client, _) = WebUtil.BuildClient();

            string tempURL = ouoURL.Replace("ouo.press", "ouo.io");
            Uri uri = new Uri(tempURL);
            string id = uri.Segments[^1];
            string location = "";

            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/110.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
            client.DefaultRequestHeaders.Add("Accept-Language", "en-GB,en-US;q=0.9,en;q=0.8");

            HttpResponseMessage response = await client.GetAsync(tempURL);
            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                throw new Exception("ouo.io is blocking the request");
            }

            string body = await response.Content.ReadAsStringAsync();
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(body);

            Dictionary<string, string> data = new Dictionary<string, string>();
            foreach (var input in doc.DocumentNode.SelectNodes("//input"))
            {
                string name = input.GetAttributeValue("name", "");
                if (name.EndsWith("token"))
                {
                    string value = input.GetAttributeValue("value", "");
                    data[name] = value;
                }
            }

            string nextURL = $"{uri.Scheme}://{uri.Host}/go/{id}";
            data["x-token"] = await RecaptchaV3Async(client);

            for (int i = 0; i < 2; i++)
            {
                await Task.Delay(1000);
                var postData = new FormUrlEncodedContent(data);
                HttpResponseMessage postResponse = await client.PostAsync(nextURL, postData);

                if (postResponse.StatusCode == System.Net.HttpStatusCode.Found)
                {
                    location = postResponse.Headers.Location.ToString();
                    break;
                }
                else if (postResponse.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    throw new Exception("ouo.io is blocking the request");
                }

                nextURL = $"{uri.Scheme}://{uri.Host}/xreallcygo/{id}";
            }

            return location;
        }
    }
}
