using System.Windows.Input;
using LiangTu.App.Services;

namespace LiangTu.App.Infrastructure;

public enum GlobalHotkey
{
    RegionScreenshot = 1,
    WindowScreenshot = 2,
    PinClipboard = 3,
}

public sealed class HotkeyService : IDisposable
{
    private readonly Dictionary<int, GlobalHotkey> _registered = [];
    private nint _window;

    public event EventHandler<GlobalHotkey>? Pressed;

    public void Attach(nint window)
    {
        Detach();
        _window = window;
    }

    public IReadOnlyList<GlobalHotkey> Register(AppSettings settings)
    {
        var failed = new List<GlobalHotkey>();
        if (_window == 0)
        {
            return failed;
        }

        UnregisterAll();
        var gestures = new Dictionary<GlobalHotkey, string>
        {
            [GlobalHotkey.RegionScreenshot] = settings.RegionHotkey,
            [GlobalHotkey.WindowScreenshot] = settings.WindowHotkey,
            [GlobalHotkey.PinClipboard] = settings.PinHotkey,
        };

        foreach (var (action, gesture) in gestures)
        {
            var parsed = ParseGesture(gesture);
            var id = (int)action;
            if (NativeMethods.RegisterHotKey(
                    _window,
                    id,
                    parsed.Modifiers | NativeMethods.ModNoRepeat,
                    parsed.VirtualKey))
            {
                _registered[id] = action;
            }
            else
            {
                failed.Add(action);
            }
        }

        return failed;
    }

    public bool ProcessWindowMessage(int message, nint wParam)
    {
        if (message != NativeMethods.WmHotkey)
        {
            return false;
        }

        if (_registered.TryGetValue(wParam.ToInt32(), out var action))
        {
            Pressed?.Invoke(this, action);
        }

        return true;
    }

    public static string? Validate(AppSettings settings)
    {
        var gestures = new[]
        {
            settings.RegionHotkey,
            settings.WindowHotkey,
            settings.PinHotkey,
        };

        var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var gesture in gestures)
        {
            var parsed = TryParseGesture(gesture);
            if (parsed is null)
            {
                return $"无法识别快捷键“{gesture}”。";
            }

            if (!normalized.Add(parsed.Value.Normalized))
            {
                return "快捷键不能重复。";
            }
        }

        return null;
    }

    public void Dispose()
    {
        UnregisterAll();
        _window = 0;
    }

    private static (uint Modifiers, uint VirtualKey) ParseGesture(string gesture)
    {
        var parsed = TryParseGesture(gesture);
        if (parsed is null)
        {
            throw new FormatException($"无法识别快捷键“{gesture}”。");
        }

        return (parsed.Value.Modifiers, parsed.Value.VirtualKey);
    }

    private static (uint Modifiers, uint VirtualKey, string Normalized)? TryParseGesture(string gesture)
    {
        var parts = gesture.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        uint modifiers = 0;
        for (var index = 0; index < parts.Length - 1; index++)
        {
            modifiers |= parts[index].ToUpperInvariant() switch
            {
                "ALT" => NativeMethods.ModAlt,
                "CTRL" or "CONTROL" => NativeMethods.ModControl,
                "SHIFT" => NativeMethods.ModShift,
                "WIN" => NativeMethods.ModWin,
                _ => uint.MaxValue,
            };
            if (modifiers == uint.MaxValue)
            {
                return null;
            }
        }

        if (!Enum.TryParse<Key>(parts[^1], ignoreCase: true, out var key)
            || key is Key.None or Key.LeftAlt or Key.RightAlt or Key.LeftCtrl or Key.RightCtrl)
        {
            return null;
        }

        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey == 0)
        {
            return null;
        }

        return (modifiers, virtualKey, $"{modifiers}:{virtualKey}");
    }

    private void UnregisterAll()
    {
        if (_window == 0)
        {
            return;
        }

        foreach (var id in _registered.Keys)
        {
            NativeMethods.UnregisterHotKey(_window, id);
        }

        _registered.Clear();
    }

    private void Detach()
    {
        UnregisterAll();
        _window = 0;
    }
}
