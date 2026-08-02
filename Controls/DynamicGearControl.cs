using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace VelesTech.Controls;

public class DynamicGearControl : Control
{
    private double _angle = 0;

    public void Update()
    {
        _angle += 1.2;
        if (_angle >= 360) _angle = 0;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var strokePen = new Pen(SolidColorBrush.Parse("#232328"), 2);

        var gearGradient = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new GradientStop(Color.Parse("#E5731C"), 0.0),
                new GradientStop(Color.Parse("#B5530C"), 1.0)
            }
        };

        using (context.PushTransform(
            Matrix.CreateTranslation(center.X, center.Y) *
            Matrix.CreateRotation(_angle * Math.PI / 180) *
            Matrix.CreateTranslation(-center.X, -center.Y)))
        {
            for (int i = 0; i < 8; i++)
            {
                using (context.PushTransform(
                    Matrix.CreateTranslation(center.X, center.Y) *
                    Matrix.CreateRotation(i * 45 * Math.PI / 180) *
                    Matrix.CreateTranslation(-center.X, -center.Y)))
                {
                    context.DrawRectangle(gearGradient, strokePen,
                        new Rect(center.X - 10, center.Y - 45, 20, 20));
                }
            }

            context.DrawGeometry(gearGradient, strokePen,
                new EllipseGeometry(new Rect(center.X - 35, center.Y - 35, 70, 70)));
            context.DrawGeometry(SolidColorBrush.Parse("#121214"), strokePen,
                new EllipseGeometry(new Rect(center.X - 12, center.Y - 12, 24, 24)));
        }
    }
}
