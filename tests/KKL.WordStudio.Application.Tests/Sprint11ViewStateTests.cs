namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.WorkingData;
using KKL.WordStudio.Domain.DataSources;
using Xunit;

public class Sprint11ViewStateTests
{
    [Fact]
    public void PreparationFilter_DoesNotChangeReportContentRows()
    {
        var data = BuildData();
        var view = new WorkingDataViewState();
        var rowsBefore = data.Rows.Count;
        var columnsBefore = data.Columns.Count;

        // Filter column 0 to rows containing "keep".
        view.SetRowFilter(0, "keep");
        var visible = view.GetVisibleRowIndexes(data);

        // The projection hides rows in the view, but the underlying working
        // data (which is exactly what ReportContentBuilder/composition read)
        // is untouched: same row and column counts, same values.
        Assert.Equal(2, visible.Count);
        Assert.Equal(rowsBefore, data.Rows.Count);
        Assert.Equal(columnsBefore, data.Columns.Count);
        Assert.Equal("keep-1", data.Rows[0].Values[0]);
        Assert.Equal("drop-1", data.Rows[1].Values[0]);
        Assert.Equal("keep-2", data.Rows[2].Values[0]);
    }

    [Fact]
    public void FilteredRowEdit_UpdatesCorrectUnderlyingWorkingDataRow()
    {
        var data = BuildData();
        var view = new WorkingDataViewState();
        view.SetRowFilter(0, "keep");

        // Display index 1 within the filtered view is the SECOND "keep" row,
        // which is underlying working-data row index 2 — not 1.
        var underlying = view.VisibleRowToWorkingRow(data, 1);
        Assert.Equal(2, underlying);

        data.Rows[underlying].Values[1] = "edited";
        Assert.Equal("edited", data.Rows[2].Values[1]);
        // The visually-adjacent but filtered-out row 1 is not the one edited.
        Assert.NotEqual("edited", data.Rows[1].Values[1]);
    }

    [Fact]
    public void ColumnVisibility_DoesNotChangeReportColumnsOrWordOutput()
    {
        var data = BuildData();
        var view = new WorkingDataViewState();
        var columnsBefore = data.Columns.Select(c => (c.Id, c.SourceField, c.Header)).ToList();

        view.SetColumnHidden(data.Columns[1], true);
        var visibleColumns = view.GetVisibleColumnIndexes(data);

        // Hidden in the view, but the working-data columns (report/Word input)
        // are unchanged in count, identity, SourceField and Header.
        Assert.Equal(new[] { 0 }, visibleColumns);
        Assert.Equal(columnsBefore, data.Columns.Select(c => (c.Id, c.SourceField, c.Header)).ToList());

        view.RestoreAllColumns();
        Assert.Equal(new[] { 0, 1 }, view.GetVisibleColumnIndexes(data));
    }

    [Fact]
    public void ClearFilter_ShowsAllRowsAgain()
    {
        var data = BuildData();
        var view = new WorkingDataViewState();
        view.SetRowFilter(0, "keep");
        Assert.Equal(2, view.GetVisibleRowIndexes(data).Count);

        view.ClearRowFilter();
        Assert.Equal(3, view.GetVisibleRowIndexes(data).Count);
        Assert.False(view.HasRowFilter);
    }

    private static WorksheetWorkingData BuildData()
    {
        var data = new WorksheetWorkingData();
        data.Columns.Add(new WorkingDataColumn { SourceField = "A", Header = "Name", OriginalSourceColumn = "A" });
        data.Columns.Add(new WorkingDataColumn { SourceField = "B", Header = "Value", OriginalSourceColumn = "B" });
        data.Rows.Add(Row(2, "keep-1", "v1"));
        data.Rows.Add(Row(3, "drop-1", "v2"));
        data.Rows.Add(Row(4, "keep-2", "v3"));
        return data;
    }

    private static WorkingDataRow Row(int number, params string?[] values)
    {
        var row = new WorkingDataRow { OriginalRowNumber = number };
        foreach (var value in values) row.Values.Add(value);
        return row;
    }
}
