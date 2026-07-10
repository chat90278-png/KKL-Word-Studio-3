namespace KKL.WordStudio.Architecture.Tests;

using System.Reflection;
using System.Runtime.CompilerServices;
using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Formatting;
using KKL.WordStudio.Application.ImportedDocuments;
using KKL.WordStudio.Application.Layout;
using KKL.WordStudio.Application.Preview;
using KKL.WordStudio.Application.Tables;
using KKL.WordStudio.Domain.Projects;
using Xunit;

public sealed class FrozenContractShapeTests
{
    [Fact]
    public void LayoutContract_MatchesFrozenSprint14Shape()
    {
        AssertNamespace<IDocumentLayoutEngine>("KKL.WordStudio.Application.Layout");
        AssertNamespace<DocumentLayoutRequest>("KKL.WordStudio.Application.Layout");
        AssertNamespace<DocumentLayoutResult>("KKL.WordStudio.Application.Layout");
        AssertNamespace<DocumentPageLayout>("KKL.WordStudio.Application.Layout");
        AssertNamespace<PositionedPageBlock>("KKL.WordStudio.Application.Layout");
        AssertNamespace<PageBlockPayload>("KKL.WordStudio.Application.Layout");
        AssertNamespace<TextRunLayout>("KKL.WordStudio.Application.Layout");
        AssertNamespace<LaidOutTocEntry>("KKL.WordStudio.Application.Layout");

        Assert.True(typeof(PageBlockPayload).IsAbstract, "PageBlockPayload must remain abstract.");
        Assert.True(typeof(IDocumentLayoutEngine).IsInterface, "IDocumentLayoutEngine must remain an interface.");

        AssertRequiredProperties<DocumentLayoutRequest>(
            ("ReportContent", typeof(ReportContentDocument)),
            ("FrontMatter", typeof(ImportedDocumentPreviewDocument)));
        AssertRequiredProperties<DocumentLayoutResult>(
            ("Pages", typeof(IReadOnlyList<DocumentPageLayout>)),
            ("Warnings", typeof(IReadOnlyList<string>)));
        AssertRequiredProperties<DocumentPageLayout>(
            ("PageNumber", typeof(int)),
            ("Origin", typeof(DocumentPageOrigin)),
            ("PageLayout", typeof(PageLayout)),
            ("Blocks", typeof(IReadOnlyList<PositionedPageBlock>)));
        AssertRequiredProperties<PositionedPageBlock>(
            ("ElementId", typeof(Guid?)),
            ("Region", typeof(DocumentPageRegion)),
            ("Kind", typeof(PageBlockKind)),
            ("XMillimeters", typeof(double)),
            ("YMillimeters", typeof(double)),
            ("WidthMillimeters", typeof(double)),
            ("HeightMillimeters", typeof(double)),
            ("FragmentIndex", typeof(int)),
            ("IsContinuation", typeof(bool)),
            ("IsEditableReportElement", typeof(bool)),
            ("Payload", typeof(PageBlockPayload)));

        AssertEnumValues<DocumentPageOrigin>("FrontMatter", "GeneratedReport");
        AssertEnumValues<DocumentPageRegion>("Header", "Body", "Footer");
        AssertEnumValues<PageBlockKind>("Text", "Table", "TableOfContents", "Image", "PageNumber", "Unsupported");
        AssertEnumValues<ParagraphAlignment>("Left", "Center", "Right", "Justify");

        AssertPayloadWithOptionalProperties<TextPageBlockPayload>(
            required:
            [
                ("Runs", typeof(IReadOnlyList<TextRunLayout>)),
                ("SemanticKind", typeof(ReportContentKind?)),
                ("Alignment", typeof(ParagraphAlignment))
            ],
            optional:
            [
                ("Format", typeof(ResolvedTextFormat))
            ]);
        AssertRequiredProperties<TextRunLayout>(
            ("Text", typeof(string)),
            ("Bold", typeof(bool)),
            ("Italic", typeof(bool)),
            ("Underline", typeof(bool)),
            ("FontSizePoints", typeof(double)),
            ("FontFamilyName", typeof(string)));
        AssertPayloadWithOptionalProperties<TablePageBlockPayload>(
            required:
            [
                ("Name", typeof(string)),
                ("Caption", typeof(string)),
                ("ColumnHeaders", typeof(IReadOnlyList<string>)),
                ("Rows", typeof(IReadOnlyList<IReadOnlyList<string>>)),
                ("StartRowIndex", typeof(int)),
                ("HasHeader", typeof(bool)),
                ("IsHeaderRepeated", typeof(bool)),
                ("SourceError", typeof(string))
            ],
            optional:
            [
                ("CellSpans", typeof(IReadOnlyList<TableCellSpan>)),
                ("Format", typeof(ResolvedTableFormat)),
                ("CaptionFormat", typeof(ResolvedTextFormat)),
                ("CaptionSequence", typeof(TableCaptionSequenceProfile)),
                ("CaptionSequenceNumber", typeof(int?))
            ]);
        AssertPayload<TocPageBlockPayload>(("Entries", typeof(IReadOnlyList<LaidOutTocEntry>)));
        AssertRequiredProperties<LaidOutTocEntry>(
            ("ElementId", typeof(Guid)),
            ("Text", typeof(string)),
            ("Level", typeof(int)),
            ("PageNumber", typeof(int)));
        AssertPayload<ImagePageBlockPayload>(
            ("Name", typeof(string)),
            ("ImageBytes", typeof(byte[])),
            ("ContentType", typeof(string)),
            ("IntrinsicWidthMillimeters", typeof(double?)),
            ("IntrinsicHeightMillimeters", typeof(double?)));
        AssertPayload<PageNumberPageBlockPayload>(("PageNumber", typeof(int)));
        AssertPayload<UnsupportedPageBlockPayload>(("Description", typeof(string)));

        Assert.Single(typeof(IDocumentLayoutEngine).GetMethods());
        var method = typeof(IDocumentLayoutEngine).GetMethod(nameof(IDocumentLayoutEngine.LayoutAsync));
        Assert.NotNull(method);
        Assert.Equal(typeof(Task<DocumentLayoutResult>), method.ReturnType);
        AssertParameterShape(method, typeof(DocumentLayoutRequest), typeof(CancellationToken));
    }

    [Fact]
    public void ImportedDocumentContract_MatchesFrozenSprint14Shape()
    {
        AssertNamespace<IImportedDocumentPreviewProvider>("KKL.WordStudio.Application.ImportedDocuments");
        AssertNamespace<ImportedDocumentPreviewResult>("KKL.WordStudio.Application.ImportedDocuments");
        AssertNamespace<ImportedDocumentPreviewDocument>("KKL.WordStudio.Application.ImportedDocuments");
        AssertNamespace<ImportedDocumentSection>("KKL.WordStudio.Application.ImportedDocuments");
        AssertNamespace<ImportedDocumentBlock>("KKL.WordStudio.Application.ImportedDocuments");
        AssertNamespace<ImportedTextRun>("KKL.WordStudio.Application.ImportedDocuments");

        Assert.True(typeof(ImportedDocumentBlock).IsAbstract, "ImportedDocumentBlock must remain abstract.");
        Assert.True(typeof(IImportedDocumentPreviewProvider).IsInterface, "IImportedDocumentPreviewProvider must remain an interface.");

        AssertRequiredProperties<ImportedDocumentPreviewResult>(
            ("Document", typeof(ImportedDocumentPreviewDocument)),
            ("IsMissing", typeof(bool)),
            ("StatusMessage", typeof(string)));
        AssertRequiredProperties<ImportedDocumentPreviewDocument>(
            ("Sections", typeof(IReadOnlyList<ImportedDocumentSection>)),
            ("Warnings", typeof(IReadOnlyList<string>)));
        AssertRequiredProperties<ImportedDocumentSection>(
            ("PageLayout", typeof(PageLayout)),
            ("Blocks", typeof(IReadOnlyList<ImportedDocumentBlock>)));

        AssertImportedBlock<ImportedParagraphBlock>(
            ("Runs", typeof(IReadOnlyList<ImportedTextRun>)),
            ("Alignment", typeof(ParagraphAlignment)),
            ("KeepWithNext", typeof(bool)));
        AssertRequiredProperties<ImportedTextRun>(
            ("Text", typeof(string)),
            ("Bold", typeof(bool)),
            ("Italic", typeof(bool)),
            ("Underline", typeof(bool)),
            ("FontSizePoints", typeof(double)),
            ("FontFamilyName", typeof(string)));
        AssertImportedBlock<ImportedTableBlock>(
            ("Rows", typeof(IReadOnlyList<IReadOnlyList<string>>)),
            ("RepeatFirstRow", typeof(bool)));
        AssertImportedBlock<ImportedImageBlock>(
            ("Name", typeof(string)),
            ("ImageBytes", typeof(byte[])),
            ("ContentType", typeof(string)),
            ("WidthMillimeters", typeof(double?)),
            ("HeightMillimeters", typeof(double?)));
        AssertImportedBlock<ImportedExplicitPageBreakBlock>();
        AssertImportedBlock<ImportedUnsupportedBlock>(("Description", typeof(string)));

        Assert.Single(typeof(IImportedDocumentPreviewProvider).GetMethods());
        var method = typeof(IImportedDocumentPreviewProvider).GetMethod(nameof(IImportedDocumentPreviewProvider.ReadAsync));
        Assert.NotNull(method);
        Assert.Equal(typeof(Task<ImportedDocumentPreviewResult>), method.ReturnType);
        AssertParameterShape(method, typeof(Project), typeof(CancellationToken));
    }

    [Fact]
    public void PreviewSnapshot_RetainsCompatibilityPropertiesAndFrozenLayout()
    {
        AssertRequiredProperties<PreviewSnapshot>(
            ("HeaderBlocks", typeof(IReadOnlyList<PreviewBlock>)),
            ("BodyBlocks", typeof(IReadOnlyList<PreviewBlock>)),
            ("FooterBlocks", typeof(IReadOnlyList<PreviewBlock>)),
            ("TableOfContents", typeof(IReadOnlyList<TocEntry>)),
            ("PageLayout", typeof(PageLayout)),
            ("Layout", typeof(DocumentLayoutResult)));
    }

    private static void AssertPayload<T>(params (string Name, Type Type)[] properties) where T : PageBlockPayload
    {
        Assert.True(typeof(T).IsSealed, $"{typeof(T).Name} must remain a concrete sealed payload.");
        Assert.Equal(typeof(PageBlockPayload), typeof(T).BaseType);
        AssertRequiredProperties<T>(properties);
    }

    private static void AssertPayloadWithOptionalProperties<T>(
        IReadOnlyList<(string Name, Type Type)> required,
        IReadOnlyList<(string Name, Type Type)> optional) where T : PageBlockPayload
    {
        Assert.True(typeof(T).IsSealed, $"{typeof(T).Name} must remain a concrete sealed payload.");
        Assert.Equal(typeof(PageBlockPayload), typeof(T).BaseType);

        var declaredProperties = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        var expected = required.Concat(optional).ToDictionary(item => item.Name, item => item.Type, StringComparer.Ordinal);
        var actualNames = declaredProperties.Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        Assert.True(
            expected.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(actualNames),
            $"Frozen contract drift on {typeof(T).FullName}. Expected properties [{string.Join(", ", expected.Keys.OrderBy(name => name, StringComparer.Ordinal))}], " +
            $"actual [{string.Join(", ", actualNames.OrderBy(name => name, StringComparer.Ordinal))}].");

        foreach (var property in declaredProperties)
        {
            Assert.Equal(expected[property.Name], property.PropertyType);
            Assert.True(property.CanRead && property.CanWrite, $"Frozen contract drift: {typeof(T).Name}.{property.Name} must remain publicly readable and settable/init-only.");
        }

        foreach (var (name, _) in required)
        {
            var property = declaredProperties.Single(candidate => candidate.Name == name);
            Assert.True(property.IsDefined(typeof(RequiredMemberAttribute), inherit: false),
                $"Frozen contract drift: {typeof(T).Name}.{name} must remain a required member.");
        }

        foreach (var (name, _) in optional)
        {
            var property = declaredProperties.Single(candidate => candidate.Name == name);
            Assert.False(property.IsDefined(typeof(RequiredMemberAttribute), inherit: false),
                $"Frozen contract drift: {typeof(T).Name}.{name} must remain default-compatible rather than required.");
        }
    }

    private static void AssertImportedBlock<T>(params (string Name, Type Type)[] properties) where T : ImportedDocumentBlock
    {
        Assert.True(typeof(T).IsSealed, $"{typeof(T).Name} must remain a concrete sealed imported-document block.");
        Assert.Equal(typeof(ImportedDocumentBlock), typeof(T).BaseType);
        AssertRequiredProperties<T>(properties);
    }

    private static void AssertRequiredProperties<T>(params (string Name, Type Type)[] expected)
    {
        var declaredProperties = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        var expectedNames = expected.Select(item => item.Name).ToHashSet(StringComparer.Ordinal);
        var actualNames = declaredProperties.Select(property => property.Name).ToHashSet(StringComparer.Ordinal);

        Assert.True(
            expectedNames.SetEquals(actualNames),
            $"Frozen contract drift on {typeof(T).FullName}. Expected properties [{string.Join(", ", expectedNames.OrderBy(name => name, StringComparer.Ordinal))}], " +
            $"actual [{string.Join(", ", actualNames.OrderBy(name => name, StringComparer.Ordinal))}].");

        foreach (var (name, expectedType) in expected)
        {
            var property = declaredProperties.Single(candidate => candidate.Name == name);
            Assert.Equal(expectedType, property.PropertyType);
            Assert.True(property.CanRead && property.CanWrite, $"Frozen contract drift: {typeof(T).Name}.{name} must remain publicly readable and settable/init-only.");
            Assert.True(
                property.IsDefined(typeof(RequiredMemberAttribute), inherit: false),
                $"Frozen contract drift: {typeof(T).Name}.{name} must remain a required member.");
        }
    }

    private static void AssertParameterShape(MethodInfo method, params Type[] expectedTypes)
    {
        var parameters = method.GetParameters();
        Assert.Equal(expectedTypes.Length, parameters.Length);
        Assert.Equal(expectedTypes, parameters.Select(parameter => parameter.ParameterType));
        Assert.True(parameters[^1].IsOptional, $"{method.DeclaringType?.Name}.{method.Name} cancellation token must keep its default value.");
    }

    private static void AssertEnumValues<T>(params string[] expected) where T : struct, Enum =>
        Assert.Equal(expected, Enum.GetNames<T>());

    private static void AssertNamespace<T>(string expectedNamespace) =>
        Assert.Equal(expectedNamespace, typeof(T).Namespace);
}
