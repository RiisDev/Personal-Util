using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using HtmlAgilityPack;
using Script.Util.Expanders;

namespace Script.Scrapers.NSFW;

public class PornScraper
{
    private readonly List<string> _videosFinished = [];
    private int _videosCompleted;

    private readonly HtmlDocument _document = new();
    private readonly ScraperBuilder _scraperSettings;

    private static readonly CookieContainer Container = new();
    private readonly HttpClient _client = new(new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.All,
        ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
        CookieContainer = Container,
        AllowAutoRedirect = true
    });

    [Flags]
    public enum IdmFlags
    {
        NoConfirmationDialog = 1,
        AddToQueueOnly = 2
    }

    public PornScraper(ScraperBuilder builder)
    {
        _client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", builder.UserAgent);
        string[] cookiesSplit = builder.CookieString.Split("; ");
        foreach (string cookieSplit in cookiesSplit)
        {
            string[] parts = cookieSplit.Split('=', 2);
            if (parts.Length != 2) continue;

            string name = parts[0];
            string value = parts[1];

            Container.Add(new Cookie(name, value, "/", WebUtil.GetRootDomain(builder.IndexPage)));
        }

        _scraperSettings = builder;
    }

    public async Task<List<PornVideo>> StartScraper(int startingPage, int endPage, bool writeFinished = true, int startIndex = 0, string watchDirectory = "", bool startDownloads = false, Func<PornVideo, Action>? downloadAction = null)
    {
        List<PornVideo> videoListParsed = [];
        List<PornVideo> videoData = await ScrapePageIndexes(startingPage, endPage);
        videoData = await ScrapeVideoMetadata(videoData, startDownloads, startIndex, watchDirectory, downloadAction);

        videoListParsed.AddRange(videoData.Where(video => video.Downloads.Count > 0));

        if (writeFinished)
            await File.WriteAllTextAsync($"{WebUtil.GetRootDomain(_scraperSettings.IndexPage)}-videos.json", JsonSerializer.Serialize(videoListParsed, new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                MaxDepth = 64,
                IncludeFields = true,
                WriteIndented = true
            }));

        return videoListParsed;
    }

    private async Task<List<PornVideo>> ScrapePageIndexes(int startingPage, int endPage)
    {
        Console.WriteLine("Starting page scraper...");
        List<PornVideo> videoList = [];

        for (int index = startingPage; index <= endPage; index++)
        {
            Console.WriteLine($"Parsing: {_scraperSettings.IndexPage}{index}");
            HttpRequestMessage request = new(HttpMethod.Get, $"{_scraperSettings.IndexPage}{index}");
            HttpResponseMessage response = await _client.SendAsync(request);
            string basePage = await response.Content.ReadAsStringAsync();
            if (response.StatusCode == HttpStatusCode.NotFound) break;

            List<Href> videos = _document.GetLinks(basePage, _scraperSettings.Settings.VideoIndexSearch);

            Debug.WriteLine(JsonSerializer.Serialize(videos));
            Console.WriteLine($"Found: {videos.Count}");

            videoList.AddRange(videos.Select(video => new PornVideo(video.Title, video.Link)));
        }

        return videoList;
    }

    private async Task<List<PornVideo>> ScrapeVideoMetadata(List<PornVideo> cacheVideos, bool starDownloads = false, int startIndex = 0, string watchDirectory = "", Func<PornVideo, Action>? downloadAction = null)
    {
        MetadataSettings settings = _scraperSettings.Settings;

        foreach (PornVideo video in cacheVideos)
        {
            if (!Uri.TryCreate(video.PageUrl, UriKind.Absolute, out Uri? uri)) continue;
            if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) continue; // Double-check the page is real
            if (_videosFinished.Contains(video.Title!.Trim())) continue;

            _videosFinished.TryAdd(video.Title!.Trim()); // Add to done list so we don't scrape the same page again

            HttpRequestMessage request = new(HttpMethod.Get, uri);
            HttpResponseMessage response = await _client.SendAsync(request);
            string basePage = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == HttpStatusCode.NotFound) continue;
            if (!basePage.Contains(settings.PageRequiredBody)) continue; // If page doesn't have 'video-info' etc, skip it not a valid page

            _videosCompleted++;

            if (_videosCompleted <= startIndex) continue;

            Console.WriteLine($"Meta Data Scrape: {video.Title}");
            List<Dictionary<string, string>> nodeData = _document.GetNodesData(basePage, settings.DescriptionXPath);
            string descriptionText = nodeData.Aggregate("", (current, descriptionPart) => current + descriptionPart.Values.First());

            List<Href> downloadLinks = settings.DownloadXPath.Contains("div") ? _document.GetDivLinks(basePage, settings.DownloadXPath) : _document.GetLinks(basePage, settings.DownloadXPath);
            
            if (starDownloads && downloadAction is not null)
                downloadAction.Invoke(video);
            
            List<Href> tagsLink = _document.GetLinks(basePage, settings.TagsXPath);
            List<Href> performersLink = _document.GetLinks(basePage, settings.PerformersXPath);

            Console.WriteLine($"Found Downloads: {downloadLinks.Count}");
            Console.WriteLine($"Found Tags: {tagsLink.Count}");
            Console.WriteLine($"Found Performers: {performersLink.Count}");

            if (starDownloads && !string.IsNullOrEmpty(watchDirectory))
                await WaitTillDownloaded(watchDirectory);

            string duration = _document.GetNodesData(basePage, settings.DurationXPath).First().Values.First();
            string uploadTime = _document.GetNodesData(basePage, settings.DateTimeXPath).First().Values.First();

            video.Description = descriptionText;
            video.Downloads = downloadLinks;
            video.Duration = duration;
            video.PublishedDate = uploadTime;

            foreach (Href performer in performersLink)
                video.Performers += $"{performer}, ";
            foreach (Href tag in tagsLink)
                video.Performers += $"{tag}, ";
            
            video.Performers = video.Performers.TrimEnd(',', ' ');
            video.Tags = video.Tags.TrimEnd(',', ' ');
        }

        return cacheVideos;
    }

    private static async Task WaitTillDownloaded(string outputDirector)
    {
        int oldCount = Directory.GetFiles(outputDirector, "*.*", SearchOption.TopDirectoryOnly).Length;
        while (true)
        {
            await Task.Delay(500);
            int newCount = Directory.GetFiles(outputDirector, "*.*", SearchOption.TopDirectoryOnly).Length;
            if (newCount > oldCount) break;
        }
    }

    public async Task DownloadWithIdm(PornVideo video, string destinationDirectory, bool waitForDownload, IdmFlags flags)
    {
        IDManLib.CIDMLinkTransmitterClass transmitter = new();

        int flagValue = (int)flags;

        if (!Directory.Exists(destinationDirectory))
            Directory.CreateDirectory(destinationDirectory);

        transmitter.SendLinkToIDM2(
            bstrUrl: video.Downloads.First().Link,
            bstrReferer: video.PageUrl,
            bstrCookies: _scraperSettings.CookieString,
            bstrData: null,
            bstrUser: null,
            bstrPassword: null,
            bstrLocalPath: destinationDirectory,
            bstrLocalFileName: video.Title,
            lFlags: flagValue,
            reserved1: Type.Missing,
            reserved2: Type.Missing
        );

        if (waitForDownload)
            await WaitTillDownloaded(destinationDirectory);
    }

    public async Task StartIdmDownloads(List<PornVideo> downloads, string destinationDirectory, bool waitEachDownload, IdmFlags flags)
    {
        foreach (PornVideo video in downloads)
        {
            await DownloadWithIdm(video, destinationDirectory, waitEachDownload, flags);
            await Task.Delay(250); // We don't want to overload the COM
        }
    }

    public async Task StartDownloads(List<PornVideo> downloads, string destinationDirectory, int threads = 3)
    {
        SemaphoreSlim semaphore = new(threads);
        int downloadIndex = 0;

        foreach (PornVideo video in downloads)
        {
            string destinationPath = $"{destinationDirectory}\\{video.Title?.SafeFileName()}.mp4";

            Console.WriteLine($"Starting download {downloadIndex + 1}/{downloads.Count}: {video.Title?.SafeFileName()}");

            await semaphore.WaitAsync();

            _ = Task.Run(async () =>
            {
                try
                {
                    await DownloadVideoAsync(video.Downloads.First().Link, destinationPath, downloadIndex);
                }
                finally
                {
                    semaphore.Release();
                    downloadIndex++;
                }
            });
        }
    }

    private async Task DownloadVideoAsync(string url, string destinationPath, int downloadIndex)
    {
        try
        {
            using (HttpResponseMessage response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                long totalFileSize = response.Content.Headers.ContentLength.GetValueOrDefault();
                long totalBytesDownloaded = 0;

                await using FileStream fileStream = new(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);

                byte[] buffer = new byte[8192];
                int bytesRead;

                await using Stream stream = await response.Content.ReadAsStreamAsync();

                while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    totalBytesDownloaded += bytesRead;

                    DisplayProgress(downloadIndex, totalBytesDownloaded, totalFileSize);
                }
            }
            Console.WriteLine($"\nDownload {downloadIndex + 1} completed successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during download: {ex.Message}");
        }
    }

    private static void DisplayProgress(int downloadIndex, long bytesDownloaded, long totalFileSize)
    {
        if (totalFileSize <= 0) return;

        double percentage = (double)bytesDownloaded / totalFileSize * 100;

        Console.CursorTop = downloadIndex + 1;
        Console.CursorLeft = 0;
        Console.Write($"Download {downloadIndex + 1} progress: {percentage:F2}%");
    }

}


public class PornVideo(string title, string pageUrl)
{
    public string? Title { get; } = title;
    public string? PageUrl { get; } = pageUrl;

    public string Description { get; set; } = "";
    public List<Href> Downloads { get; set; } = [];

    public string Tags { get; set; } = "";

    public string Performers { get; set; } = "";

    public string PublishedDate { get; set; } = "";

    public string Duration { get; set; } = "";
};

public record ScraperBuilder(
    string UserAgent,
    string CookieString,
    string IndexPage,
    MetadataSettings Settings
);

public record DescriptionReplace([StringSyntax(StringSyntaxAttribute.Regex)] string Regex, string Value);

public record MetadataSettings(
    string PageRequiredBody,
    string VideoIndexSearch,
    string DescriptionXPath,
    DescriptionReplace DescriptionReplace,
    string DownloadXPath,
    string TagsXPath,
    string PerformersXPath,
    string DurationXPath,
    string DateTimeXPath
);