using System.Diagnostics;

namespace Script.Util.Expanders
{
    public static class MiscExpanders
    {
        public static bool TryAdd<T>(this List<T> list, T value)
        {
            if (list.Contains(value)) return false;
            list.Add(value);
            return true;
        }

        public static void Flip(ref this bool value) => value = !value;

        public static void Print<T>(this IEnumerable<T> collection, bool console = false)
        {
            foreach (T item in collection)
            {
                Debug.WriteLine(item);
                if (console) Console.WriteLine(item);
            }
        }
    }
}
