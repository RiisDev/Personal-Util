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
    }
}
