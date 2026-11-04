namespace PlannerApp.Controls;

public sealed class ProgressRing : GraphicsView, IDrawable
{
    public static readonly BindableProperty ProgressProperty = BindableProperty.Create(nameof(Progress), typeof(double), typeof(ProgressRing), 0d, propertyChanged: (target, _, _) => ((ProgressRing)target).Invalidate());
    public double Progress { get => (double)GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
    public ProgressRing() { Drawable = this; HeightRequest = WidthRequest = 116; }
    public void Draw(ICanvas canvas, RectF bounds)
    {
        var size = Math.Min(bounds.Width, bounds.Height) - 16;
        var x = (bounds.Width - size) / 2;
        var y = (bounds.Height - size) / 2;
        canvas.StrokeSize = 8;
        canvas.StrokeColor = Color.FromArgb("#446454");
        canvas.DrawEllipse(x, y, size, size);
        canvas.StrokeColor = Color.FromArgb("#DEEBAC");
        if (Progress >= 1) canvas.DrawEllipse(x, y, size, size);
        else if (Progress > 0) canvas.DrawArc(x, y, size, size, 90, 90 - (float)(Math.Clamp(Progress, 0, 1) * 360), true, false);
        canvas.FontColor = Colors.White;
        canvas.FontSize = 27;
        canvas.DrawString($"{Math.Round(Progress * 100)} %", 0, 0, bounds.Width, bounds.Height, HorizontalAlignment.Center, VerticalAlignment.Center);
    }
}
