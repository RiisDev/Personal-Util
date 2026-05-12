
namespace Script.Util.Expanders
{
    public static class ShittyMethods
    {
        public static void Print(object? data) { Debug.WriteLine(data?.ToString()); Console.WriteLine(data?.ToString()); }
        public static void print(object? data) => Print(data);

        public static void Print<T>(this IEnumerable<T> collection) => collection.Print(true);
        public static void print<T>(this IEnumerable<T> collection) => collection.Print(true);

        public static void Wait(int duration) => Thread.Sleep(duration);
        public static void wait(int duration) => Thread.Sleep(duration);

    }

    public sealed class FileProgressReporter(string filePath, long fileLength)
    {
	    private long _bytesRead = 0;
	    private int _statementsProcessed = 0;
	    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
	    private DateTime _lastReport = DateTime.UtcNow;

	    public void Report(int bytesConsumed, bool forceReport = false)
	    {
		    Interlocked.Add(ref _bytesRead, bytesConsumed);
		    Interlocked.Increment(ref _statementsProcessed);

		    if (!forceReport && (DateTime.UtcNow - _lastReport).TotalSeconds < 2) return;
		    _lastReport = DateTime.UtcNow;

		    double percent = (double)_bytesRead / fileLength * 100;
		    double mbRead = _bytesRead / 1024.0 / 1024.0;
		    double mbTotal = fileLength / 1024.0 / 1024.0;
		    double mbPerSec = mbRead / _stopwatch.Elapsed.TotalSeconds;
		    double remainingSec = mbPerSec > 0 ? (mbTotal - mbRead) / mbPerSec : 0;
		    TimeSpan eta = TimeSpan.FromSeconds(remainingSec);

		    // Save cursor position to overwrite the same line
		    int cursorTop = Console.CursorTop;
		    Console.ForegroundColor = ConsoleColor.Gray;
		    Console.Write($"\r  📄 {Path.GetFileName(filePath)} | " +
		                  $"{percent:F1}% | " +
		                  $"{mbRead:F0}/{mbTotal:F0} MB | " +
		                  $"{mbPerSec:F1} MB/s | " +
		                  $"Statements: {_statementsProcessed:N0} | " +
		                  $"ETA: {eta:mm\\:ss}          "); // trailing spaces clear leftover chars
		    Console.ResetColor();
	    }

	    public void Complete()
	    {
		    Report(0, forceReport: true);
		    Console.WriteLine(); // move past the progress line
		    Console.ForegroundColor = ConsoleColor.Green;
		    Console.WriteLine($"  ✓ Done in {_stopwatch.Elapsed:mm\\:ss} | " +
		                      $"{_statementsProcessed:N0} statements processed");
		    Console.ResetColor();
	    }
    }
}
