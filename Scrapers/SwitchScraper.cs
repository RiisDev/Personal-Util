using System.Diagnostics;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using Script.WebDrivers.HeavyDriver;
using Script.WebDrivers.LightweightDriver.DriverHandler;

namespace Script.Scrapers;

// Needs ReWrite

static class SwitchScraper
{
    private static List<RomData> _roms = [];
    private static List<PageUrl> _items = [];
    private static DriverHandler _handler = null!;

    private const string BaseUrl = "https://nsw2u.net/switch-posts?lcp_page0=";
    private static readonly int Port = Win32.GetFreePort();
    private static readonly Regex IdRegex = new(@"ID=( |&nbsp;||\[)([^\s()]+)", RegexOptions.Compiled);
    private static readonly Regex SizeRegex = new(@"(\d+((\.|,)\d+)?)\s*(MB|GB|KB)<", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex LinkRegex = new(@"<a\s+href=['""](?<url>[^'""]+)['""]>(?<name>[^<]+)</a>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PullLinks = new("<li\\b[^>]*>\\s*<a\\s+href=\"([^\"]*)\">([^<]*)<\\/a>\\s*<\\/li>", RegexOptions.Compiled);


    public static async Task RunLinkGen()
    {
        for (int i = 0; i < 15; i++)
        {
            await SocketHandler.NavigateTo(BaseUrl + i, "Switch Posts", Port, _handler.Socket, false);

            while (true)
            {
                string? title = await DriverHandler.GetPageUrl(Port);
                if (title != "Just a moment...") break;
            }

            await Task.Delay(75);

            Dictionary<string, object> navigateEvent = new()
            {
                {"id", new Random().Next() },
                {"method", "Runtime.evaluate"},
                {"params", new Dictionary<string, string> {{"expression", "document.documentElement.outerHTML" } }}
            };

            string? data = await SocketHandler.ExecuteOnPageWithResponse("", Port, navigateEvent, "", _handler.Socket, true, true);
            string actualData = DriverMethods.GetJavaScriptReturnValue(data!);

            MatchCollection matches = PullLinks.Matches(actualData);


            foreach (Match match in matches)
            {
                string url = match.Groups[1].Value;
                string title = match.Groups[2].Value;
                _items.Add(new PageUrl(url, title));
            }

            Console.WriteLine($"Finished page: {i}, total links curated: {_items.Count}");
        }

        StreamWriter writer = new("links.json");
        await writer.WriteAsync(JsonSerializer.Serialize(_items, new JsonSerializerOptions { WriteIndented = true, AllowTrailingCommas = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        await writer.DisposeAsync();
    }

    public static bool CheckSkip(string itemTitle)
    {
        Dictionary<string, string> skipReasons = new()
        {
            { "Update Pack", "UPDATE PACK" },
            { "Sigpatches", "SIGPATCHES" },
            { "Last Patched", "MISC" },
            { "website has now", "MISC" },
            { "Live | ", "MISC" },
            { "Arcade Archives DOUBLE DRAGON II The Revenge Switch NSP XCI", "MISC" },
            { "Happy New Year", "ANNOUNCEMENT" },
            { "Discord", "ANNOUNCEMENT" },
            { "SX OS", "ANNOUNCEMENT" },
            { "Switch-xci", "ANNOUNCEMENT" },
            { "Base Rom", "BASE PACK" },
            { "All Games Dev", "GAME PACK" },
            { "MIG Switch Game Collection", "MIG PACK" },
            { "+ 30 Games", "MIG PACK" },
            { "The Core Collection", "FNAF PACK" },
            { "Sonic 1-2", "SONIC PACK" },
            { "Arcade Archives", "SONIC PACK" },
            { "RADIO HAMMER", "SONIC PACK" }
        };

        foreach (RomData x in _roms)
        {
            if (x.Name == itemTitle)
            {
                Debug.WriteLine($"Skipping: {itemTitle}");
                return true;
            }
        }

        foreach (KeyValuePair<string, string> reason in skipReasons)
        {
            if (itemTitle.Contains(reason.Key, StringComparison.OrdinalIgnoreCase))
            {
                Debug.WriteLine($"Skipping {reason.Value}: {itemTitle}");
                return true;
            }
        }

        return false;
    }


    public static string Actual(this string str)
    {
        string itemTitle = HttpUtility.HtmlDecode(str);
        itemTitle = Regex.Unescape(itemTitle)
            .Replace("\\", "\\")
            .Replace("'", "'")
            .Replace("’", "'")
            .Replace("–", "-")
            .Replace("”", "\"")
            .Replace("“", "\"")
            .Replace("---", "-")
            .Replace("—", "-")
            .Replace("''", "\"")
            .Replace("\u00d7", "x")
            .Replace("‘", "'")
            .Replace("'", "'")
            .Replace("  ", " ")
            .Replace("—", "-")
            .Replace("—", "-")
            .Replace("||", "|")
            .Replace("?", "?")
            .Replace("             ", " ")
            .Replace("             ", " ")
            .Replace("             ", " ")
            .Replace("-", "-")
            .Replace("—", "-");

        return itemTitle;
    }

    public static async Task ParsePageUrl()
    {
        foreach (PageUrl item in _items!)
        {
            Retry:
            string itemTitle = item.Title.Actual();

            if (CheckSkip(itemTitle))
                continue;

            await SocketHandler.NavigateTo(item.Url, "Switch Posts", Port, _handler.Socket, false);

            await WaitForPage(itemTitle);

            await Task.Delay(75);

            string actualData = await GetPageBody();
            double totalSize = GetTotalSize(actualData);

            List<RomDownloadLink> links = [];

            Debug.WriteLine("links...");

            GetDownloadLinks(actualData, links);

            if (AddDataToRows(actualData, itemTitle, item, totalSize, links, out Match idMatch)) goto Retry;

            StreamWriter romWriter = new("roms.json");
            await romWriter.WriteAsync(JsonSerializer.Serialize(_roms, new JsonSerializerOptions { WriteIndented = true, AllowTrailingCommas = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
            await romWriter.DisposeAsync();

            Console.WriteLine($"Parsed: {itemTitle} | {idMatch.Groups[2]}");
        }
    }

    private static async Task<string> GetPageBody()
    {
        Debug.WriteLine("nav...");
        Dictionary<string, object> navigateEvent = new()
        {
            {"id", new Random().Next() },
            {"method", "Runtime.evaluate"},
            {"params", new Dictionary<string, string> {{"expression", "document.documentElement.outerHTML" } }}
        };

        string? data = await SocketHandler.ExecuteOnPageWithResponse("", Port, navigateEvent, "", _handler.Socket, true, true);
        string actualData = DriverMethods.GetJavaScriptReturnValue(data!);
        return actualData;
    }

    private static async Task WaitForPage(string itemTitle)
    {
        while (true)
        {
            Debug.WriteLine("Check title...");
            string title = (await DriverHandler.GetPageTitle(Port))?.Actual() ?? "Database Error";

            Debug.WriteLine($"Title: {title} | Item Title: {itemTitle}");

            if (title == "Database Error")
            {
                Dictionary<string, object> reloadEvent = new()
                {
                    {"id", new Random().Next() },
                    {"method", "Page.reload"},
                    {"params", new Dictionary<string, object> {{"ignoreCache", true } }}
                };

                await SocketHandler.ExecuteOnPageWithResponse("", Port, reloadEvent, "", _handler.Socket, true, true);
                continue;
            }

            if (title.Contains(itemTitle, StringComparison.InvariantCultureIgnoreCase) || itemTitle.Contains(title, StringComparison.InvariantCultureIgnoreCase))
            {
                break;
            }
        }
    }

    private static bool AddDataToRows(string actualData, string itemTitle, PageUrl item, double totalSize, List<RomDownloadLink> links,
        out Match idMatch)
    {
        string id;
        idMatch = IdRegex.Match(actualData);

        Debug.WriteLine("id...");
        if (!idMatch.Success || string.IsNullOrEmpty($"{idMatch.Groups[2]}") || string.IsNullOrEmpty($"{idMatch.Groups[2].Value}"))
            id = "FAILED";
        else
            id = idMatch.Groups[2].Value;


        _roms.Add(new RomData(itemTitle, item.Url, id, totalSize.ToString(CultureInfo.InvariantCulture), links));

        Console.Title = $"{_roms.Count}/{_items.Count} ROMS Parsed";

        return false;
    }

    private static void GetDownloadLinks(string actualData, List<RomDownloadLink> links)
    {
        foreach (Match match in LinkRegex.Matches(actualData))
        {
            string name = Regex.Unescape(match.Groups["name"].Value.Trim());
            string url = match.Groups["url"].Value;

            if (!url.Contains("ouo.io")) continue;

            links.Add(new RomDownloadLink(name, url));
        }
    }

    private static double GetTotalSize(string actualData)
    {
        double totalSize = 0;
        Debug.WriteLine("Size...");
        foreach (Match match in SizeRegex.Matches(actualData))
        {
            double size = Convert.ToDouble(match.Groups[1].Value.Replace(",", "."));
            string unit = match.Groups[4].Value.ToUpper();

            switch (unit)
            {
                case "GB":
                    size *= 1024; // Convert GB to MB
                    break;
                case "KB":
                    size /= 1024; // Convert KB to MB
                    break;
            }

            totalSize += size;
        }

        return totalSize;
    }

    public static async Task<double> GetTotalRomsSize()
    {
        List<RomData> rowData = JsonSerializer.Deserialize<List<RomData>>(await File.ReadAllTextAsync("roms.json")) ?? [];
        double totalSize = rowData.Sum(x => double.TryParse(x.Size, out double size) ? size : 0.0);
        return totalSize;
    }


    public static async Task Run()
    {
        _handler = new DriverHandler();

        await _handler.StartDriver(@"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe", Port);
        
        //await RunLinkGen();

        _items = JsonSerializer.Deserialize<List<PageUrl>>(await File.ReadAllTextAsync("links.json")) ?? [];
        
        if (!File.Exists("roms.json"))
            await File.WriteAllTextAsync("roms.json", "[]");

        _roms = JsonSerializer.Deserialize<List<RomData>>(await File.ReadAllTextAsync("roms.json")) ?? [];

        await ParsePageUrl();

        StreamWriter finalWriter = new("roms.json");
        await finalWriter.WriteAsync(JsonSerializer.Serialize(_roms, new JsonSerializerOptions { WriteIndented = true, AllowTrailingCommas = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        await finalWriter.DisposeAsync();

        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine($"Finished parsing all links: {_roms.Count}");

        while (true) Console.ReadLine();

    }
}
internal record PageUrl(string Url, string Title);
internal record RomDownloadLink(string Name, string Url);
internal record RomData(string Name, string PageUrl, string Id, string Size, List<RomDownloadLink> DownloadLinks);