namespace RaceDay.Api.Services;

/// <summary>Checks that an uploaded file really is a small JPG, PNG or WebP image.</summary>
public static class ImageFileValidator
{
    public const long MaxBytes = 5 * 1024 * 1024; // 5 MB

    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp"
    };

    /// <summary>Returns an error message, or null when the file is acceptable.</summary>
    public static string? Validate(IFormFile? file, out string extension, out string contentType)
    {
        extension = string.Empty;
        contentType = string.Empty;

        if (file == null || file.Length == 0)
            return "Please choose an image file.";

        if (file.Length > MaxBytes)
            return "The image must be 5 MB or smaller.";

        extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!ContentTypes.TryGetValue(extension, out var type))
            return "Only JPG, PNG or WebP images are allowed.";
        contentType = type;

        // Don't trust the file name: check the first bytes really match the image type
        var header = new byte[12];
        using (var stream = file.OpenReadStream())
        {
            var read = stream.Read(header, 0, header.Length);
            if (read < header.Length || !HasImageSignature(header, extension))
                return "That file is not a valid image.";
        }

        return null;
    }

    private static bool HasImageSignature(byte[] h, string extension) => extension switch
    {
        ".jpg" or ".jpeg" => h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF,
        ".png" => h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47,
        ".webp" => h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F'
                   && h[8] == 'W' && h[9] == 'E' && h[10] == 'B' && h[11] == 'P',
        _ => false
    };
}