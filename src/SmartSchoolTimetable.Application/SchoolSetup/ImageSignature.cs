namespace SmartSchoolTimetable.Application.SchoolSetup;

public sealed record ImageFormat(string ContentType, string Extension);

/// <summary>
/// Detects allowed image formats from their magic bytes (PNG, JPEG, WebP). Anything else, including SVG,
/// GIF and HTML, is rejected. The declared content type is never trusted on its own.
/// </summary>
public static class ImageSignature
{
    public const long MaxBytes = 2 * 1024 * 1024;

    public static readonly ImageFormat Png = new("image/png", "png");
    public static readonly ImageFormat Jpeg = new("image/jpeg", "jpg");
    public static readonly ImageFormat WebP = new("image/webp", "webp");

    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegMagic = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] RiffMagic = [0x52, 0x49, 0x46, 0x46];
    private static readonly byte[] WebPMagic = [0x57, 0x45, 0x42, 0x50];

    public static ImageFormat? Detect(ReadOnlySpan<byte> content)
    {
        if (content.StartsWith(PngMagic))
            return Png;
        if (content.StartsWith(JpegMagic))
            return Jpeg;
        if (content.Length >= 12 && content[..4].SequenceEqual(RiffMagic) && content[8..12].SequenceEqual(WebPMagic))
            return WebP;
        return null;
    }

    /// <summary>True when the declared type is absent or names the same format as the bytes.</summary>
    public static bool MatchesDeclared(ImageFormat detected, string? declaredContentType)
    {
        ArgumentNullException.ThrowIfNull(detected);
        if (string.IsNullOrWhiteSpace(declaredContentType))
            return true;
        var declared = declaredContentType.Split(';')[0].Trim().ToLowerInvariant();
        return declared == detected.ContentType || (detected == Jpeg && declared == "image/jpg");
    }
}
