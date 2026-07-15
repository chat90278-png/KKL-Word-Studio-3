namespace KKL.WordStudio.UI.ViewModels;

internal static class DiagnosticReadOnlyListExtensions
{
    public static int IndexOf<T>(this IReadOnlyList<T> items, T value)
    {
        ArgumentNullException.ThrowIfNull(items);
        var comparer = EqualityComparer<T>.Default;
        for (var index = 0; index < items.Count; index++)
        {
            if (comparer.Equals(items[index], value))
                return index;
        }

        return -1;
    }
}
