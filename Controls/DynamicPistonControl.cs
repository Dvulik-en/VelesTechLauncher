using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace VelesTech.Controls;

public class DynamicPistonControl : Control
{
    private double _time = 0;
    private double _pistonY = 0;

    public void Update()
    {
        _time += 0.06;
        _pistonY = Math.Sin(_time) * 22;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var metalPen = new Pen(SolidColorBrush.Parse("#3D3D42"), 2);
        var orangeBrush = SolidColorBrush.Parse("#E5731C");
        var darkPistonBrush = SolidColorBrush.Parse("#25252A");

        context.DrawRectangle(Brushes.Transparent,
            new Pen(SolidColorBrush.Parse("#222226"), 3),
            new Rect(center.X - 25, center.Y - 50, 50, 100));

        var currentTop = center.Y - 15 + _pistonY;
        context.DrawLine(new Pen(SolidColorBrush.Parse("#7E7E84"), 6),
            new Point(center.X, currentTop + 20),
            new Point(center.X, center.Y + 48));

        context.DrawRectangle(darkPistonBrush, metalPen,
            new Rect(center.X - 22, currentTop, 44, 24));
        context.DrawRectangle(orangeBrush, null,
            new Rect(center.X - 22, currentTop + 8, 44, 4));
    }
}
