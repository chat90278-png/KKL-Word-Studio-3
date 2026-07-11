namespace KKL.WordStudio.Application.Formatting;

/// <summary>
/// Shared deterministic presentation and sequence-counting rules for table captions.
/// Word remains authoritative for live SEQ fields; Preview uses the same
/// descriptive-caption normalization and document-order number semantics.
/// </summary>
public static class TableCaptionSequenceFormatter
{
    public static int? PeekNextSequenceNumber(
        string? caption,
        TableCaptionSequenceProfile? sequence,
        IDictionary<string, int> sequenceCounters)
    {
        ArgumentNullException.ThrowIfNull(sequenceCounters);

        if (string.IsNullOrWhiteSpace(caption)
            || sequence is null
            || string.IsNullOrWhiteSpace(sequence.SequenceIdentifier))
        {
            return null;
        }

        sequenceCounters.TryGetValue(sequence.SequenceIdentifier, out var current);
        return current + 1;
    }

    public static int? ResolveNextSequenceNumber(
        string? caption,
        TableCaptionSequenceProfile? sequence,
        IDictionary<string, int> sequenceCounters)
    {
        ArgumentNullException.ThrowIfNull(sequenceCounters);

        var next = PeekNextSequenceNumber(caption, sequence, sequenceCounters);
        if (next is null || sequence is null)
            return null;

        sequenceCounters[sequence.SequenceIdentifier] = next.Value;
        return next;
    }

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
