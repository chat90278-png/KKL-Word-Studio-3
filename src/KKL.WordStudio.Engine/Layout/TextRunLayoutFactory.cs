namespace KKL.WordStudio.Engine.Layout;

using KKL.WordStudio.Application.Layout;

internal static class TextRunLayoutFactory
{
    public static IReadOnlyList<TextRunLayout> Build(IReadOnlyList<MeasuredLine> lines)
    {
        var runs = new List<TextRunLayout>();
        foreach (var line in lines)
        {
            AppendMeasuredCharacters(runs, line.Characters);
            if (line.EndsWithExplicitNewline)
            {
                AppendText(
                    runs,
                    "\n",
                    line.Characters.LastOrDefault()?.Style
                    ?? new MeasuredTextStyle(false, false, false, 11d, null));
            }
        }

        if (runs.Count == 0)
        {
            runs.Add(new TextRunLayout
            {
                Text = string.Empty,
                Bold = false,
                Italic = false,
                Underline = false,
                FontSizePoints = 11d,
                FontFamilyName = null
            });
        }

        return runs;
    }

    private static void AppendMeasuredCharacters(
        List<TextRunLayout> target,
        IReadOnlyList<MeasuredCharacter> characters)
    {
        foreach (var group in characters.GroupAdjacentByStyle())
        {
            AppendText(
                target,
                new string(group.Characters.Select(character => character.Value).ToArray()),
                group.Style);
        }
    }

    private static void AppendText(
        List<TextRunLayout> target,
        string text,
        MeasuredTextStyle style)
    {
        if (target.Count > 0)
        {
            var last = target[^1];
            if (last.Bold == style.Bold
                && last.Italic == style.Italic
                && last.Underline == style.Underline
                && Math.Abs(last.FontSizePoints - style.FontSizePoints) < 0.0001d
                && string.Equals(last.FontFamilyName, style.FontFamilyName, StringComparison.Ordinal))
            {
                target[^1] = new TextRunLayout
                {
                    Text = last.Text + text,
                    Bold = last.Bold,
                    Italic = last.Italic,
                    Underline = last.Underline,
                    FontSizePoints = last.FontSizePoints,
                    FontFamilyName = last.FontFamilyName
                };
                return;
            }
        }

        target.Add(new TextRunLayout
        {
            Text = text,
            Bold = style.Bold,
            Italic = style.Italic,
            Underline = style.Underline,
            FontSizePoints = style.FontSizePoints,
            FontFamilyName = style.FontFamilyName
        });
    }
}

internal static class MeasuredCharacterGroupingExtensions
{
    public static IEnumerable<MeasuredCharacterGroup> GroupAdjacentByStyle(
        this IReadOnlyList<MeasuredCharacter> characters)
    {
        if (characters.Count == 0)
            yield break;

        var currentStyle = characters[0].Style;
        var currentCharacters = new List<MeasuredCharacter>();
        foreach (var character in characters)
        {
            if (character.Style != currentStyle && currentCharacters.Count > 0)
            {
                yield return new MeasuredCharacterGroup(currentStyle, currentCharacters.ToList());
                currentCharacters.Clear();
                currentStyle = character.Style;
            }

            currentCharacters.Add(character);
        }

        if (currentCharacters.Count > 0)
            yield return new MeasuredCharacterGroup(currentStyle, currentCharacters);
    }
}

internal sealed record MeasuredCharacterGroup(
    MeasuredTextStyle Style,
    IReadOnlyList<MeasuredCharacter> Characters);
