using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace GestioneFoto.App.Services;

public readonly record struct ExifDates(
    DateTime? Original,
    DateTime? Digitized,
    DateTime? Generic)
{
    public DateTime? Preferred => Original ?? Digitized ?? Generic;
}

public sealed class ExifDateReader
{
    public DateTime? TryRead(string filePath)
    {
        return ReadDates(filePath).Preferred;
    }

    public ExifDates ReadDates(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            var directories = ImageMetadataReader.ReadMetadata(stream);

            var subIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();

            return new ExifDates(
                TryGetDate(subIfd, ExifDirectoryBase.TagDateTimeOriginal),
                TryGetDate(subIfd, ExifDirectoryBase.TagDateTimeDigitized),
                TryGetDate(ifd0, ExifDirectoryBase.TagDateTime));
        }
        catch
        {
            return default;
        }
    }

    private static DateTime? TryGetDate(MetadataExtractor.Directory? directory, int tagType)
    {
        return directory is not null && directory.TryGetDateTime(tagType, out var value)
            ? value
            : null;
    }
}
