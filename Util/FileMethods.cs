namespace Script.Util
{
    public static class FileMethods
    {
        public static string[] GetFiles(string directory, string filter = "*.*") => Directory.GetFiles(directory, filter, SearchOption.AllDirectories);

        public static string GetFolderFromFile(string file) => Path.GetFileNameWithoutExtension(file).Split('\\').Last();

        public static string GetLastDirectory(string directoryPath) => directoryPath.Split('\\').Last();
        
    }
}
