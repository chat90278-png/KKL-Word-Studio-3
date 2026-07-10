namespace KKL.WordStudio.UI.Preview;

using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using KKL.WordStudio.UI.ViewModels;

/// <summary>
/// Small WPF presenter that maps layout text runs to real Run inlines. The
/// layout contract owns text semantics; this control only applies the supplied
/// run typography to WPF.
/// </summary>
public sealed class PreviewTextBlockControl : TextBlock
{
    public static readonly DependencyProperty RunsProperty = DependencyProperty.Register(
        nameof(Runs),
        typeof(IEnumerable),
        typeof(PreviewTextBlockControl),
        new PropertyMetadata(null, OnRunsChanged));

    public static readonly DependencyProperty FirstLineIndentProperty = DependencyProperty.Register(
        nameof(FirstLineIndent),
        typeof(double),
        typeof(PreviewTextBlockControl),
        new PropertyMetadata(0d, OnRunsChanged));

    public IEnumerable? Runs
    {
        get => (IEnumerable?)GetValue(RunsProperty);
        set => SetValue(RunsProperty, value);
    }

    public double FirstLineIndent
    {
        get => (double)GetValue(FirstLineIndentProperty);
        set => SetValue(FirstLineIndentProperty, value);
    }

    public PreviewTextBlockControl()
    {
        TextWrapping = TextWrapping.Wrap;
    }

    private static void OnRunsChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e) =>
        ((PreviewTextBlockControl)dependencyObject).RebuildRuns();

    private void RebuildRuns()
    {
        Inlines.Clear();
        if (FirstLineIndent > 0d)
        {
            Inlines.Add(new InlineUIContainer(new Border
            {
                Width = FirstLineIndent,
                Height = 0d,
                IsHitTestVisible = false
            })
            {
                BaselineAlignment = BaselineAlignment.Baseline
            });
        }

        if (Runs is null)
            return;

        foreach (var item in Runs)
        {
            if (item is not PreviewTextRunViewModel runViewModel)
                continue;

            var run = new Run(runViewModel.Text)
            {
                FontWeight = runViewModel.FontWeight,
                FontStyle = runViewModel.FontStyle,
                FontSize = runViewModel.FontSize,
                TextDecorations = runViewModel.TextDecorations
            };

            if (runViewModel.FontFamily is not null)
                run.FontFamily = runViewModel.FontFamily;

            Inlines.Add(run);
        }
    }
}
