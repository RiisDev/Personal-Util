using Script.Util.Expanders;
using Script.Util.RegexUtil;

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

        public static long GetFileSize(string file) => new FileInfo(file).Length;

        public static DateTime GetFileCreationDate(string file) => new FileInfo(file).CreationTime;

        public static DateTime GetFileModificationDate(string file) => new FileInfo(file).LastWriteTime;

    }
}
