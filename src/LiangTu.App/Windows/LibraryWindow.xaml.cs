using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LiangTu.App.Infrastructure;
using LiangTu.App.Services;
using LiangTu.App.ViewModels;
using LiangTu.Core;

namespace LiangTu.App.Windows;

public partial class LibraryWindow : Window
{
    private readonly ImageLibraryService _library;
    private readonly ClipboardService _clipboard;
    private bool _loading;

    public LibraryWindow(ImageLibraryService library, ClipboardService clipboard)
    {
        _library = library;
        _clipboard = clipboard;
        InitializeComponent();
        Loaded += async (_, _) => await RefreshAsync();
        _library.AssetUpdated += Library_AssetUpdated;
        Closed += (_, _) => _library.AssetUpdated -= Library_AssetUpdated;
    }

    public event EventHandler<ImageAsset>? PreviewRequested;

    public event EventHandler<ImageAsset>? EditRequested;

    public event EventHandler<ImageAsset>? PinRequested;

    public event EventHandler<string>? ToastRequested;

    private async Task RefreshAsync()
    {
        if (_loading)
        {
            return;
        }

        _loading = true;
        try
        {
            var assets = await _library.GetAssetsAsync();
            var viewModels = new List<LibraryImageViewModel>(assets.Count);
            foreach (var asset in assets)
            {
                var thumbnail = await _library.GetThumbnailAsync(asset);
                viewModels.Add(new LibraryImageViewModel(asset, thumbnail));
            }

            AssetList.ItemsSource = viewModels;
            CountText.Text = viewModels.Count.ToString();
            EmptyText.Visibility = viewModels.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateSelectionState();
        }
        catch (Exception ex)
        {
            ToastRequested?.Invoke(this, $"读取图片库失败：{ex.Message}");
        }
        finally
        {
            _loading = false;
        }
    }

    private void AssetList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (AssetList.SelectedItem is LibraryImageViewModel item)
        {
            PreviewRequested?.Invoke(this, item.Asset);
        }
    }

    private void AssetList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateSelectionState();
    }

    private void AssetList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
        {
            DeleteSelected_Click(sender, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private async void DeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = AssetList.SelectedItems
            .OfType<LibraryImageViewModel>()
            .Select(item => item.Asset)
            .ToList();
        if (selected.Count == 0)
        {
            return;
        }

        var confirmation = System.Windows.MessageBox.Show(
            this,
            $"删除选中的 {selected.Count} 张图片？此操作不可恢复。",
            "删除图片",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var deletedCount = await _library.DeleteAssetsAsync(selected);
            ToastRequested?.Invoke(this, $"已删除 {deletedCount} 张图片");
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            ToastRequested?.Invoke(this, $"删除失败：{ex.Message}");
        }
    }

    private async void Copy_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not LibraryImageViewModel item)
        {
            return;
        }

        try
        {
            var image = await _library.LoadFullImageAsync(item.Asset);
            _clipboard.CopyImage(image);
            ToastRequested?.Invoke(this, "已复制原图");
        }
        catch (Exception ex)
        {
            ToastRequested?.Invoke(this, $"复制失败：{ex.Message}");
        }
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is LibraryImageViewModel item)
        {
            PreviewRequested?.Invoke(this, item.Asset);
        }
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is LibraryImageViewModel item)
        {
            EditRequested?.Invoke(this, item.Asset);
        }
    }

    private void Pin_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is LibraryImageViewModel item)
        {
            PinRequested?.Invoke(this, item.Asset);
        }
    }

    private async void Promote_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is LibraryImageViewModel item)
        {
            await _library.PromoteToRopeAsync(item.Asset);
            ToastRequested?.Invoke(this, "已挂到晾衣绳");
        }
    }

    private void Library_AssetUpdated(object? sender, ImageAsset e)
    {
        Dispatcher.InvokeAsync(async () => await RefreshAsync());
    }

    private void UpdateSelectionState()
    {
        var selectedCount = AssetList.SelectedItems.Count;
        SelectionText.Text = selectedCount == 0 ? string.Empty : $"已选 {selectedCount}";
        DeleteSelectedButton.IsEnabled = selectedCount > 0;
        DeleteSelectedButton.Content = selectedCount > 1 ? $"删除 {selectedCount}" : "删除";
    }
}
