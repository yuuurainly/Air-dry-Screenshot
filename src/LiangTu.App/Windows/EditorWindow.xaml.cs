using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using LiangTu.App.Infrastructure;
using LiangTu.App.Services;
using LiangTu.Core;

namespace LiangTu.App.Windows;

public partial class EditorWindow : Window
{
    private enum AnnotationTool
    {
        Pen,
        Arrow,
        Rectangle,
        Text,
        Mosaic,
    }

    private readonly ImageLibraryService _library;
    private readonly ImageAsset _asset;
    private readonly List<byte[]> _undo = [];
    private readonly List<byte[]> _redo = [];
    private readonly List<Point> _penPoints = [];
    private BitmapSource _currentImage;
    private AnnotationTool _tool = AnnotationTool.Pen;
    private Color _color = Color.FromRgb(224, 90, 90);
    private Point? _startPoint;
    private Shape? _previewShape;
    private double _displayScale = 1;
    private bool _isDrawing;

    public EditorWindow(ImageLibraryService library, ImageAsset asset, BitmapSource image)
    {
        _library = library;
        _asset = asset;
        _currentImage = image;
        InitializeComponent();
        BaseImage.Source = image;
        Loaded += EditorWindow_Loaded;
        KeyDown += EditorWindow_KeyDown;
        UpdateUndoRedoState();
    }

    private void EditorWindow_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateImageLayout();
        StatusText.Text = $"{_asset.PixelWidth} × {_asset.PixelHeight}";
    }

    private void ImageArea_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateImageLayout();
    }

    private void UpdateImageLayout()
    {
        if (ImageArea.ActualWidth <= 0 || ImageArea.ActualHeight <= 0)
        {
            return;
        }

        var availableWidth = Math.Max(80, ImageArea.ActualWidth - 36);
        var availableHeight = Math.Max(80, ImageArea.ActualHeight - 36);
        _displayScale = Math.Min(
            availableWidth / _currentImage.PixelWidth,
            availableHeight / _currentImage.PixelHeight);
        _displayScale = Math.Min(_displayScale, 1d);

        CanvasHost.Width = _currentImage.PixelWidth * _displayScale;
        CanvasHost.Height = _currentImage.PixelHeight * _displayScale;
        BaseImage.Width = CanvasHost.Width;
        BaseImage.Height = CanvasHost.Height;
        OverlayCanvas.Width = CanvasHost.Width;
        OverlayCanvas.Height = CanvasHost.Height;
    }

    private void Tool_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: string tag }
            || !Enum.TryParse(tag, out AnnotationTool tool))
        {
            return;
        }

        _tool = tool;
    }

    private void Color_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag })
        {
            _color = (Color)ColorConverter.ConvertFromString(tag);
        }
    }

    private void OverlayCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        _isDrawing = true;
        _startPoint = e.GetPosition(OverlayCanvas);
        _penPoints.Clear();
        _penPoints.Add(_startPoint.Value);
        OverlayCanvas.Children.Clear();
        OverlayCanvas.CaptureMouse();

        if (_tool == AnnotationTool.Pen)
        {
            _previewShape = new Polyline
            {
                Stroke = new SolidColorBrush(_color),
                StrokeThickness = ToolSizeSlider.Value * _displayScale,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
            };
            OverlayCanvas.Children.Add(_previewShape);
        }
        else if (_tool is AnnotationTool.Arrow or AnnotationTool.Rectangle)
        {
            _previewShape = _tool == AnnotationTool.Arrow
                ? new Line
                {
                    Stroke = new SolidColorBrush(_color),
                    StrokeThickness = ToolSizeSlider.Value * _displayScale,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                }
                : new Rectangle
                {
                    Stroke = new SolidColorBrush(_color),
                    StrokeThickness = ToolSizeSlider.Value * _displayScale,
                };
            OverlayCanvas.Children.Add(_previewShape);
        }
        else if (_tool == AnnotationTool.Mosaic)
        {
            _previewShape = new Rectangle
            {
                Fill = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)),
                Stroke = Brushes.White,
                StrokeThickness = 1,
            };
            OverlayCanvas.Children.Add(_previewShape);
        }

        e.Handled = true;
    }

    private void OverlayCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDrawing || _startPoint is null)
        {
            return;
        }

        var current = e.GetPosition(OverlayCanvas);
        if (_tool == AnnotationTool.Pen && _previewShape is Polyline polyline)
        {
            _penPoints.Add(current);
            polyline.Points = new PointCollection(_penPoints);
            return;
        }

        UpdatePreviewShape(_startPoint.Value, current);
    }

    private void OverlayCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDrawing || _startPoint is null)
        {
            return;
        }

        _isDrawing = false;
        OverlayCanvas.ReleaseMouseCapture();
        var end = e.GetPosition(OverlayCanvas);
        var start = _startPoint.Value;
        _startPoint = null;
        OverlayCanvas.Children.Clear();
        _previewShape = null;

        if (_tool == AnnotationTool.Text)
        {
            AddText(start);
            return;
        }

        if ((end - start).Length < 2 && _tool != AnnotationTool.Pen)
        {
            return;
        }

        CommitOperation(start, end);
    }

    private void UpdatePreviewShape(Point start, Point current)
    {
        var left = Math.Min(start.X, current.X);
        var top = Math.Min(start.Y, current.Y);
        var width = Math.Abs(current.X - start.X);
        var height = Math.Abs(current.Y - start.Y);

        if (_previewShape is Line line)
        {
            line.X1 = start.X;
            line.Y1 = start.Y;
            line.X2 = current.X;
            line.Y2 = current.Y;
        }
        else if (_previewShape is Rectangle rectangle)
        {
            Canvas.SetLeft(rectangle, left);
            Canvas.SetTop(rectangle, top);
            rectangle.Width = width;
            rectangle.Height = height;
        }
    }

    private void AddText(Point position)
    {
        var dialog = new TextInputWindow { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            PushUndo();
            _currentImage = RenderOperation(position, position, dialog.Text);
            RefreshImage();
            _redo.Clear();
            UpdateUndoRedoState();
        }
    }

    private void CommitOperation(Point start, Point end)
    {
        PushUndo();
        _currentImage = RenderOperation(start, end, null);
        RefreshImage();
        _redo.Clear();
        UpdateUndoRedoState();
    }

    private BitmapSource RenderOperation(Point startDisplay, Point endDisplay, string? text)
    {
        var start = ToImagePoint(startDisplay);
        var end = ToImagePoint(endDisplay);
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawImage(_currentImage, new Rect(0, 0, _currentImage.PixelWidth, _currentImage.PixelHeight));
            var brush = new SolidColorBrush(_color);
            brush.Freeze();
            var strokeWidth = ToolSizeSlider.Value;

            switch (_tool)
            {
                case AnnotationTool.Pen:
                    DrawPen(drawing, brush, strokeWidth);
                    break;
                case AnnotationTool.Arrow:
                    DrawArrow(drawing, brush, strokeWidth, start, end);
                    break;
                case AnnotationTool.Rectangle:
                    DrawRectangle(drawing, brush, strokeWidth, start, end);
                    break;
                case AnnotationTool.Text:
                    DrawText(drawing, brush, start, text ?? string.Empty);
                    break;
                case AnnotationTool.Mosaic:
                    DrawMosaic(drawing, start, end);
                    break;
            }
        }

        var result = new RenderTargetBitmap(
            _currentImage.PixelWidth,
            _currentImage.PixelHeight,
            96,
            96,
            PixelFormats.Pbgra32);
        result.Render(visual);
        result.Freeze();
        return result;
    }

    private void DrawPen(DrawingContext drawing, Brush brush, double strokeWidth)
    {
        if (_penPoints.Count < 2)
        {
            return;
        }

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(ToImagePoint(_penPoints[0]), isFilled: false, isClosed: false);
            context.PolyLineTo(_penPoints.Skip(1).Select(ToImagePoint).ToList(), isStroked: true, isSmoothJoin: true);
        }

        geometry.Freeze();
        drawing.DrawGeometry(null, new Pen(brush, strokeWidth), geometry);
    }

    private static void DrawArrow(
        DrawingContext drawing,
        Brush brush,
        double strokeWidth,
        Point start,
        Point end)
    {
        var pen = new Pen(brush, strokeWidth)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
        };
        drawing.DrawLine(pen, start, end);

        var angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
        var headLength = Math.Max(14, strokeWidth * 3.5);
        var spread = Math.PI / 7;
        var p1 = new Point(
            end.X - headLength * Math.Cos(angle - spread),
            end.Y - headLength * Math.Sin(angle - spread));
        var p2 = new Point(
            end.X - headLength * Math.Cos(angle + spread),
            end.Y - headLength * Math.Sin(angle + spread));

        var head = new StreamGeometry();
        using (var context = head.Open())
        {
            context.BeginFigure(end, isFilled: true, isClosed: true);
            context.LineTo(p1, isStroked: true, isSmoothJoin: true);
            context.LineTo(p2, isStroked: true, isSmoothJoin: true);
        }

        head.Freeze();
        drawing.DrawGeometry(brush, null, head);
    }

    private static void DrawRectangle(
        DrawingContext drawing,
        Brush brush,
        double strokeWidth,
        Point start,
        Point end)
    {
        var rectangle = new Rect(start, end);
        drawing.DrawRectangle(null, new Pen(brush, strokeWidth), rectangle);
    }

    private void DrawText(DrawingContext drawing, Brush brush, Point position, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            System.Windows.FlowDirection.LeftToRight,
            new Typeface("Microsoft YaHei UI"),
            Math.Max(18, ToolSizeSlider.Value * 4),
            brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        drawing.DrawText(formatted, position);
    }

    private void DrawMosaic(DrawingContext drawing, Point start, Point end)
    {
        var rectangle = ToPixelRect(start, end);
        if (rectangle.Width < 2 || rectangle.Height < 2)
        {
            return;
        }

        var converted = _currentImage.Format == PixelFormats.Bgra32
            ? _currentImage
            : new FormatConvertedBitmap(_currentImage, PixelFormats.Bgra32, null, 0);
        var stride = rectangle.Width * 4;
        var pixels = new byte[stride * rectangle.Height];
        converted.CopyPixels(rectangle, pixels, stride, 0);
        var blockSize = Math.Max(8, (int)Math.Round(ToolSizeSlider.Value * 2));

        for (var blockY = 0; blockY < rectangle.Height; blockY += blockSize)
        {
            for (var blockX = 0; blockX < rectangle.Width; blockX += blockSize)
            {
                var width = Math.Min(blockSize, rectangle.Width - blockX);
                var height = Math.Min(blockSize, rectangle.Height - blockY);
                long blue = 0;
                long green = 0;
                long red = 0;
                var count = 0;
                for (var y = blockY; y < blockY + height; y++)
                {
                    for (var x = blockX; x < blockX + width; x++)
                    {
                        var index = (y * stride) + (x * 4);
                        blue += pixels[index];
                        green += pixels[index + 1];
                        red += pixels[index + 2];
                        count++;
                    }
                }

                var brush = new SolidColorBrush(Color.FromRgb(
                    (byte)(red / count),
                    (byte)(green / count),
                    (byte)(blue / count)));
                drawing.DrawRectangle(
                    brush,
                    null,
                    new Rect(
                        rectangle.X + blockX,
                        rectangle.Y + blockY,
                        width,
                        height));
            }
        }
    }

    private Int32Rect ToPixelRect(Point start, Point end)
    {
        var left = Math.Clamp((int)Math.Round(Math.Min(start.X, end.X)), 0, _currentImage.PixelWidth - 1);
        var top = Math.Clamp((int)Math.Round(Math.Min(start.Y, end.Y)), 0, _currentImage.PixelHeight - 1);
        var right = Math.Clamp((int)Math.Round(Math.Max(start.X, end.X)), left + 1, _currentImage.PixelWidth);
        var bottom = Math.Clamp((int)Math.Round(Math.Max(start.Y, end.Y)), top + 1, _currentImage.PixelHeight);
        return new Int32Rect(left, top, right - left, bottom - top);
    }

    private Point ToImagePoint(Point displayPoint)
    {
        return new Point(
            Math.Clamp(displayPoint.X / _displayScale, 0, _currentImage.PixelWidth),
            Math.Clamp(displayPoint.Y / _displayScale, 0, _currentImage.PixelHeight));
    }

    private void RefreshImage()
    {
        BaseImage.Source = _currentImage;
        UpdateImageLayout();
        StatusText.Text = $"{_currentImage.PixelWidth} × {_currentImage.PixelHeight}";
    }

    private void PushUndo()
    {
        _undo.Add(ImageCodec.EncodePng(_currentImage));
        if (_undo.Count > 12)
        {
            _undo.RemoveAt(0);
        }
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (_undo.Count == 0)
        {
            return;
        }

        _redo.Add(ImageCodec.EncodePng(_currentImage));
        _currentImage = ImageCodec.LoadBytes(_undo[^1]);
        _undo.RemoveAt(_undo.Count - 1);
        RefreshImage();
        UpdateUndoRedoState();
    }

    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        if (_redo.Count == 0)
        {
            return;
        }

        _undo.Add(ImageCodec.EncodePng(_currentImage));
        _currentImage = ImageCodec.LoadBytes(_redo[^1]);
        _redo.RemoveAt(_redo.Count - 1);
        RefreshImage();
        UpdateUndoRedoState();
    }

    private void UpdateUndoRedoState()
    {
        StatusText.Text = $"{_currentImage.PixelWidth} × {_currentImage.PixelHeight} · 撤销 {_undo.Count} · 重做 {_redo.Count}";
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            StatusText.Text = "正在保存…";
            IsEnabled = false;
            var bytes = ImageCodec.EncodePng(_currentImage);
            await _library.SaveEditedImageAsync(_asset, bytes);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            IsEnabled = true;
            StatusText.Text = $"保存失败：{ex.Message}";
            System.Windows.MessageBox.Show(
                this,
                ex.Message,
                "保存失败",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void EditorWindow_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
        }
        else if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            Undo_Click(this, new RoutedEventArgs());
        }
        else if (e.Key == Key.Y && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            Redo_Click(this, new RoutedEventArgs());
        }
    }

    private void Toolbar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
