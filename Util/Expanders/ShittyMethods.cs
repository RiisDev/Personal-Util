
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
}
