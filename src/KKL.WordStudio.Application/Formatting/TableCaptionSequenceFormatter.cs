namespace KKL.WordStudio.Application.Formatting;

/// <summary>
/// Shared deterministic presentation rules for a table-caption sequence.
/// Word remains authoritative for live SEQ fields; Preview uses the same
/// descriptive-caption normalization when showing the cached sequence number.
/// </summary>
public static class TableCaptionSequenceFormatter
{
    public static string BuildDisplayText(
        string caption,
        TableCaptionSequenceProfile? sequence,
        int? sequenceNumber)
    {
        if (sequence is null || sequenceNumber is null || sequenceNumber <= 0)
            return caption;

        var description = RemoveDeterministicManualSequencePrefix(caption, sequence);
        return $"{sequence.DisplayLabel} {sequenceNumber.Value}{sequence.Separator}{description}";
    }

    /// <summary>
    /// Avoids duplicate numbering only for the exact deterministic shape
    /// "{DisplayLabel} {positive integer}{Separator}{description}" at the start
    /// of the authored caption. Digits elsewhere are never interpreted as numbering.
    /// </summary>
    public static string RemoveDeterministicManualSequencePrefix(
        string caption,
        TableCaptionSequenceProfile sequence)
    {
        ArgumentNullException.ThrowIfNull(caption);
        ArgumentNullException.ThrowIfNull(sequence);

        var prefix = sequence.DisplayLabel + " ";
        if (!caption.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return caption;

        var numberStart = prefix.Length;
        var numberEnd = numberStart;
        while (numberEnd < caption.Length && char.IsAsciiDigit(caption[numberEnd]))
            numberEnd++;

        if (numberEnd == numberStart
            || !caption.AsSpan(numberEnd).StartsWith(sequence.Separator.AsSpan(), StringComparison.Ordinal))
        {
            return caption;
        }

        return caption[(numberEnd + sequence.Separator.Length)..];
    }
}
