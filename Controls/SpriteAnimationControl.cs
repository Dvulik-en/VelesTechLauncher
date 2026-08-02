using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using System;

namespace VelesTech.Controls;

/// <summary>
/// Проигрывает спрайт-лист (Avalonia): PNG с кадрами по горизонтали.
/// speedMultiplier — умножает скорость воспроизведения.
/// </summary>
public class SpriteAnimationControl : Control
{
    private Bitmap? _spriteSheet;
    private readonly int _frameCount;
    private double _currentFrame = 0;
    private DispatcherTimer? _timer;
    private readonly string _assetPath;
    private readonly double _speedMultiplier;
    private readonly int _fps;

    public SpriteAnimationControl(string assetPath, int frameCount, int fps, double speedMultiplier = 1.0)
    {
        _assetPath = assetPath;
        _frameCount = frameCount;
        _fps = fps;
        _speedMultiplier = speedMultiplier;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.0 / 60.0) };
        _timer.Tick += (_, _) =>
        {
            double frameStep = (_fps / 60.0) * _speedMultiplier;
            _currentFrame = (_currentFrame + frameStep) % _frameCount;
            InvalidateVisual();
        };
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        try
        {
            // Пытаемся загрузить как ресурс Avalonia, если не выходит — как файл
            try
            {
                var uri = new Uri($"avares://VelesTech/{_assetPath.Replace('\\', '/')}");
                using var stream = AssetLoader.Open(uri);
                _spriteSheet = new Bitmap(stream);
            }
            catch
            {
                _spriteSheet = new Bitmap(_assetPath);
            }
            _timer?.Start();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SpriteAnimation] Ошибка загрузки {_assetPath}: {ex.Message}");
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer?.Stop();
        _spriteSheet?.Dispose();
        _spriteSheet = null;
    }

    public override void Render(DrawingContext context)
    {
        if (_spriteSheet == null || _frameCount <= 0) return;

        double srcFrameWidth = _spriteSheet.Size.Width / _frameCount;
        double srcFrameHeight = _spriteSheet.Size.Height;

        int frameIndex = (int)Math.Floor(_currentFrame);
        var sourceRect = new Rect(frameIndex * srcFrameWidth, 0, srcFrameWidth, srcFrameHeight);
        var destRect = new Rect(0, 0, Bounds.Width, Bounds.Height);
        context.DrawImage(_spriteSheet, sourceRect, destRect);
    }
}
