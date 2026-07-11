namespace KKL.WordStudio.Engine.Tests;

using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Formatting;
using KKL.WordStudio.Application.Layout;
using KKL.WordStudio.Application.Tables;
using KKL.WordStudio.Engine.Layout;
using Xunit;

public sealed class Sprint18LongTablePaginationFidelityTests
{
    [Fact]
    public async Task A4LongCaptionedTable_RepeatsHeadersAndMovesBoundaryGroupIntact()
    {
        var profile = DefaultDocumentFormatProfileFactory.Create();
        var rows = BuildRows(rowCount: 70);
        var tableId = Guid.NewGuid();
        var table = new TableContentNode
        {
            ElementId = tableId,
            Kind = ReportContentKind.Table,
            Name = "Three page fidelity table",
            Caption = "Long document pagination fidelity",
            CaptionFormat = profile.TableCaption,
            CaptionSequence = profile.TableCaptionSequence,
            ColumnHeaders = ["No", "Product Name", "Product Number", "NSN", "Serial No", "Quantity"],
            Rows = rows,
            CellSpans = BuildBoundaryGroupSpans(),
            RowGroups =
            [
                new TableRowGroup
                {
                    StartRowIndex = 21,
                    RowCount = 2,
                    KeepTogetherWhenPossible = true
                }
            ],
            Format = profile.TableFormats[0].Format,
            SourceCount = 0
        };
        var document = new ReportContentDocument
        {
            HeaderNodes = [],
            BodyNodes = [table],
            FooterNodes = [],
            TableOfContents = [],
            PageLayout = new PageLayout
            {
                WidthMillimeters = profile.Page.WidthMillimeters,
                HeightMillimeters = profile.Page.HeightMillimeters,
                MarginTopMillimeters = profile.Page.MarginTopMillimeters,
                MarginBottomMillimeters = profile.Page.MarginBottomMillimeters,
                MarginLeftMillimeters = profile.Page.MarginLeftMillimeters,
                MarginRightMillimeters = profile.Page.MarginRightMillimeters,
                HeaderDistanceMillimeters = profile.Page.HeaderDistanceMillimeters,
                FooterDistanceMillimeters = profile.Page.FooterDistanceMillimeters,
                ShowPageNumbers = false
            }
        };

        var result = await new DeterministicDocumentLayoutEngine().LayoutAsync(new DocumentLayoutRequest
        {
            ReportContent = document,
            FrontMatter = null
        });

        Assert.True(result.Pages.Count >= 3, $"Expected at least three pages, actual: {result.Pages.Count}.");
        var tableBlocks = result.Pages
            .SelectMany(page => page.Blocks)
            .Where(block => block.ElementId == tableId && block.Kind == PageBlockKind.Table)
            .ToList();
        Assert.True(tableBlocks.Count >= 3);

        var payloads = tableBlocks
            .Select(block => Assert.IsType<TablePageBlockPayload>(block.Payload))
            .ToList();
        Assert.Equal("Long document pagination fidelity", payloads[0].Caption);
        Assert.All(payloads.Skip(1), payload => Assert.Null(payload.Caption));
        Assert.False(payloads[0].IsHeaderRepeated);
        Assert.All(payloads.Skip(1), payload => Assert.True(payload.IsHeaderRepeated));
        Assert.All(payloads, payload => Assert.Equal(1, payload.CaptionSequenceNumber));

        Assert.Equal(21, payloads[0].Rows.Count);
        var boundaryFragment = Assert.Single(payloads, payload => payload.StartRowIndex == 21);
        Assert.Equal("SER-A", boundaryFragment.Rows[0][4]);
        Assert.Equal("SER-B", boundaryFragment.Rows[1][4]);
        Assert.Equal(5, boundaryFragment.CellSpans.Count);
        Assert.All(boundaryFragment.CellSpans, span =>
        {
            Assert.Equal(0, span.RowIndex);
            Assert.Equal(2, span.RowSpan);
        });
        Assert.DoesNotContain(boundaryFragment.CellSpans, span => span.ColumnIndex == 4);
        Assert.Equal(
            new[] { 0, 1, 2, 3, 5 },
            boundaryFragment.CellSpans.Select(span => span.ColumnIndex).Order().ToArray());

        var projectedRowCount = payloads.Sum(payload => payload.Rows.Count);
        Assert.Equal(rows.Count, projectedRowCount);
    }

    private static IReadOnlyList<IReadOnlyList<string>> BuildRows(int rowCount)
    {
        var rows = new List<IReadOnlyList<string>>(rowCount);
        for (var index = 0; index < rowCount; index++)
        {
            if (index == 21)
            {
                rows.Add(["22", "Boundary Product", "P-22", "NSN-22", "SER-A", "2"]);
                continue;
            }

            if (index == 22)
            {
                rows.Add([string.Empty, string.Empty, string.Empty, string.Empty, "SER-B", string.Empty]);
                continue;
            }

            rows.Add(
            [
                (index + 1).ToString(),
                $"Product {index + 1}",
                $"P-{index + 1:000}",
                $"NSN-{index + 1:000}",
                $"SER-{index + 1:000}",
                "1"
            ]);
        }

        return rows;
    }

    private static IReadOnlyList<TableCellSpan> BuildBoundaryGroupSpans() =>
        new[] { 0, 1, 2, 3, 5 }
            .Select(columnIndex => new TableCellSpan
            {
                RowIndex = 21,
                ColumnIndex = columnIndex,
                RowSpan = 2
            })
            .ToList();
}
