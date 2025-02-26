using System.Globalization;

namespace Script.Util.Expanders
{
    public static class Parsing
    {
        public static string ConvertSecondsToTime(long seconds)
        {
            List<(string Name, long Value)> units =
            [
                ("month", 30 * 24 * 60 * 60), ("week", 7 * 24 * 60 * 60), ("day", 24 * 60 * 60), ("hour", 60 * 60), ("minute", 60)
            ];

            List<string> result = [];

            foreach ((string name, long value) in units)
            {
                if (seconds < value) continue;

                long count = Math.DivRem(seconds, value, out seconds);
                result.Add(count > 1 ? $"{count} {name}s" : $"{count} {name}");
            }

            return string.Join(", ", result);
        }

        public static string ConvertSecondsToTime(double seconds) => ConvertSecondsToTime(long.Parse(seconds.ToString(CultureInfo.InvariantCulture)));
        public static string ConvertSecondsToTime(int seconds) => ConvertSecondsToTime(long.Parse(seconds.ToString(CultureInfo.InvariantCulture)));
        
        public static string FormatBytes(double bytes)
        {
            switch (bytes)
            {
                case < 0:
                case 0:
                    return "0B";
            }

            const double kb = 1024;
            const double mb = kb * 1024;
            const double gb = mb * 1024;
            const double tb = gb * 1024;

            return bytes switch
            {
                < kb => $"{bytes:F0}B",
                < mb => $"{Math.Round(bytes / kb, 2)}KB",
                < gb => $"{Math.Round(bytes / mb, 2)}MB",
                < tb => $"{Math.Round(bytes / gb, 2)}GB",
                _ => $"{Math.Round(bytes / tb, 2)}TB"
            };
        }

    }
}
