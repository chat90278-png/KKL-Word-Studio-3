namespace KKL.WordStudio.Application.Editing;

using KKL.WordStudio.Application.Styling;
using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Expressions;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Reports;
using KKL.WordStudio.Domain.Visitors;
using KKL.WordStudio.Shared.Results;

/// <summary>
/// The commit side of the Sprint 7 interactive report design surface.
/// Inline editors in the Preview only capture text — the actual Domain
/// mutation lives here so it's (a) testable without WPF and (b) shared with
/// any future editor entry point. Both operations mutate the REAL report
/// elements; the preview never edits its own block ViewModels directly, so
/// Preview refresh and Word export always flow through the one shared
/// ReportContentBuilder interpretation.
/// </summary>
public interface IReportEditingService
{
    /// <summary>Commits an inline heading/subheading (or header/footer text) edit to the underlying TextElement.</summary>
    Result CommitHeadingText(Report report, Guid elementId, string text);

    /// <summary>Commits the table's real semantic display caption/title.</summary>
    Result CommitTableCaption(Report report, Guid tableElementId, string caption);

    /// <summary>Copies the current heading text into the table caption without deleting or moving the heading.</summary>
    Result UseHeadingTextAsTableCaption(Report report, Guid tableElementId, Guid headingElementId);

    /// <summary>
    /// Renames the DISPLAYED header of a table column without changing its
    /// source identity: "Tr İsim" (source) can be displayed as "Parça Adı"
    /// while the data keeps resolving from the same source column. For a
    /// legacy bound table that has no own columns yet (pre-Sprint-7: headers
    /// came straight from DataSource.Fields), the columns are materialized
    /// first — the smallest change that separates display from identity
    /// without redesigning the table model.
    /// </summary>
    Result RenameDisplayedTableColumn(Project project, Report report, Guid tableElementId, int columnIndex, string newHeader);
}

public sealed class ReportEditingService : IReportEditingService
{
    public Result CommitHeadingText(Report report, Guid elementId, string text)
    {
        var element = ReportElementFlattener.FindById(report, elementId);
        if (element is not TextElement textElement)
            return Result.Failure("Düzenlenecek metin öğesi bulunamadı.");

        textElement.Content = Expression.Literal(text);
        return Result.Success();
    }

    public Result CommitTableCaption(Report report, Guid tableElementId, string caption)
    {
        if (ReportElementFlattener.FindById(report, tableElementId) is not TableElement table)
            return Result.Failure("Düzenlenecek tablo bulunamadı.");

        table.Caption = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim();
        return Result.Success();
    }

    public Result UseHeadingTextAsTableCaption(Report report, Guid tableElementId, Guid headingElementId)
    {
        if (ReportElementFlattener.FindById(report, tableElementId) is not TableElement table)
            return Result.Failure("Başlık atanacak tablo bulunamadı.");

        if (ReportElementFlattener.FindById(report, headingElementId) is not TextElement heading
            || (!HeadingStylePresets.IsHeading(heading.Style) && !HeadingStylePresets.IsAltHeading(heading.Style)))
            return Result.Failure("Kullanılacak başlık öğesi bulunamadı.");

        table.Caption = heading.Content.Text;
        return Result.Success();
    }

    public Result RenameDisplayedTableColumn(Project project, Report report, Guid tableElementId, int columnIndex, string newHeader)
    {
        var element = ReportElementFlattener.FindById(report, tableElementId);
        if (element is not TableElement table)
            return Result.Failure("Düzenlenecek tablo bulunamadı.");

        if (table.Columns.Count == 0 && table.Binding is not null)
            MaterializeColumnsFromDataSource(project, table);

        if (columnIndex < 0 || columnIndex >= table.Columns.Count)
            return Result.Failure("Yeniden adlandırılacak tablo sütunu bulunamadı.");

        // Only the DISPLAYED header changes. The column's SourceField (set
        // here during materialization, or earlier by the direct transfer)
        // remains the data key — this is what the
        // RenamingDisplayedTableHeader_DoesNotChangeSourceDataResolution
        // regression test locks in.
        table.Columns[columnIndex].Header = newHeader;
        return Result.Success();
    }

    /// <summary>
    /// A legacy bound table displays DataSource.Fields directly and owns no
    /// TableColumns. To rename one displayed header per-table, the field list
    /// is captured once into real TableColumns whose SourceField keeps the
    /// original field name — data resolution is bit-for-bit unchanged, only
    /// the header text becomes independently editable afterwards.
    /// </summary>
    private static void MaterializeColumnsFromDataSource(Project project, TableElement table)
    {
        var binding = table.Binding;
        if (binding is null)
            return;

        var dataSource = project.DataSources.FirstOrDefault(ds => ds.Name == binding.DataSourceName);
        if (dataSource is null)
            return;

        var fields = dataSource.Fields;
        if (dataSource is ExcelDataSource excelDataSource)
        {
            var worksheet = excelDataSource.Workbook.Worksheets.FirstOrDefault(w => w.Name == binding.WorksheetName);
            if (worksheet is not null && worksheet.ColumnMappings.Count > 0)
                fields = worksheet.ColumnMappings.Select(m => m.TargetField).ToList();
        }

        foreach (var field in fields)
            table.Columns.Add(new TableColumn { Header = field.Name, SourceField = field.Name });
    }
}
