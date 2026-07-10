namespace KKL.WordStudio.Application.DataSources;

using KKL.WordStudio.Domain.DataBinding;
using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Shared.Results;

public sealed class TableSourceDescriptor
{
    public required int Index { get; init; }
    public required string DataSourceName { get; init; }
    public required string WorksheetName { get; init; }
    public required string RangeText { get; init; }
    public required string StatusText { get; init; }
    public bool IsUsable { get; init; }
    public bool IsLegacyBinding { get; init; }
}

public interface ITableSourceCompositionService
{
    IReadOnlyList<TableSourceDescriptor> DescribeSources(Project project, TableElement table);
    Result MoveSource(TableElement table, int sourceIndex, int offset);
    Result RemoveSource(TableElement table, int sourceIndex);
}

/// <summary>
/// Small Application-layer coordinator for ordered table-source references.
/// It mutates only table source references; project DataSource ownership is
/// never affected by reorder/remove operations.
/// </summary>
public sealed class TableSourceCompositionService : ITableSourceCompositionService
{
    public IReadOnlyList<TableSourceDescriptor> DescribeSources(Project project, TableElement table)
    {
        if (table.Sources.Count > 0)
            return table.Sources.Select((source, index) => Describe(project, source, index)).ToList();

        var binding = table.Binding;
        if (binding is null) return Array.Empty<TableSourceDescriptor>();

        var dataSource = project.DataSources.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, binding.DataSourceName, StringComparison.OrdinalIgnoreCase));
        var worksheetName = binding.WorksheetName
            ?? (dataSource as ExcelDataSource)?.ActiveWorksheetName
            ?? string.Empty;
        var worksheet = (dataSource as ExcelDataSource)?.Workbook.Worksheets.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, worksheetName, StringComparison.OrdinalIgnoreCase));
        var status = ResolveStatus(dataSource, worksheet);

        return new[]
        {
            new TableSourceDescriptor
            {
                Index = 0,
                DataSourceName = binding.DataSourceName,
                WorksheetName = worksheetName,
                RangeText = worksheet?.SelectedRange?.RangeReference ?? "Aralık ayarlanmadı",
                StatusText = status.Text,
                IsUsable = status.IsUsable,
                IsLegacyBinding = true
            }
        };
    }

    public Result MoveSource(TableElement table, int sourceIndex, int offset)
    {
        if (table.Sources.Count == 0)
            return Result.Failure("Tek veri kaynağı yeniden sıralanamaz.");
        if (sourceIndex < 0 || sourceIndex >= table.Sources.Count)
            return Result.Failure("Taşınacak veri kaynağı bulunamadı.");

        var targetIndex = sourceIndex + offset;
        if (targetIndex < 0 || targetIndex >= table.Sources.Count)
            return Result.Failure("Veri kaynağı daha fazla taşınamaz.");

        var source = table.Sources[sourceIndex];
        table.Sources.RemoveAt(sourceIndex);
        table.Sources.Insert(targetIndex, source);
        return Result.Success();
    }

    public Result RemoveSource(TableElement table, int sourceIndex)
    {
        if (table.Sources.Count > 0)
        {
            if (sourceIndex < 0 || sourceIndex >= table.Sources.Count)
                return Result.Failure("Kaldırılacak veri kaynağı bulunamadı.");
            table.Sources.RemoveAt(sourceIndex);
            if (table.Sources.Count == 0)
                table.Binding = null; // prevent the preserved legacy fallback from resurrecting a removed source
            return Result.Success();
        }

        if (sourceIndex == 0 && table.Binding is not null)
        {
            table.Binding = null;
            return Result.Success();
        }

        return Result.Failure("Kaldırılacak veri kaynağı bulunamadı.");
    }

    private static TableSourceDescriptor Describe(Project project, TableSourceBinding source, int index)
    {
        var dataSource = project.DataSources.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, source.DataSourceName, StringComparison.OrdinalIgnoreCase));
        var worksheet = (dataSource as ExcelDataSource)?.Workbook.Worksheets.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, source.WorksheetName, StringComparison.OrdinalIgnoreCase));
        var status = ResolveStatus(dataSource, worksheet);

        return new TableSourceDescriptor
        {
            Index = index,
            DataSourceName = source.DataSourceName,
            WorksheetName = source.WorksheetName,
            RangeText = source.Range.RangeReference,
            StatusText = status.Text,
            IsUsable = status.IsUsable,
            IsLegacyBinding = false
        };
    }

    private static (bool IsUsable, string Text) ResolveStatus(KKL.WordStudio.Domain.DataSources.DataSource? dataSource, Worksheet? worksheet)
    {
        if (dataSource is not ExcelDataSource excelDataSource)
            return (false, "Veri kaynağı projede bulunamadı");
        if (worksheet is null)
            return (false, "Excel sayfası projede bulunamadı");
        if (worksheet.WorkingData is not null)
            return (true, "Hazır · proje çalışma verisi");
        if (!string.IsNullOrWhiteSpace(excelDataSource.Workbook.SourcePath) && System.IO.File.Exists(excelDataSource.Workbook.SourcePath))
            return (true, "Hazır");
        return (false, "Kaynak Excel bulunamadı");
    }
}
