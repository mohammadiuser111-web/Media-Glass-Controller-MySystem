using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MediaGlass.Services;

internal sealed record ThumbnailTheme(ImageSource? Artwork, Color Start, Color End, Color Accent);

internal static class ThumbnailThemeService
{
    private static readonly Color DefaultStart = Color.FromArgb(220, 20, 27, 42);
    private static readonly Color DefaultEnd = Color.FromArgb(230, 8, 11, 19);
    private static readonly Color DefaultAccent = Color.FromRgb(96, 165, 250);

    public static ThumbnailTheme Create(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0)
        {
            return new ThumbnailTheme(null, DefaultStart, DefaultEnd, DefaultAccent);
        }

        try
        {
            BitmapImage image = CreateBitmap(bytes);
            Color accent = ExtractAccent(image);
            Color start = Shade(accent, 0.46, alpha: 215);
            Color end = Shade(accent, 0.16, alpha: 232);
            return new ThumbnailTheme(image, start, end, accent);
        }
        catch (NotSupportedException)
        {
            return new ThumbnailTheme(null, DefaultStart, DefaultEnd, DefaultAccent);
        }
        catch (IOException)
        {
            return new ThumbnailTheme(null, DefaultStart, DefaultEnd, DefaultAccent);
        }
    }

    private static BitmapImage CreateBitmap(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static Color ExtractAccent(BitmapSource source)
    {
        const int sampleSize = 32;
        var scaled = new TransformedBitmap(source, new ScaleTransform(
            sampleSize / Math.Max(1.0, source.PixelWidth),
            sampleSize / Math.Max(1.0, source.PixelHeight)));
        var formatted = new FormatConvertedBitmap(scaled, PixelFormats.Bgra32, null, 0);
        int stride = formatted.PixelWidth * 4;
        byte[] pixels = new byte[stride * formatted.PixelHeight];
        formatted.CopyPixels(pixels, stride, 0);

        double red = 0;
        double green = 0;
        double blue = 0;
        double weightTotal = 0;

        for (int offset = 0; offset < pixels.Length; offset += 4)
        {
            double b = pixels[offset];
            double g = pixels[offset + 1];
            double r = pixels[offset + 2];
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double saturation = max <= 1 ? 0 : (max - min) / max;
            double brightness = max / 255.0;
            double weight = 0.2 + (saturation * 1.8) + (brightness * 0.35);

            red += r * weight;
            green += g * weight;
            blue += b * weight;
            weightTotal += weight;
        }

        if (weightTotal <= 0)
        {
            return DefaultAccent;
        }

        return Color.FromRgb(
            Clamp(red / weightTotal),
            Clamp(green / weightTotal),
            Clamp(blue / weightTotal));
    }

    private static Color Shade(Color color, double factor, byte alpha) => Color.FromArgb(
        alpha,
        Clamp((color.R * factor) + 8),
        Clamp((color.G * factor) + 10),
        Clamp((color.B * factor) + 14));

    private static byte Clamp(double value) => (byte)Math.Clamp((int)Math.Round(value), 0, 255);
}
