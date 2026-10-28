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
        var geometry = Geometry.Parse("M 5,30 C 5,17 15,8 27,8 C 37,8 44,17 51,27 L 59,38 C 64,45 70,49 77,49 C 88,49 95,40 95,29 C 95,21 91,15 84,12 C 91,18 88,26 82,31 C 76,36 69,35 63,28 L 51,12 C 44,4 36,1 27,3 C 12,5 3,17 3,30 C 3,42 9,51 20,55 C 13,50 12,41 16,34 C 20,27 28,25 34,31 L 47,47 C 55,57 64,63 76,61 C 89,59 98,47 98,34 C 98,26 94,18 87,14 C 90,19 90,27 85,32 C 80,37 73,37 67,30 L 55,14 C 47,4 38,0 27,3 C 14,6 5,17 5,30 Z");
        var gradient = new LinearGradientBrush(Color.FromRgb(124,58,237), Color.FromRgb(167,139,250), 35);
        var drawing = new DrawingGroup();
        drawing.Children.Add(new GeometryDrawing(gradient, null, geometry));
        var highlight = Geometry.Parse("M 14,31 C 14,20 21,13 29,13 C 36,13 42,21 49,30 L 59,43 C 65,50 71,54 78,54 C 84,54 89,50 92,44 C 89,55 81,60 73,59 C 64,59 57,53 50,44 L 39,30 C 34,24 29,22 24,25 C 19,27 17,32 18,37 Z");
        drawing.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromArgb(90,255,255,255)), null, highlight));
        return new DrawingImage(drawing);
    }

    internal static BitmapSource Icon(int size = 64)
    {
        var background = new GeometryDrawing(
            new LinearGradientBrush(Color.FromRgb(124,58,237), Color.FromRgb(91,33,182), 45),
            null, new RectangleGeometry(new Rect(0,0,100,100), 23, 23));
        var mark = new GeometryDrawing(new LinearGradientBrush(Color.FromRgb(255,255,255), Color.FromRgb(221,214,254), 35), null,
            Geometry.Parse("M 8,48 C 8,31 20,19 34,19 C 45,19 53,31 61,42 L 69,53 C 74,60 80,64 87,64 C 95,64 101,57 101,47 C 101,39 97,33 91,30 C 97,36 93,43 87,47 C 81,51 75,49 69,42 L 59,28 C 51,17 42,12 32,14 C 17,16 8,30 8,48 Z"));
        var group = new DrawingGroup(); group.Children.Add(background); group.Children.Add(mark);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen()) dc.DrawDrawing(group);
        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual); bitmap.Freeze(); return bitmap;
    }
}
