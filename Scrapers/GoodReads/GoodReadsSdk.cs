using RawHtml;
using System.Diagnostics;
using System.Net;
using System.ServiceModel.Syndication;
using System.Xml;

namespace Script.Scrapers
{
	public static class GoodReadsSdk
	{

		public static readonly CookieContainer Cookies = new();

		public static readonly HttpClient Client = new(new HttpClientHandler
		{
			AllowAutoRedirect = true,
			AutomaticDecompression = DecompressionMethods.All,
			CookieContainer = Cookies,
			UseCookies = true
		})
		{
			DefaultRequestHeaders =
			{
				{
					"User-Agent",
					"Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:141.0) Gecko/20100101 Firefox/141.0 GoodReadsWatcher/1.0"
				}
			},
			Timeout = TimeSpan.FromSeconds(15)
		};

		public static async Task<IReadOnlyList<BookDetails>> GetReadingListBooksAsync(long userId, string? shelf = null) => await GetReadingListBooksAsync(userId.ToString(), shelf);

		public static async Task<IReadOnlyList<BookDetails>> GetReadingListBooksAsync(string userId, string? shelf = null)
		{
			string url = $"https://www.goodreads.com/review/list_rss/{userId}" + $"{(string.IsNullOrWhiteSpace(shelf) ? string.Empty : $"?shelf={shelf}")}";

			string xmlContent = await GetStringWithRetryAsync(url);

			using XmlReader reader = XmlReader.Create(new StringReader(xmlContent));

			SyndicationFeed feed = SyndicationFeed.Load(reader);

			Uri[] bookUrls =
			[
				..
				from SyndicationItem item in feed.Items
				let doc = HtmlDocument.Parse(item.Summary.Text)
				let href = doc
					.GetElementsByTagName("a")
					.FirstOrDefault()
					?.GetAttribute("href")
				where Uri.IsWellFormedUriString(href, UriKind.Absolute)
				select new Uri(href!)
			];

			BookDetails[] books = await Task.WhenAll(bookUrls.Select(GetBookDetailsAsync));

			return books.AsReadOnly();
		}

		public static async Task<BookDetails> GetBookDetailsAsync(Uri url)
		{
			string html = await GetStringWithRetryAsync(url);

			HtmlDocument doc = HtmlDocument.Parse(html);

			HtmlElement? seriesElement = doc.GetElementsByTagName("h3")
				.FirstOrDefault(static element =>
					element.GetAttribute("aria-label")?.Contains("series") == true);

			HtmlElement? seriesAnchor = seriesElement?
				.GetElementsByTagName("a")
				.FirstOrDefault();

			Series? series = null;

			if (seriesAnchor is not null)
			{
				series = new Series
				{
					Id = Guid.NewGuid(),
					Name = seriesAnchor.TextContent.Trim(),
					Url = new Uri(seriesAnchor.GetAttribute("href") ?? string.Empty)
				};
			}

			HtmlElement? authorAnchor = doc.GetElementsByTagName("a")
				.FirstOrDefault(static element =>
					element.GetAttribute("class")?.Contains("ContributorLink") == true);

			HtmlElement? authorNameSpan = authorAnchor?
				.GetElementsByTagName("span")
				.FirstOrDefault(static element =>
					element.GetAttribute("data-testid") == "name");

			if (authorAnchor is null || authorNameSpan is null)
			{
				throw new InvalidOperationException("Could not parse author.");
			}

			Author author = new()
			{
				Id = Guid.NewGuid(),
				Name = authorNameSpan.TextContent.Trim(),
				Url = new Uri(authorAnchor.GetAttribute("href") ?? string.Empty)
			};

			HtmlElement? titleElement = doc.GetElementsByTagName("h1")
				.FirstOrDefault(static element =>
					element.GetAttribute("data-testid") == "bookTitle");

			string title = titleElement?.TextContent.Trim()
				?? url.Segments.Last();

			Book book = new()
			{
				Id = Guid.NewGuid(),
				Title = title,
				Url = url,
				AuthorId = author.Id,
				SeriesId = series?.Id
			};

			BookDetails details = new()
			{
				Book = book,
				Author = author,
				Series = series
			};

			if (series is not null)
			{
				details = await GetBookSeriesDetails(details);
			}

			return details;
		}

		public static async Task<BookDetails> GetBookSeriesDetails(BookDetails details)
		{
			if (details.Series is null)
			{
				return details;
			}

			string html = await GetStringWithRetryAsync(details.Series.Url);

			HtmlDocument doc = HtmlDocument.Parse(html);

			List<SeriesBook> books = [];

			HashSet<string> seen = [];

			Uri baseUri = new("https://www.goodreads.com");

			IEnumerable<HtmlElement> anchors = doc.All
				.Where(x => x.HasAttribute("href"))
				.Where(x => x.GetAttribute("href")!.Contains("/book/show"));

			foreach (HtmlElement anchor in anchors)
			{
				string? href = anchor.GetAttribute("href");

				if (string.IsNullOrWhiteSpace(href)) continue;
				if (!href.Contains("/book/show")) continue;

				Uri absoluteUrl = new(baseUri, href);

				string normalizedUrl = absoluteUrl.GetLeftPart(UriPartial.Path);

				if (!seen.Add(normalizedUrl)) continue;

				string title = anchor.Children.First().GetAttribute("alt") ?? "N/A";

				if (string.IsNullOrWhiteSpace(title)) continue;
				if (title.Length < 2) continue;

				books.Add(new SeriesBook
				{
					Title = title,
					Url = new Uri(normalizedUrl),
					Position = books.Count + 1
				});
			}

			books = books
				.Where(static book =>
					!book.Title.Contains("See full series") &&
					!book.Title.Contains("More books"))
				.ToList();

			Series updatedSeries = details.Series with
			{
				Books = books.AsReadOnly()
			};

			return details with
			{
				Series = updatedSeries
			};
		}

		public static async Task<List<Book>> GetAuthorsBooks(Uri authorUrl)
		{
			string html = await GetStringWithRetryAsync(authorUrl);

			HtmlDocument doc = HtmlDocument.Parse(html);

			List<Book> books = [];

			HashSet<string> seen = [];

			Uri baseUri = new("https://www.goodreads.com");

			IEnumerable<HtmlElement> rows = doc.All
				.Where(static element =>
					element.TagName.Equals("tr", StringComparison.OrdinalIgnoreCase))
				.Where(static element =>
					element.GetAttribute("itemtype")
						== "http://schema.org/Book");

			foreach (HtmlElement row in rows)
			{
				HtmlElement? bookAnchor = row
					.GetElementsByTagName("a")
					.FirstOrDefault(static element =>
						element.GetAttribute("class")
							?.Contains("bookTitle") == true);

				if (bookAnchor is null)
				{
					continue;
				}

				string? href = bookAnchor.GetAttribute("href");

				if (string.IsNullOrWhiteSpace(href))
				{
					continue;
				}

				Uri bookUrl = new(baseUri, href);

				string normalizedUrl =
					bookUrl.GetLeftPart(UriPartial.Path);

				if (!seen.Add(normalizedUrl))
				{
					continue;
				}

				string title =
					bookAnchor.TextContent.Trim();

				if (string.IsNullOrWhiteSpace(title))
				{
					HtmlElement? image = row
						.GetElementsByTagName("img")
						.FirstOrDefault();

					title =
						image?.GetAttribute("alt")
						?? "Unknown";
				}

				HtmlElement? authorAnchor = row
					.GetElementsByTagName("a")
					.FirstOrDefault(static element =>
						element.GetAttribute("class")
							?.Contains("authorName") == true);

				string authorName =
					authorAnchor?.TextContent.Trim()
					?? "Unknown";

				string? authorHref =
					authorAnchor?.GetAttribute("href");

				Author author = new()
				{
					Id = Guid.NewGuid(),
					Name = authorName,
					Url = new Uri(authorHref ?? authorUrl.ToString())
				};

				Book book = new()
				{
					Id = Guid.NewGuid(),
					Title = title,
					Url = new Uri(normalizedUrl),
					AuthorId = author.Id
				};

				books.Add(book);
			}

			return books;
		}

		private static async Task<string> GetStringWithRetryAsync(Uri url, int maxRetries = 5,
			CancellationToken cancellationToken = default) =>
			await GetStringWithRetryAsync(url.AbsoluteUri, maxRetries, cancellationToken);

		private static async Task<string> GetStringWithRetryAsync(string url, int maxRetries = 5, CancellationToken cancellationToken = default)
		{
			for (int attempt = 1; ; attempt++)
			{
				try
				{
					using HttpResponseMessage response =
						await Client.GetAsync(url, cancellationToken);

					if (response.StatusCode is HttpStatusCode.ServiceUnavailable or (HttpStatusCode)429)
					{
						if (attempt >= maxRetries)
						{
							throw new HttpRequestException(
								$"Request failed with status code {(int)response.StatusCode} after {maxRetries} attempts.");
						}

						TimeSpan delay =
							TimeSpan.FromSeconds(Math.Pow(2, attempt));

						Debug.WriteLine(
							$"Retry {attempt}/{maxRetries} for {url} due to {(int)response.StatusCode}. Waiting {delay.TotalSeconds}s");

						await Task.Delay(delay, cancellationToken);

						continue;
					}

					response.EnsureSuccessStatusCode();

					return await response.Content.ReadAsStringAsync(cancellationToken);
				}
				catch (HttpRequestException) when (attempt < maxRetries)
				{
					TimeSpan delay =
						TimeSpan.FromSeconds(Math.Pow(2, attempt));

					await Task.Delay(delay, cancellationToken);
				}
				catch (TaskCanceledException) when (attempt < maxRetries)
				{
					TimeSpan delay =
						TimeSpan.FromSeconds(Math.Pow(2, attempt));

					await Task.Delay(delay, cancellationToken);
				}
			}
		}
	}
}

