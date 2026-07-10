namespace KKL.WordStudio.Application.DependencyInjection;

using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.DataSources;
using KKL.WordStudio.Application.Editing;
using KKL.WordStudio.Application.Excel;
using KKL.WordStudio.Application.Formatting;
using KKL.WordStudio.Application.Plugins;
using KKL.WordStudio.Application.Structure;
using KKL.WordStudio.Application.TableComposition;
using KKL.WordStudio.Application.Tables;
using KKL.WordStudio.Application.Transfer;
using KKL.WordStudio.Application.Workspace;
using KKL.WordStudio.Application.WorkingData;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Composition entry point for the Application layer's own services (as opposed to Infrastructure's).</summary>
public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddWordStudioApplication(this IServiceCollection services)
    {
        services.AddSingleton<PluginCatalog>();
        services.AddSingleton<IWorkspace, Workspace>();
        services.AddSingleton<ISerialQuantityGroupingDetector, SerialQuantityGroupingDetector>();
        services.AddSingleton<ITableContentRowComposer, SerialQuantityTableContentRowComposer>();
        services.AddSingleton<IReferenceDocumentFormatProvider, NoReferenceDocumentFormatProvider>();
        services.AddSingleton<IReportContentFormatResolver, ReferenceReportContentFormatResolver>();
        services.AddSingleton<IReportContentBuilder, ReportContentBuilder>();
        services.AddSingleton<IExcelReportTransferService, ExcelReportTransferService>();
        services.AddSingleton<IExcelDataRangeDetector, ExcelDataRangeDetector>();
        services.AddSingleton<ITableSourceCompositionService, TableSourceCompositionService>();
        services.AddSingleton<IReportEditingService, ReportEditingService>();
        services.AddSingleton<IReportStructureService, ReportStructureService>();
        services.AddSingleton<IWorksheetWorkingDataService, WorksheetWorkingDataService>();
        services.AddSingleton<IWorkingDataHistoryRegistry, WorkingDataHistoryRegistry>();
        return services;
    }
}
