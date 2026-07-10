namespace KKL.WordStudio.Application.Excel;

using KKL.WordStudio.Domain.DataSources;

/// <summary>Single worksheet-load decision table for source range initialization.</summary>
public static class ExcelRangeLoadPolicy
{
    public static ExcelRangeLoadAction Decide(Worksheet? worksheet)
    {
        if (worksheet?.WorkingData is not null) return ExcelRangeLoadAction.UseWorkingData;
        if (worksheet?.SelectedRange is not null) return ExcelRangeLoadAction.UsePersistedRange;
        return ExcelRangeLoadAction.AutoDetect;
    }
}

public enum ExcelRangeLoadAction
{
    UseWorkingData,
    UsePersistedRange,
    AutoDetect
}
