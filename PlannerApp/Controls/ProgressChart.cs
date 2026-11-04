using System.Windows.Input;
using PlannerApp.Helpers;

namespace PlannerApp.Controls;

public sealed class ProgressChart : GraphicsView, IDrawable
{
    public static readonly BindableProperty PointsProperty = BindableProperty.Create(nameof(Points), typeof(IEnumerable<TrendPoint>), typeof(ProgressChart), null, propertyChanged: Redraw);
    public static readonly BindableProperty SelectionCommandProperty = BindableProperty.Create(nameof(SelectionCommand), typeof(ICommand), typeof(ProgressChart));
    public IEnumerable<TrendPoint>? Points { get => (IEnumerable<TrendPoint>?)GetValue(PointsProperty); set => SetValue(PointsProperty, value); }
    public ICommand? SelectionCommand { get => (ICommand?)GetValue(SelectionCommandProperty); set => SetValue(SelectionCommandProperty, value); }
    private float chartWidth;

    public ProgressChart()
    {
        Drawable = this;
        HeightRequest = 205;
        Loaded += (_, _) => { if (Application.Current is not null) Application.Current.RequestedThemeChanged += ThemeChanged; };
        Unloaded += (_, _) => { if (Application.Current is not null) Application.Current.RequestedThemeChanged -= ThemeChanged; };
        StartInteraction += (_, args) => {
            var points = Points?.ToList();
            if (points is null || points.Count == 0 || args.Touches.Length == 0 || chartWidth <= 40) return;
            var index = (int)((args.Touches[0].X - 34) / ((chartWidth - 44) / points.Count));
            if (index >= 0 && index < points.Count && SelectionCommand?.CanExecute(points[index]) == true) SelectionCommand.Execute(points[index]);
        };
    }

    private void ThemeChanged(object? sender, AppThemeChangedEventArgs args) => Invalidate();
    private static void Redraw(BindableObject target, object oldValue, object newValue)
    {
        var chart = (ProgressChart)target;
        SemanticProperties.SetDescription(chart, string.Join(". ", chart.Points?.Select(point => $"{point.Date:d. M.}: {point.ValueLabel}") ?? []));
        chart.Invalidate();
    }

    public void Draw(ICanvas canvas, RectF bounds)
    {
        chartWidth = bounds.Width;
        var points = Points?.ToList() ?? [];
        if (points.Count == 0) return;
        var dark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var muted = Color.FromArgb(dark ? "#B5C8BC" : "#64786B");
        var grid = Color.FromArgb(dark ? "#3B5144" : "#E5EAE2");
        var baseline = bounds.Height - 32;
        var height = baseline - 26;
        canvas.FontSize = 10;
        canvas.FontColor = muted;
        for (var tick = 0; tick <= 2; tick++)
        {
            var y = baseline - tick * height / 2;
            canvas.StrokeColor = grid;
            canvas.StrokeSize = 1;
            canvas.DrawLine(32, y, bounds.Width - 8, y);
            canvas.DrawString($"{tick * 50}", 0, y - 7, 27, 16, HorizontalAlignment.Right, VerticalAlignment.Center);
        }
        var slot = (bounds.Width - 44) / points.Count;
        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            var width = Math.Min(36, slot * .62f);
            var x = 34 + index * slot + (slot - width) / 2;
            var barHeight = point.Progress is null ? 5 : Math.Max(4, (float)Math.Clamp(point.Progress.Value, 0, 1) * height);
            canvas.FillColor = point.Progress is null ? grid : Color.FromArgb(dark ? "#A6CFB4" : "#38745D");
            canvas.FillRoundedRectangle(x, baseline - barHeight, width, barHeight, 5);
            canvas.FontColor = muted;
            if (points.Count <= 12 || index % 7 == 0 || index == points.Count - 1)
                canvas.DrawString(points.Count > 12 ? point.Date.Day.ToString() : point.Label, 34 + index * slot - 3, baseline + 8, Math.Max(slot, 18), 20, HorizontalAlignment.Center, VerticalAlignment.Center);
            if (points.Count <= 12)
                canvas.DrawString(point.Progress is null ? "–" : $"{Math.Round(point.Progress.Value * 100)}", 34 + index * slot, baseline - barHeight - 23, slot, 20, HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }
}
