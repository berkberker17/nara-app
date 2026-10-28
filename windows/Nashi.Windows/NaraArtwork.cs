using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Nashi.Windows;

internal static class NaraArtwork
{
    // A soft, continuous ribbon-N mark drawn in vector form so it stays crisp
    // at every size and does not depend on the legacy Nashi bitmap assets.
    internal static ImageSource Mark()
    {
        var geometry = Geometry.Parse("M 8,30 C 8,17 18,9 29,9 C 38,9 43,16 49,24 L 57,35 C 61,40 65,43 70,43 C 77,43 82,38 82,30 C 82,22 77,17 70,17 C 65,17 61,20 57,25 L 49,35 C 43,43 38,50 29,50 C 18,50 8,42 8,30 Z");
        var drawing = new GeometryDrawing(new LinearGradientBrush(Color.FromRgb(124,58,237), Color.FromRgb(167,139,250), 0), null, geometry);
        return new DrawingImage(drawing);
    }

    internal static BitmapSource Icon(int size = 64)
    {
        var background = new GeometryDrawing(
            new LinearGradientBrush(Color.FromRgb(124,58,237), Color.FromRgb(91,33,182), 45),
            null, new RectangleGeometry(new Rect(0,0,100,100), 23, 23));
        var mark = new GeometryDrawing(Brushes.White, null,
            Geometry.Parse("M 16,51 C 16,34 28,24 40,24 C 49,24 56,33 63,42 L 70,52 C 74,57 78,60 83,60 C 90,60 94,55 94,48 C 94,41 90,36 83,36 C 78,36 74,40 70,45 L 63,55 C 56,64 49,74 40,74 C 28,74 16,65 16,51 Z"));
        var group = new DrawingGroup(); group.Children.Add(background); group.Children.Add(mark);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen()) dc.DrawDrawing(group);
        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual); bitmap.Freeze(); return bitmap;
    }
}
