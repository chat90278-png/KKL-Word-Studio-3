namespace KKL.WordStudio.Application.Abstractions;

using KKL.WordStudio.Domain.DataBinding;
using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Shared.Results;

/// <summary>
/// Application-facing contract for retrieving actual rows from a concrete
/// data source (SQL, CSV, REST, in-memory). The Domain only knows the
/// *shape* of a data source (<see cref="IDataSourceDefinition"/>); actually
/// connecting to and querying one is an Infrastructure/plugin concern.
///
/// <paramref name="worksheetNameOverride"/> was added (Variant 2.5 UI task
/// / ADR 0009): for Excel-backed sources, the caller (ReportContentBuilder)
/// passes the Binding's own WorksheetName here so a table stays pinned to
/// the worksheet it was actually bound to, independent of whichever
/// worksheet is currently "active" on the DataSource. Providers that don't
/// have a worksheet concept (e.g. a future SQL provider) simply ignore it.
/// </summary>
public interface IDataProvider
{
    string ProviderKey { get; }

    Task<Result<IReadOnlyList<IReadOnlyDictionary<string, object?>>>> GetRowsAsync(
        IDataSourceDefinition definition, CancellationToken cancellationToken = default, string? worksheetNameOverride = null, DataRange? rangeOverride = null);
}
