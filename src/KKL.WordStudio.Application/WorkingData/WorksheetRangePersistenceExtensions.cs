namespace KKL.WordStudio.Application.WorkingData;

using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Domain.Projects;

/// <summary>
/// Persists worksheet configuration without forcing creation of editable
/// WorkingData. Range selection is project metadata and must survive source/
/// worksheet navigation even when the user never edits a cell.
/// </summary>
public static class WorksheetRangePersistenceExtensions
{
    public static Worksheet SaveSelectedRange(
        this IWorksheetWorkingDataService service,
        Project project,
        string workbookFilePath,
        string workbookFileName,
        string worksheetName,
        DataRange range,
        string? preferredDataSourceName = null)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(range);

        var dataSource = service.FindDataSource(project, workbookFilePath);
        if (dataSource is null)
        {
            dataSource = new ExcelDataSource
            {
                Name = ResolveUniqueDataSourceName(project, workbookFileName, preferredDataSourceName),
                Workbook = new Workbook
                {
                    FileName = workbookFileName,
                    SourcePath = workbookFilePath
                },
                ActiveWorksheetName = worksheetName
            };
            project.DataSources.Add(dataSource);
        }

        dataSource.ActiveWorksheetName = worksheetName;
        var worksheet = dataSource.Workbook.Worksheets.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, worksheetName, StringComparison.OrdinalIgnoreCase));
        if (worksheet is null)
        {
            worksheet = new Worksheet { Name = worksheetName };
            dataSource.Workbook.Worksheets.Add(worksheet);
        }

        worksheet.SelectedRange = Clone(range);
        return worksheet;
    }

    private static string ResolveUniqueDataSourceName(
        Project project,
        string workbookFileName,
        string? preferredDataSourceName)
    {
        var baseName = string.IsNullOrWhiteSpace(preferredDataSourceName)
            ? Path.GetFileNameWithoutExtension(workbookFileName)
            : preferredDataSourceName.Trim();
        if (string.IsNullOrWhiteSpace(baseName))
            baseName = "Veri Kaynağı";

        var name = baseName;
        var suffix = 2;
        while (project.DataSources.Any(source =>
            string.Equals(source.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            name = $"{baseName} {suffix++}";
        }

        return name;
    }

    private static DataRange Clone(DataRange range) => new()
    {
        DataStartRow = range.DataStartRow,
        DataEndRow = range.DataEndRow,
        HeaderRowIndex = range.HeaderRowIndex,
        StartColumn = range.StartColumn,
        EndColumn = range.EndColumn,
        WasAutoDetected = range.WasAutoDetected
    };
}
