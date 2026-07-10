namespace KKL.WordStudio.Application.Tests;

using System.Reflection;
using System.Text.Json.Serialization;
using KKL.WordStudio.Application.Abstractions;
using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Formatting;
using KKL.WordStudio.Application.Layout;
using KKL.WordStudio.Application.Tables;
using KKL.WordStudio.Domain.DataBinding;
using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Expressions;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Reports;
using KKL.WordStudio.Domain.Styling;
using KKL.WordStudio.Shared.Results;
using Xunit;

public sealed class Sprint16ContractBootstrapTests
{
    [Fact]
    public void Project_HasPersistedReferenceFormatAssetIdentity()
    {
        var project = new Project
        {
            ReferenceFormat = new ReferenceFormatDocument
            {
                FileName = "Sero.docx",
                OriginalSourcePath = @"C:\\Templates\\Sero.docx"
            }
        };

        Assert.NotNull(project.ReferenceFormat);
        Assert.Equal("Sero.docx", project.ReferenceFormat.FileName);
        Assert.Equal(ReferenceFormatDocument.DefaultEmbeddedAssetEntryName, project.ReferenceFormat.EmbeddedAssetEntryName);
    }

    [Fact]
    public void ReferenceFormatDocument_UsesProjectOwnedEmbeddedEntryAndJsonIgnore()
    {
        var property = typeof(ReferenceFormatDocument).GetProperty(nameof(ReferenceFormatDocument.ResolvedFilePath));

        Assert.Equal("resources/reference-format/reference-format.docx", ReferenceFormatDocument.DefaultEmbeddedAssetEntryName);
        Assert.NotNull(property);
        Assert.NotNull(property!.GetCustomAttribute<JsonIgnoreAttribute>());
    }

    [Fact]
    public void TableElement_HasPersistedReferenceTableFormatKey()
    {
        var table = new TableElement { ReferenceTableFormatKey = "reference-table-2" };

        Assert.Equal("reference-table-2", table.ReferenceTableFormatKey);
    }

    [Fact]
    public async Task NoReferenceProvider_ReturnsNoProfileAndNotMissing()
    {
        var result = await new NoReferenceDocumentFormatProvider().ReadAsync(new Project());

        Assert.Null(result.Profile);
        Assert.False(result.IsMissing);
        Assert.Null(result.StatusMessage);
    }

    [Fact]
    public void DefaultResolver_PreservesExistingAuthoredTextAndPageLayout()
    {
        var resolver = new DefaultReportContentFormatResolver();
        var style = new Style
        {
            FontFamily = "Arial",
            FontSize = 13,
            Bold = true,
            Italic = true,
            Underline = true,
            ForegroundColor = "#FF112233",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var authored = CreatePageLayout(width: 200, headerDistance: 9, footerDistance: 8);

        var text = resolver.ResolveText(null, ReportContentKind.Paragraph, style);
        var page = resolver.ResolvePageLayout(null, authored);

        Assert.Equal("Arial", text.FontFamilyName);
        Assert.Equal(13, text.FontSizePoints);
        Assert.True(text.Bold);
        Assert.True(text.Italic);
        Assert.True(text.Underline);
        Assert.Equal("#FF112233", text.ForegroundColor);
        Assert.Equal(ParagraphAlignment.Center, text.Alignment);
        Assert.Same(authored, page);
    }

    [Fact]
    public void SharedContentAndPayloadContracts_DefaultToCompatibilityFormatsAndWarnings()
    {
        var text = new TextContentNode
        {
            ElementId = Guid.NewGuid(),
            Kind = ReportContentKind.Paragraph,
            Text = "Body"
        };
        var table = new TableContentNode
        {
            ElementId = Guid.NewGuid(),
            Kind = ReportContentKind.Table,
            Name = "Table",
            ColumnHeaders = [],
            Rows = []
        };
        var document = new ReportContentDocument
        {
            HeaderNodes = [],
            BodyNodes = [text, table],
            FooterNodes = [],
            TableOfContents = [],
            PageLayout = CreatePageLayout()
        };
        var textPayload = new TextPageBlockPayload
        {
            Runs = [],
            SemanticKind = ReportContentKind.Paragraph,
            Alignment = ParagraphAlignment.Left
        };
        var tablePayload = new TablePageBlockPayload
        {
            Name = "Table",
            Caption = null,
            ColumnHeaders = [],
            Rows = [],
            StartRowIndex = 0,
            HasHeader = false,
            IsHeaderRepeated = false,
            SourceError = null
        };

        Assert.NotNull(text.Format);
        Assert.NotNull(table.Format);
        Assert.Null(table.CaptionFormat);
        Assert.Null(table.CaptionSequence);
        Assert.Empty(document.FormatWarnings);
        Assert.NotNull(textPayload.Format);
        Assert.NotNull(tablePayload.Format);
        Assert.Null(tablePayload.CaptionFormat);
        Assert.Equal(12.7d, document.PageLayout.HeaderDistanceMillimeters);
        Assert.Equal(12.7d, document.PageLayout.FooterDistanceMillimeters);
    }

    [Fact]
    public async Task ReportContentBuilder_ResolvesFormatsAfterCompositionAndPropagatesProfileWarnings()
    {
        var project = new Project();
        var (report, table) = CreateReportWithTextAndTable();
        var profile = CreateProfile("Reference warning");
        var provider = new SpyFormatProvider(profile, isMissing: false, status: null);
        var resolver = new SpyFormatResolver();
        var composer = new SpyComposer();
        var builder = new ReportContentBuilder(new NoOpRegistry(), composer, provider, resolver);

        var document = await builder.BuildAsync(project, report);

        Assert.Equal(1, provider.CallCount);
        Assert.Equal(1, composer.CallCount);
        Assert.Equal(1, resolver.TextCallCount);
        Assert.Equal(1, resolver.TableCallCount);
        Assert.Equal(1, resolver.PageCallCount);
        Assert.Same(profile, resolver.LastTextProfile);
        Assert.Same(profile, resolver.LastTableProfile);
        Assert.Same(profile, resolver.LastPageProfile);
        var textNode = Assert.IsType<TextContentNode>(document.BodyNodes[0]);
        var tableNode = Assert.IsType<TableContentNode>(document.BodyNodes[1]);
        Assert.Same(resolver.TextFormat, textNode.Format);
        Assert.Same(resolver.TableFormat, tableNode.Format);
        Assert.Same(profile.TableCaptionSequence, tableNode.CaptionSequence);
        Assert.Equal("COMPOSED", Assert.Single(tableNode.Rows)[0]);
        Assert.Same(resolver.PageLayout, document.PageLayout);
        Assert.Equal(new[] { "Reference warning" }, document.FormatWarnings);
    }

    [Fact]
    public async Task ReportContentBuilder_TransportsResolvedCaptionFormat()
    {
        var project = new Project();
        var (report, _) = CreateReportWithTextAndTable();
        var profile = CreateProfile();
        var builder = new ReportContentBuilder(
            new NoOpRegistry(),
            new PassthroughTableContentRowComposer(),
            new SpyFormatProvider(profile, isMissing: false, status: null),
            new SpyFormatResolver());

        var document = await builder.BuildAsync(project, report);

        var table = Assert.IsType<TableContentNode>(document.BodyNodes.Single(node => node.Kind == ReportContentKind.Table));
        Assert.Same(profile.TableCaption, table.CaptionFormat);
    }

    [Fact]
    public async Task ReportContentBuilder_DirectConstructors_KeepNoReferenceFallbackCompatibility()
    {
        var project = new Project();
        var (report, _) = CreateReportWithTextAndTable();

        var oneArgument = await new ReportContentBuilder(new NoOpRegistry()).BuildAsync(project, report);
        var twoArgument = await new ReportContentBuilder(new NoOpRegistry(), new PassthroughTableContentRowComposer()).BuildAsync(project, report);

        Assert.Empty(oneArgument.FormatWarnings);
        Assert.Empty(twoArgument.FormatWarnings);
        Assert.All(oneArgument.BodyNodes.OfType<TextContentNode>(), node => Assert.NotNull(node.Format));
        Assert.All(twoArgument.BodyNodes.OfType<TableContentNode>(), node => Assert.NotNull(node.Format));
    }

    [Fact]
    public async Task ReportContentBuilder_MissingReferenceStatus_FlowsToFormatWarnings()
    {
        var project = new Project();
        var (report, _) = CreateReportWithTextAndTable();
        var builder = new ReportContentBuilder(
            new NoOpRegistry(),
            new PassthroughTableContentRowComposer(),
            new SpyFormatProvider(null, isMissing: true, status: "Biçim şablonu bulunamadı."),
            new DefaultReportContentFormatResolver());

        var document = await builder.BuildAsync(project, report);

        Assert.Equal(new[] { "Biçim şablonu bulunamadı." }, document.FormatWarnings);
    }

    [Fact]
    public async Task ReportContentBuilder_SourceError_DoesNotComposePartialRows_ButStillResolvesTableFormat()
    {
        var project = new Project();
        var source = new ExcelDataSource
        {
            Name = "Source A",
            Workbook = new Workbook { FileName = "a.xlsx" },
            ActiveWorksheetName = "Sheet1"
        };
        source.Workbook.Worksheets.Add(new Worksheet { Name = "Sheet1" });
        project.DataSources.Add(source);
        var report = new Report();
        var page = new Page();
        var section = new Section { Kind = SectionKind.Body };
        var table = new TableElement { Name = "Table" };
        var column = new TableColumn { Header = "PN", SourceField = "PN" };
        table.Columns.Add(column);
        table.Sources.Add(CreateSourceBinding(source.Name, column.Id, "PN"));
        table.Sources.Add(CreateSourceBinding("Missing", column.Id, "PN"));
        section.Root.Children.Add(table);
        page.Sections.Add(section);
        report.Pages.Add(page);
        var composer = new SpyComposer();
        var resolver = new SpyFormatResolver();
        var builder = new ReportContentBuilder(
            new FakeRegistry(new FakeProvider()),
            composer,
            new SpyFormatProvider(CreateProfile(), isMissing: false, status: null),
            resolver);

        var document = await builder.BuildAsync(project, report);

        Assert.Equal(0, composer.CallCount);
        Assert.Equal(1, resolver.TableCallCount);
        var node = Assert.IsType<TableContentNode>(Assert.Single(document.BodyNodes));
        Assert.NotNull(node.SourceError);
        Assert.Same(resolver.TableFormat, node.Format);
    }

    private static (Report Report, TableElement Table) CreateReportWithTextAndTable()
    {
        var report = new Report();
        var page = new Page();
        var section = new Section { Kind = SectionKind.Body };
        section.Root.Children.Add(new TextElement
        {
            Name = "Text",
            Content = Expression.Literal("Hello"),
            Style = new Style { FontFamily = "Arial", FontSize = 11, Bold = true }
        });
        var table = new TableElement { Name = "Table", Caption = "Caption" };
        table.Columns.Add(new TableColumn { Header = "PN", SourceField = "PN" });
        section.Root.Children.Add(table);
        page.Sections.Add(section);
        report.Pages.Add(page);
        return (report, table);
    }

    private static TableSourceBinding CreateSourceBinding(string sourceName, Guid columnId, string field)
    {
        var binding = new TableSourceBinding
        {
            DataSourceName = sourceName,
            WorksheetName = "Sheet1",
            Range = new KKL.WordStudio.Domain.DataSources.DataRange
            {
                DataStartRow = 1,
                DataEndRow = 1,
                StartColumn = 1,
                EndColumn = 1
            }
        };
        binding.FieldMappings.Add(new TableSourceFieldMapping { TableColumnId = columnId, SourceField = field });
        return binding;
    }

    private static PageLayout CreatePageLayout(
        double width = 210,
        double headerDistance = 12.7,
        double footerDistance = 12.7) => new()
    {
        WidthMillimeters = width,
        HeightMillimeters = 297,
        MarginTopMillimeters = 20,
        MarginBottomMillimeters = 20,
        MarginLeftMillimeters = 20,
        MarginRightMillimeters = 20,
        HeaderDistanceMillimeters = headerDistance,
        FooterDistanceMillimeters = footerDistance,
        ShowPageNumbers = true
    };

    private static DocumentFormatProfile CreateProfile(params string[] warnings) => new()
    {
        Page = new PageFormatProfile
        {
            WidthMillimeters = 210,
            HeightMillimeters = 297,
            MarginTopMillimeters = 25,
            MarginBottomMillimeters = 25,
            MarginLeftMillimeters = 25,
            MarginRightMillimeters = 25,
            HeaderDistanceMillimeters = 12.49,
            FooterDistanceMillimeters = 12.49
        },
        PrimaryHeading = DefaultFormatProfiles.BodyText,
        SecondaryHeading = DefaultFormatProfiles.BodyText,
        BodyText = DefaultFormatProfiles.BodyText,
        TableCaption = DefaultFormatProfiles.BodyText,
        TableCaptionSequence = new TableCaptionSequenceProfile
        {
            DisplayLabel = "Tablo",
            SequenceIdentifier = "Tablo",
            Separator = ". "
        },
        TableFormats = [],
        Warnings = warnings
    };

    private sealed class SpyFormatProvider : IReferenceDocumentFormatProvider
    {
        private readonly DocumentFormatProfile? profile;
        private readonly bool isMissing;
        private readonly string? status;

        public SpyFormatProvider(DocumentFormatProfile? profile, bool isMissing, string? status)
        {
            this.profile = profile;
            this.isMissing = isMissing;
            this.status = status;
        }

        public int CallCount { get; private set; }

        public Task<ReferenceDocumentFormatResult> ReadAsync(Project project, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new ReferenceDocumentFormatResult
            {
                Profile = profile,
                IsMissing = isMissing,
                StatusMessage = status
            });
        }
    }

    private sealed class SpyFormatResolver : IReportContentFormatResolver
    {
        public ResolvedTextFormat TextFormat { get; } = new()
        {
            FontFamilyName = "Reference",
            FontSizePoints = 12,
            Bold = true,
            Italic = true,
            Underline = false,
            ForegroundColor = "#FF000000",
            Alignment = ParagraphAlignment.Center,
            SpaceBeforePoints = 4,
            SpaceAfterPoints = 2,
            LineSpacingMultiple = 1,
            LeftIndentMillimeters = 0,
            FirstLineIndentMillimeters = 0,
            KeepWithNext = true
        };

        public ResolvedTableFormat TableFormat { get; } = new()
        {
            WidthPercent = 100,
            FixedLayout = true,
            BorderSizePoints = 0.5,
            CellMarginTopMillimeters = 0,
            CellMarginBottomMillimeters = 0,
            CellMarginLeftMillimeters = 1.235,
            CellMarginRightMillimeters = 1.235,
            PreferredRowHeightMillimeters = 10.195,
            RepeatHeader = true,
            Columns = []
        };

        public PageLayout PageLayout { get; } = CreatePageLayout(width: 211, headerDistance: 12.49, footerDistance: 12.49);
        public int TextCallCount { get; private set; }
        public int TableCallCount { get; private set; }
        public int PageCallCount { get; private set; }
        public DocumentFormatProfile? LastTextProfile { get; private set; }
        public DocumentFormatProfile? LastTableProfile { get; private set; }
        public DocumentFormatProfile? LastPageProfile { get; private set; }

        public ResolvedTextFormat ResolveText(DocumentFormatProfile? profile, ReportContentKind kind, Style elementStyle)
        {
            TextCallCount++;
            LastTextProfile = profile;
            return TextFormat;
        }

        public ResolvedTableFormat ResolveTable(DocumentFormatProfile? profile, TableElement table)
        {
            TableCallCount++;
            LastTableProfile = profile;
            return TableFormat;
        }

        public PageLayout ResolvePageLayout(DocumentFormatProfile? profile, PageLayout authoredLayout)
        {
            PageCallCount++;
            LastPageProfile = profile;
            return PageLayout;
        }
    }

    private sealed class SpyComposer : ITableContentRowComposer
    {
        public int CallCount { get; private set; }

        public TableRowCompositionResult Compose(TableElement table, IReadOnlyList<IReadOnlyList<string>> normalizedRows)
        {
            CallCount++;
            return new TableRowCompositionResult
            {
                Rows = [new[] { "COMPOSED" }],
                CellSpans = [],
                RowGroups = [],
                Warnings = []
            };
        }
    }

    private sealed class NoOpRegistry : IDataProviderRegistry
    {
        public void Register(IDataProvider provider) { }
        public IDataProvider Resolve(string providerKey) => throw new InvalidOperationException();
    }

    private sealed class FakeRegistry : IDataProviderRegistry
    {
        private readonly IDataProvider provider;
        public FakeRegistry(IDataProvider provider) => this.provider = provider;
        public void Register(IDataProvider value) { }
        public IDataProvider Resolve(string providerKey) => provider;
    }

    private sealed class FakeProvider : IDataProvider
    {
        public string ProviderKey => "excel";
        public Task<Result<IReadOnlyList<IReadOnlyDictionary<string, object?>>>> GetRowsAsync(
            IDataSourceDefinition definition,
            CancellationToken cancellationToken = default,
            string? worksheetNameOverride = null,
            KKL.WordStudio.Domain.DataSources.DataRange? rangeOverride = null) =>
            Task.FromResult(Result.Success<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(
            [
                new Dictionary<string, object?> { ["PN"] = "A" }
            ]));
    }
}
