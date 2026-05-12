using System.Buffers.Binary;

namespace Script.Util.FileUtil
{
	public class VideoMethods
	{
		public static (int Width, int Height) GetVideoResolution(FileInfo fi) => GetVideoResolution(fi.FullName);

		public static (int Width, int Height) GetVideoResolution(string path)
		{
			using Process process = new();
			process.StartInfo = new ProcessStartInfo
			{
				FileName = "ffprobe",
				Arguments = $"-v error -select_streams v:0 -show_entries stream=width,height -of csv=p=0 \"{path}\"",
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false
			};

			process.Start();
			string output = process.StandardOutput.ReadToEnd().Trim();
			process.WaitForExit();

			string[] parts = output.Split(',');
			if (parts.Length < 2) throw new InvalidDataException($"Unexpected ffprobe output: '{output}'");

			int width = int.Parse(parts[0]);
			int height = int.Parse(parts[1]);

			return (width, height);
		}

		public static Dictionary<string, (int Width, int Height)> GetVideoResolutionsBulk(List<string> paths)
		{
			if (paths.Count == 0) return [];

			string inputs = string.Join(" ", paths.Select(p => $"-i \"{p}\""));
			string arguments = $"-v error -show_entries stream=width,height -of json {inputs}";

			using Process process = new();
			process.StartInfo = new ProcessStartInfo
			{
				FileName = "ffprobe",
				Arguments = arguments,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false
			};

			process.Start();
			string json = process.StandardOutput.ReadToEnd();
			process.WaitForExit();

			// Output groups streams per file in order — match back by index
			using JsonDocument doc = JsonDocument.Parse(json);
			JsonElement[] streams = doc.RootElement.GetProperty("streams")
				.EnumerateArray()
				.Where(s => s.TryGetProperty("width", out _))
				.ToArray();

			Dictionary<string, (int Width, int Height)> results = [];

			for (int i = 0; i < Math.Min(paths.Count, streams.Length); i++)
			{
				int width = streams[i].GetProperty("width").GetInt32();
				int height = streams[i].GetProperty("height").GetInt32();
				results[paths[i]] = (width, height);
			}

			return results;
		}
	}
}
