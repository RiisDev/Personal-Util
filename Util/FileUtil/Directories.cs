#pragma warning disable CA2211

namespace Script.Util.FileUtil
{
    public static class Directories
    {
        public static string Temp = Path.GetTempPath();
        public static string AppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)[..^6];
        public static string AppDataLocal = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        public static string AppDataRoaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        public static string Documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        public static string Desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        public static string ProgramFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        public static string ProgramsFiles86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
    }
}
