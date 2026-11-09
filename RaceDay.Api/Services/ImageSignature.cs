namespace RaceDay.Api.Services;

/// <summary>
/// Identifies an image by its leading "magic bytes" instead of trusting the file name or the
/// Content-Type header, which a client can set to anything.
/// </summary>
public static class ImageSignature
{
    /// <summary>Returns the verified MIME type and extension, or null when the bytes are not a supported image.</summary>
    public static (string ContentType, string Extension)? Detect(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return ("image/jpeg", ".jpg");

        if (header.Length >= 8 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
            && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
            return ("image/png", ".png");

        if (header.Length >= 6 && header[0] == 'G' && header[1] == 'I' && header[2] == 'F' && header[3] == '8'
            && (header[4] == '7' || header[4] == '9') && header[5] == 'a')
            return ("image/gif", ".gif");

        // WebP: "RIFF" <4 byte size> "WEBP"
        if (header.Length >= 12 && header[0] == 'R' && header[1] == 'I' && header[2] == 'F' && header[3] == 'F'
            && header[8] == 'W' && header[9] == 'E' && header[10] == 'B' && header[11] == 'P')
            return ("image/webp", ".webp");

        return null;
    }
}
