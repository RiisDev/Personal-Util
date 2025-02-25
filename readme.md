![C#](https://img.shields.io/badge/-.NET%208.0-blueviolet?style=for-the-badge&logo=windows&logoColor=white) [![Discord Server](https://img.shields.io/discord/477201632204161025.svg?label=Discord&logo=Discord&colorB=7289da&style=for-the-badge)](https://discord.gg/yyuggrH) ![License](https://img.shields.io/github/license/RiisDev/RadiantConnect?style=for-the-badge)
# Utilities

This repo just contains my "Scripts/Util" solution which contains a bunch of methods and classes I use frequently individually for what ever I need done quickly

This code should not be used in a professional / efficient manner as it's not been tested and used just by me.

Publicized incase somehow someone needs some code listed here

## Tools Embedded

### Bypassers
* BypassVIP API Usage (Needs api key)
* BypassVIP Telegram Automation (WIP)
* Krnl Keygen / Auto Bypass (WIP)

### Scrapers
* Dynamic PornScraper designated by settings [Examples](https://github.com/RiisDev/Personal-Util/blob/main/Scrapers/NSFW/PrebuiltSettings.cs) | Based on XPath
* PornScraper includes metadata scraping, as well as video downloader (needs login cookies)
* nsw2u.net Scraper (Doesn't include downloading)

### Custom Build WebDrivers - Note I have no idea what the differences are when I wrote them
* HeavyDriver 
* LIghtDriver

### Quick File Utilties | Namespace HtmlUtil | Methods are Static
```csharp
public static string[] GetFiles(string directory, string filter = "*.*") => Directory.GetFiles(directory, filter, SearchOption.AllDirectories);

public static string GetFolderFromFile(string file) => Path.GetFileNameWithoutExtension(file).Split('\\').Last();

public static string GetLastDirectory(string directoryPath) => directoryPath.Split('\\').Last();
```

### HtmlAgilityPack Expander | Namespace HtmlUtil | Methods are Static | Properties of HtmlDocument
```csharp
public static List<Href> GetLinks(this HtmlDocument document, string html, string? startNodeXPath = null);

public static List<Dictionary<string, string>> GetNodesData(this HtmlDocument document, string html, string xpath, params string[] attributes);
```

### DataType Expander
```csharp
public static bool TryAdd<T>(this List<T> list, T value);

public static bool RegexEndsWith(this string text, [StringSyntax(StringSyntaxAttribute.Regex)] string pattern) => IsMatch(text, pattern + "$");

public static bool IsNullOrEmpty(this string text) => string.IsNullOrEmpty(text);

public static string Normalize(this string abstractText); // This removes invalid path characters, and normalizes to utf8 etc

public static string SafeFileName(this string abstractFile); // Just gets valid path name

public static string JsonSerialize<TValue>(TValue obj, bool @unsafe = true); // Encodes unsafe, Writes tab indents, allows floating literals
```

### Formatting / Conversion
* string ConvertSecondsToTime | Returns a formatted string, will take in int/double/long
* (HttpClient, CookieContainer) BuildClient | Builds an http client with default settings and returns as a tuple
* GetRootDomain | Returns the rootdomain and TLD from a url, not sure why it's not built into the Uri namespace
* FOrmatBytes | Takes in a double, returns a forammted string with B,KB,MB,GB,TB

### Regex Expander
* static string ExtractValue(this Match match, int group = 0) => match.Groups[group].Value;

### Regex Patterns
* JAV Title Naming | RegexPatterns.JavTitle
* Size Grabbing | RegexPatterns.SizeRegex
* ReCaptcha Stuff | RegexPatterns.(GetReCaptchaType/GetReCaptchaToken/GetReCaptchaResponse)