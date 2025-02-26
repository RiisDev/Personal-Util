using HtmlAgilityPack;

namespace Script.Util.Expanders
{
    public record Href(string Link, string Title, string LinkText);

    public static class HtmlUtil
    {
        public static List<Href> GetLinks(this HtmlDocument document, string html, string? startNodeXPath = null)
        {
            List<Href> links = [];

            document.DocumentNode.RemoveAll();
            document.LoadHtml(html);

            HtmlNode searchRoot = document.DocumentNode;
            if (!string.IsNullOrEmpty(startNodeXPath))
            {
                HtmlNode? startNodeElement = document.DocumentNode.SelectSingleNode(startNodeXPath);
                if (startNodeElement != null)
                    searchRoot = startNodeElement;
            }

            HtmlNodeCollection? hrefNodes = searchRoot.SelectNodes(".//a[@href]");
            if (hrefNodes == null) return links;

            links.AddRange(hrefNodes.Select(hrefNode =>
                new Href(
                    hrefNode.GetAttributeValue("href", "#"),
                    hrefNode.GetAttributeValue("title", ""),
                    hrefNode.InnerText.Trim()
                )
            ));


            return links;
        }

        public static List<Dictionary<string, string>> GetNodesData(this HtmlDocument document, string html, string xpath, params string[] attributes)
        {
            List<Dictionary<string, string>> data = [];

            document.DocumentNode.RemoveAll();
            document.LoadHtml(html);

            HtmlNodeCollection? nodes = document.DocumentNode.SelectNodes(xpath);
            if (nodes == null) return data;

            foreach (HtmlNode nodeData in nodes)
            {
                Dictionary<string, string> attributesFound = [];
                foreach (string attribute in attributes)
                {
                    attributesFound[attribute] = nodeData.GetAttributeValue(attribute, "");
                }

                attributesFound["INNER_TEXT"] = nodeData.InnerText.Trim();

                data.Add(attributesFound);
            }

            return data;
        }
    }
}
