using System.Drawing;
using System.Drawing.Imaging;

namespace GestioneFoto.App.Services;

public sealed class ThumbnailGenerator
{
    private const int MaxDimension = 256;

    public byte[]? TryGenerate(string filePath)
    {
        try
        {
            using var image = Image.FromFile(filePath);
            return GenerateThumbnail(image);
        }
        catch
        {
            return null;
        }
    }

    public byte[]? TryGenerateFromStream(Stream stream)
    {
        try
        {
            using var image = Image.FromStream(stream, useEmbeddedColorManagement: false);
            return GenerateThumbnail(image);
        }
        catch
        {
            return null;
        }
    }

    private static byte[] GenerateThumbnail(Image originalImage)
    {
        var (thumbWidth, thumbHeight) = CalculateDimensions(originalImage.Width, originalImage.Height);

        using var thumbnail = new Bitmap(thumbWidth, thumbHeight);
        using var g = Graphics.FromImage(thumbnail);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.DrawImage(originalImage, 0, 0, thumbWidth, thumbHeight);

        using var memStream = new MemoryStream();
        thumbnail.Save(memStream, ImageFormat.Jpeg);
        return memStream.ToArray();
    }

    private static (int width, int height) CalculateDimensions(int originalWidth, int originalHeight)
    {
        if (originalWidth <= MaxDimension && originalHeight <= MaxDimension)
        {
            return (originalWidth, originalHeight);
        }

        double scale = Math.Min((double)MaxDimension / originalWidth, (double)MaxDimension / originalHeight);
        return ((int)(originalWidth * scale), (int)(originalHeight * scale));
    }
}
