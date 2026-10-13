using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace SoundDrought;

/// <summary>Renders Segoe Fluent / MDL2 icon glyphs into images for the taskbar thumbnail buttons.</summary>
public static class Glyphs
{
    public const string Previous = "\uE892";
    public const string Next = "\uE893";
    public const string Play = "\uE768";
    public const string Pause = "\uE769";
    public const string Heart = "\uEB51";
    public const string HeartFilled = "\uEB52";

    public static readonly Brush Accent = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0x55, 0x00)));

    private const double Size = 16;
    private static readonly Typeface Font = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    public static ImageSource Render(string glyph)
    {
        var text = new FormattedText(glyph, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            Font, Size, Brushes.White, 1.0);

        var group = new DrawingGroup();
        // Transparent frame keeps every glyph at the same size so icons don't jump around.
        group.Children.Add(new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new Rect(0, 0, Size, Size))));
        group.Children.Add(new GeometryDrawing(Brushes.White, null, text.BuildGeometry(new Point(0, 0))));

        var image = new DrawingImage(group);
        image.Freeze();
        return image;
    }

    private static Brush Freeze(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
