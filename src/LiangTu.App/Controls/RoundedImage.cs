using System.Windows;
using System.Windows.Media;

namespace LiangTu.App.Controls;

public sealed class RoundedImage : FrameworkElement
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source),
        typeof(ImageSource),
        typeof(RoundedImage),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius),
        typeof(double),
        typeof(RoundedImage),
        new FrameworkPropertyMetadata(
            8d,
            FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderBrushProperty = DependencyProperty.Register(
        nameof(BorderBrush),
        typeof(Brush),
        typeof(RoundedImage),
        new FrameworkPropertyMetadata(
            Brushes.White,
            FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderThicknessProperty = DependencyProperty.Register(
        nameof(BorderThickness),
        typeof(double),
        typeof(RoundedImage),
        new FrameworkPropertyMetadata(
            1d,
            FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty OuterBorderBrushProperty = DependencyProperty.Register(
        nameof(OuterBorderBrush),
        typeof(Brush),
        typeof(RoundedImage),
        new FrameworkPropertyMetadata(
            new SolidColorBrush(Color.FromArgb(160, 50, 61, 67)),
            FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BackgroundProperty = DependencyProperty.Register(
        nameof(Background),
        typeof(Brush),
        typeof(RoundedImage),
        new FrameworkPropertyMetadata(
            Brushes.Transparent,
            FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HighlightBrushProperty = DependencyProperty.Register(
        nameof(HighlightBrush),
        typeof(Brush),
        typeof(RoundedImage),
        new FrameworkPropertyMetadata(
            Brushes.Transparent,
            FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StretchProperty = DependencyProperty.Register(
        nameof(Stretch),
        typeof(Stretch),
        typeof(RoundedImage),
        new FrameworkPropertyMetadata(
            Stretch.UniformToFill,
            FrameworkPropertyMetadataOptions.AffectsRender));

    public RoundedImage()
    {
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
    }

    public ImageSource? Source
    {
        get => (ImageSource?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public double CornerRadius
    {
        get => (double)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public Brush BorderBrush
    {
        get => (Brush)GetValue(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public double BorderThickness
    {
        get => (double)GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public Brush OuterBorderBrush
    {
        get => (Brush)GetValue(OuterBorderBrushProperty);
        set => SetValue(OuterBorderBrushProperty, value);
    }

    public Brush Background
    {
        get => (Brush)GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public Brush HighlightBrush
    {
        get => (Brush)GetValue(HighlightBrushProperty);
        set => SetValue(HighlightBrushProperty, value);
    }

    public Stretch Stretch
    {
        get => (Stretch)GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var radius = Math.Min(CornerRadius, Math.Min(ActualWidth, ActualHeight) / 2);
        var outerThickness = Math.Max(2.8, BorderThickness * 2);
        var innerInset = Math.Max(2, outerThickness - 0.4);
        var innerBounds = new Rect(
            innerInset,
            innerInset,
            Math.Max(0, ActualWidth - (innerInset * 2)),
            Math.Max(0, ActualHeight - (innerInset * 2)));
        var innerRadius = Math.Max(1, radius - innerInset);
        var clip = new RectangleGeometry(innerBounds, innerRadius, innerRadius);

        if (Source is not null && Source.Width > 0 && Source.Height > 0)
        {
            var scale = Stretch == Stretch.Uniform
                ? Math.Min(innerBounds.Width / Source.Width, innerBounds.Height / Source.Height)
                : Math.Max(innerBounds.Width / Source.Width, innerBounds.Height / Source.Height);
            var width = Source.Width * scale;
            var height = Source.Height * scale;
            var destination = new Rect(
                innerBounds.X + ((innerBounds.Width - width) / 2),
                innerBounds.Y + ((innerBounds.Height - height) / 2),
                width,
                height);

            drawingContext.PushClip(clip);
            drawingContext.DrawRoundedRectangle(
                Background,
                null,
                innerBounds,
                innerRadius,
                innerRadius);
            drawingContext.DrawImage(Source, destination);
            drawingContext.Pop();
        }
        else
        {
            drawingContext.DrawRoundedRectangle(
                Background,
                null,
                innerBounds,
                innerRadius,
                innerRadius);
        }

        if (BorderThickness <= 0 || BorderBrush is null)
        {
            return;
        }

        var outerInset = outerThickness / 2;
        var outerBounds = new Rect(
            outerInset,
            outerInset,
            Math.Max(0, ActualWidth - outerThickness),
            Math.Max(0, ActualHeight - outerThickness));
        drawingContext.DrawRoundedRectangle(
            null,
            new Pen(OuterBorderBrush, outerThickness),
            outerBounds,
            Math.Max(1, radius - outerInset),
            Math.Max(1, radius - outerInset));

        var innerPenInset = outerThickness + (BorderThickness / 2);
        var highlightBounds = new Rect(
            innerPenInset,
            innerPenInset,
            Math.Max(0, ActualWidth - (innerPenInset * 2)),
            Math.Max(0, ActualHeight - (innerPenInset * 2)));
        drawingContext.DrawRoundedRectangle(
            null,
            new Pen(BorderBrush, BorderThickness),
            highlightBounds,
            Math.Max(1, radius - innerPenInset),
            Math.Max(1, radius - innerPenInset));

        if (HighlightBrush is not null)
        {
            var specularInset = outerThickness + BorderThickness + 0.4;
            var specularBounds = new Rect(
                specularInset,
                specularInset,
                Math.Max(0, ActualWidth - (specularInset * 2)),
                Math.Max(0, ActualHeight - (specularInset * 2)));
            drawingContext.DrawRoundedRectangle(
                null,
                new Pen(HighlightBrush, 0.8),
                specularBounds,
                Math.Max(1, radius - specularInset),
                Math.Max(1, radius - specularInset));
        }
    }
}
