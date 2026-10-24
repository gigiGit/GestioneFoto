using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace GestioneFoto.App.Services;

public sealed class ExifDateReader
{
    public DateTime? TryRead(string filePath)
    {
        try
        {
            var directories = ImageMetadataReader.ReadMetadata(filePath);

            var subIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();

            if (TryGetDate(subIfd, ExifDirectoryBase.TagDateTimeOriginal, out var original))
            {
                return original;
            }

            if (TryGetDate(subIfd, ExifDirectoryBase.TagDateTimeDigitized, out var digitized))
            {
                return digitized;
            }

            if (TryGetDate(ifd0, ExifDirectoryBase.TagDateTime, out var generic))
            {
                return generic;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static bool TryGetDate(MetadataExtractor.Directory? directory, int tagType, out DateTime value)
    {
        value = default;
        return directory is not null && directory.TryGetDateTime(tagType, out value);
    }
}
