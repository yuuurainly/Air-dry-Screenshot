using System.Windows;
using System.Windows.Media;

namespace LiangTu.App.Controls;

public sealed class RopeRail : FrameworkElement
{
    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke),
        typeof(Brush),
        typeof(RopeRail),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShadowBrushProperty = DependencyProperty.Register(
        nameof(ShadowBrush),
        typeof(Brush),
        typeof(RopeRail),
        new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SagProperty = DependencyProperty.Register(
        nameof(Sag),
        typeof(double),
        typeof(RopeRail),
        new FrameworkPropertyMetadata(7d, FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public Brush ShadowBrush
    {
        get => (Brush)GetValue(ShadowBrushProperty);
        set => SetValue(ShadowBrushProperty, value);
    }

    public double Sag
    {
        get => (double)GetValue(SagProperty);
        set => SetValue(SagProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (ActualWidth < 1 || ActualHeight < 1)
        {
            return;
        }

        var width = ActualWidth;
        var top = ActualHeight * 0.36;
        var sag = Math.Clamp(Sag, 0.5, Math.Max(0.5, ActualHeight - top - 2));
        var shadowGeometry = CreateCurve(width, top + 1.2, sag);
        var mainGeometry = CreateCurve(width, top, sag);

        drawingContext.PushOpacity(0.38);
        drawingContext.DrawGeometry(
            null,
            new Pen(ShadowBrush, 2.8) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round },
            shadowGeometry);
        drawingContext.Pop();
        drawingContext.DrawGeometry(
            null,
            new Pen(Stroke, 1.4) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round },
            mainGeometry);
    }

    private static StreamGeometry CreateCurve(double width, double baseline, double sag)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(new Point(0, baseline), isFilled: false, isClosed: false);
        context.BezierTo(
            new Point(width * 0.28, baseline + sag),
            new Point(width * 0.72, baseline + sag),
            new Point(width, baseline),
            isStroked: true,
            isSmoothJoin: true);
        geometry.Freeze();
        return geometry;
    }
}
