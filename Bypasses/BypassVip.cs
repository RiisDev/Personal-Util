using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Script.Util.Expanders;
using TL;
using WTelegram;

namespace Script.Bypasses
{
    public record BypassApiReturn([property: JsonPropertyName("status")] string Status, [property: JsonPropertyName("result")] string Result);
    public record TelegramUser(int ApiId, string ApiHash, string PhoneNumber);

    public class BypassVip
    {
        public string VerificationCodeReturn { get; set; } = "";
        public delegate void VerificationCode();
        public event VerificationCode? OnVerificationRequested;
        
        public static async Task<List<string>> Bypass(string apiKey, List<string> urls)
        {
            List<string> bypassed = [];

            using HttpClient client = WebUtil.BuildClient().Item1;

            client.DefaultRequestHeaders.Add("x-api-key", apiKey);

            foreach (string url in urls)
            {
                HttpResponseMessage response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"https://api.bypass.vip/premium/bypass?url={url}"));

                if (!response.IsSuccessStatusCode) continue;

                bypassed.Add((await response.Content.ReadFromJsonAsync<BypassApiReturn>() ?? new BypassApiReturn("", "")).Result);
            }

            return bypassed;
        }

        public async Task<string> TelegramBypass(TelegramUser user, string url)
        {
            Client telegramClient = new(user.ApiId, user.ApiHash);

            string accessData = user.PhoneNumber;

            await Task.Run(async () =>
            {
                while (telegramClient.User == null)
                {
                    switch (await telegramClient.Login(accessData))
                    {
                        case "verification_code":
                            OnVerificationRequested?.Invoke();

                            while (VerificationCodeReturn.IsNullOrEmpty())
                            {
                                await Task.Delay(50);
                            }

                            accessData = VerificationCodeReturn;
                            break;
                        case "name":
                        case "password":
                            throw new InvalidOperationException("User must exist, without password.");
                    }
                }
            });

            string botUsername = "";
            Contacts_ResolvedPeer? resolved = await telegramClient.Contacts_ResolveUsername(botUsername);
            User? bot = resolved.users.Values.First();

            telegramClient.WithUpdateManager(async (update) =>
            {
                if (update is UpdateNewMessage { message: Message m })
                    if (m.peer_id.ID == bot.ID)
                    {
                        if (m.message == "Please provide a valid URL")
                        {
                            await telegramClient.SendMessageAsync(bot, url);
                        }
                        else
                        {
                            Debug.WriteLine($"New Message: {m.message}");
                        }
                    }
                        
            });

            await telegramClient.SendMessageAsync(bot, "/start");

            return "";
        }
    }
}
