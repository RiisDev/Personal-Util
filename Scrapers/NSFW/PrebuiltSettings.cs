namespace Script.Scrapers.NSFW
{
    public static class PrebuiltSettings
    {
        public static PornScraper AvJialiSettings(string cookies)
        {
            return new PornScraper(
                new ScraperBuilder(
                    UserAgent: "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:135.0) Gecko/20100101 Firefox/135.0",
                    CookieString: cookies,
                    IndexPage: "https://avjiali.com/chinese-av/page/",
                    Settings: new MetadataSettings(
                        PageRequiredBody: "class=\"videoinfo\"",
                        VideoIndexSearch: "//div[@class='content']",
                        DescriptionXPath:
                        "//body//div[@class=\"videoinfo\"]/p//text() | //body//div[@class=\"videoinfo\"]/p/span[@class=\"readmore\"]//text()\n",
                        DescriptionReplace: new DescriptionReplace(
                            Regex: "", 
                            Value: ""
                        ),
                        DownloadXPath: "//ul[@class=\"download\"]",
                        TagsXPath: "//div[@class=\"videoinfo\"]/div[@class=\"cat\"][2]",
                        PerformersXPath: "//div[@class=\"videoinfo\"]/div[@class=\"cat\"][1]",
                        DurationXPath: "//body//div[@class=\"videoinfo\"]//div[@class=\"video-duration\"]/text()",
                        DateTimeXPath: "//body//div[@class=\"videoinfo\"]//div[@class=\"video-date\"]/text()"
                    )
                )
            );
        }
    }
}
