// ReSharper disable ArrangeNamespaceBody

namespace Script.Scrapers.GoodReads;

public sealed record Book
{
	public required Guid Id { get; init; }

	public required string Title { get; init; }

	public required Uri Url { get; init; }

	public required Guid AuthorId { get; init; }

	public Guid? SeriesId { get; init; }
}

public sealed record Author
{
	public required Guid Id { get; init; }

	public required string Name { get; init; }

	public required Uri Url { get; init; }
}

public sealed record SeriesBook
{
	public required string Title { get; init; }

	public required Uri Url { get; init; }

	public required int Position { get; init; }
}

public sealed record Series
{
	public required Guid Id { get; init; }

	public required string Name { get; init; }

	public required Uri Url { get; init; }

	public IReadOnlyList<SeriesBook> Books { get; init; } = [];
}

public sealed record BookDetails
{
	public required Book Book { get; init; }

	public required Author Author { get; init; }

	public Series? Series { get; init; }
}


public sealed record KnownBook
{
	public required string Title { get; init; }

	public required string AuthorName { get; init; }

	public required string Url { get; init; }

	public string? SeriesName { get; init; }

	public int? SeriesPosition { get; init; }
}

public sealed record NotificationPayload
{
	public required string Type { get; init; }

	public required string Title { get; init; }

	public required string Author { get; init; }

	public required string Url { get; init; }

	public string? Series { get; init; }

	public int? Position { get; init; }

	public required DateTimeOffset DetectedAtUtc { get; init; }
}