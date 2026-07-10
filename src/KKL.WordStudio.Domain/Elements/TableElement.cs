namespace KKL.WordStudio.Domain.Elements;

using KKL.WordStudio.Domain.DataBinding;
using KKL.WordStudio.Domain.Visitors;

public sealed class TableElement : ReportElement
{
    public List<TableColumn> Columns { get; } = new();
    public List<TableRow> Rows { get; } = new();

    /// <summary>
    /// Stable column-role configuration for optional serial/quantity table
    /// composition. Null means the feature is not configured for this table.
    /// </summary>
    public SerialQuantityGrouping? SerialQuantityGrouping { get; set; }

    /// <summary>
    /// Optional stable key selecting one table format extracted from the project's reference DOCX. Null lets the format resolver choose a compatible profile.
    /// </summary>
    public string? ReferenceTableFormatKey { get; set; }

    /// <summary>
    /// Ordered persisted inputs for Sprint 10 multi-source composition. Empty
    /// means the legacy single <see cref="Binding"/> behavior remains
    /// authoritative. Once populated, row append order is this list order.
    /// </summary>
    public List<TableSourceBinding> Sources { get; } = new();

    /// <summary>Free-text notes for the report author — added in Sprint 3 for the Table Designer's basic property panel. Kept on TableElement specifically rather than promoted to ReportElement; promote only once a second element type genuinely needs it.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// Visible report caption/title rendered immediately above this table in
    /// Preview and Word output. It is intentionally independent from Name
    /// (designer identity) and Description (author notes).
    /// </summary>
    public string? Caption { get; set; }

    /// <summary>
    /// When set, this table is a data-bound ReportTable: its Detail row(s)
    /// repeat once per row returned by the named DataSource, with cell
    /// content populated via each cell's Expression (e.g. "=Fields.Total").
    /// Null means the table is purely static/manually authored.
    /// </summary>
    public Binding? Binding { get; set; }

    public override void Accept(IReportElementVisitor visitor) => visitor.Visit(this);
}

public sealed class TableColumn
{
    /// <summary>Stable report-column identity used by per-source normalization mappings.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>The DISPLAYED column header in the report (Preview and Word export). Purely presentational — renaming it must never change which source data the column shows (see <see cref="SourceField"/>).</summary>
    public string Header { get; set; } = string.Empty;

    /// <summary>
    /// The stable source identity this column reads its data from — either a
    /// logical field name (when the owning DataSource has ColumnMappings) or a
    /// raw column letter such as "B" (direct transfer without mappings).
    ///
    /// Added in Sprint 7 to separate "what the report shows as the header"
    /// from "which source column the data comes from": before this field
    /// existed, a bound table's headers came straight from
    /// DataSource.Fields, so a per-table display rename was impossible
    /// without corrupting data resolution. Null means "no explicit source
    /// identity" — pre-Sprint-7 tables keep resolving via DataSource.Fields
    /// exactly as before (see ReportContentBuilder).
    /// </summary>
    public string? SourceField { get; set; }

    public double Width { get; set; } = 100;
}

public sealed class TableRow
{
    public List<Container> Cells { get; } = new();
    public TableRowKind Kind { get; set; } = TableRowKind.Detail;
}

public enum TableRowKind { Header, Detail, Footer, GroupHeader, GroupFooter }
