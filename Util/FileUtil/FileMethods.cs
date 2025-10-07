namespace Script.Util.FileUtil
{
    public static class FileMethods
    {
        public static string[] GetFiles(string directory, string filter = "*.*") => Directory.GetFiles(directory, filter, SearchOption.AllDirectories);

        public static string GetFolderFromFile(string file) => Path.GetFileNameWithoutExtension(file).Split('\\').Last();

        public static string GetLastDirectory(string directoryPath) => directoryPath.Split('\\').Last();

        public static string[] GetDuplicates(string directory)
        {
            List<string> fileNames = [];
            string[] files = GetFiles(directory);
            string[] duplicates = files.Where(file => !fileNames.TryAdd(Path.GetFileNameWithoutExtension(file))).ToArray();
            return duplicates;
        }

        public static string[] GetRegexDuplicates(string directory)
        {
            string[] files = GetFiles(directory);
            string[] duplicates = files.Where(file => Path.GetFileNameWithoutExtension(file).RegexEndsWith(RegexPatterns.DuplicateFile.ToString())).ToArray();
            return duplicates;
        }

        public static string[] GetAllDuplicates(string directory) => GetDuplicates(directory).Concat(GetRegexDuplicates(directory)).ToList().OrderBy(x=> x, StringComparer.InvariantCultureIgnoreCase).ToArray();

        public static long GetFileSize(string file) => new FileInfo(file).Length;

        public static DateTime GetFileCreationDate(string file) => new FileInfo(file).CreationTime;

        public static DateTime GetFileModificationDate(string file) => new FileInfo(file).LastWriteTime;

        public static void Rename(string file, string newName)
        {
            if (!File.Exists(file)) throw new FileNotFoundException($"The file '{file}' does not exist."); 

            string? directory = Path.GetDirectoryName(file);
            if (directory.IsNullOrEmpty()) throw new DirectoryNotFoundException($"The directory for the file '{file}' does not exist.");

            string extension = Path.GetExtension(file);
            string newNameWithExtension = newName + extension;
            string newFilePath = Path.Combine(directory!, newNameWithExtension);

            int counter = 1;
            while (File.Exists(newFilePath))
            {
                newNameWithExtension = $"{newName} ({counter}){extension}";
                newFilePath = Path.Combine(directory!, newNameWithExtension);
                counter++;
            }

            try { File.Move(file, newFilePath); } catch { /**/ }
        }

        public static void RenameDuplicates(string directory) => GetAllDuplicates(directory).ToList().ForEach(file=> Rename(file, Path.GetFileNameWithoutExtension(file).RegexReplace(RegexPatterns.DuplicateFile.ToString(), string.Empty).Trim()));

        public static void FixShowFormatting(string directory)
        {
	        foreach (string file in Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories))
	        {
		        Match invalidForm = Regex.Match(file, @"-\s(\d\d)x(\d\d)\s-");

		        if (invalidForm.Success)
		        {
			        string newFile = Regex.Replace(file, @"-\s(\d\d)x(\d\d)\s-", m => $"- S{m.Groups[1].Value}E{m.Groups[2].Value} -");
			        Debug.WriteLine($"{Path.GetFileName(file)} -> {Path.GetFileName(newFile)}");

			        try { File.Move(file, newFile); }
			        catch { Debug.WriteLine("File in use"); }
		        }
	        }

		}
	}
}
