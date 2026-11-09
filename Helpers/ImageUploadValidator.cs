namespace RaceDay.Web.Helpers;

/// <summary>
/// Quick, friendly checks run before an image is forwarded to the API, so people get a clear message
/// immediately. The API repeats the real validation (it inspects the file bytes) - this is only UX.
/// </summary>
public static class ImageUploadValidator
{
    public const long MaxBytes = 5 * 1024 * 1024;

    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];

    /// <summary>Returns null when the file looks acceptable, otherwise a message to show.</summary>
    public static string? Validate(IFormFile? file)
    {
        if (file is null || file.Length == 0) return null;   // "no file chosen" is fine - uploads are optional

        if (file.Length > MaxBytes)
            return "That image is larger than 5 MB. Please choose a smaller one.";

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            return "Please choose a JPG, PNG, GIF or WebP image.";

        return null;
    }
}
