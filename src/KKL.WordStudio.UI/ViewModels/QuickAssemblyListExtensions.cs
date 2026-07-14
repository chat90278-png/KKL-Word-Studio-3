namespace KKL.WordStudio.UI.ViewModels;

internal static class QuickAssemblyListExtensions
{
    public static int IndexOf<T>(this IReadOnlyList<T> items, T value)
    {
        for (var index = 0; index < items.Count; index++)
        {
            if (EqualityComparer<T>.Default.Equals(items[index], value))
                return index;
        }

        return -1;
    }
}
