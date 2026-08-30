using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Tutor.Api.Utilities;

public static class ImageUtility
{
    private const int MaximumAvatarUploadSize = 1024 * 1024;
    private const int MaximumAvatarDimension = 512;
    private static readonly HashSet<string> SupportedImageFormats =
        new(StringComparer.OrdinalIgnoreCase) { "JPEG", "PNG", "WEBP", "GIF", "BMP" };

    public static async Task<SavedImage> ProcessAndSaveAvatar(
        IFormFile uploadedImage,
        string destinationDirectory,
        ILogger logger)
    {
        ValidateAvatarUpload(uploadedImage, logger);

        await using var input = uploadedImage.OpenReadStream();
        Image image;
        try
        {
            image = await Image.LoadAsync(input);
        }
        catch (Exception exception) when (exception is UnknownImageFormatException or InvalidImageContentException)
        {
            logger.LogWarning(
                exception,
                "Avatar upload rejected because {FileName} is not a valid image.",
                uploadedImage.FileName);
            throw new ArgumentException("The uploaded file is not a valid supported image.", nameof(uploadedImage), exception);
        }

        using (image)
        {
            var format = image.Metadata.DecodedImageFormat;
            if (format is null)
            {
                logger.LogWarning(
                    "Avatar upload rejected because the format of {FileName} could not be detected.",
                    uploadedImage.FileName);
                throw new ArgumentException("The image format could not be detected.", nameof(uploadedImage));
            }

            if (!SupportedImageFormats.Contains(format.Name))
            {
                logger.LogWarning(
                    "Avatar upload rejected because image format {ImageFormat} is not supported.",
                    format.Name);
                throw new ArgumentException("The image format is not supported.", nameof(uploadedImage));
            }

            image.Mutate(x => x
                .AutoOrient()
                .Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(MaximumAvatarDimension, MaximumAvatarDimension)
                }));

            var extension = format.FileExtensions.First().ToLowerInvariant();
            var fileName = $"{Guid.CreateVersion7():N}.{extension}";
            Directory.CreateDirectory(destinationDirectory);
            var filePath = Path.Combine(destinationDirectory, fileName);
            var encoder = image.Configuration.ImageFormatsManager.GetEncoder(format);
            await image.SaveAsync(filePath, encoder);

            return new SavedImage(fileName, format.Name, filePath);
        }
    }

    public static string GetContentType(string format) => format.ToUpperInvariant() switch
    {
        "JPEG" => "image/jpeg",
        "PNG" => "image/png",
        "WEBP" => "image/webp",
        "GIF" => "image/gif",
        "BMP" => "image/bmp",
        _ => "application/octet-stream"
    };

    public static void DeleteImageFile(string filePath, ILogger logger)
    {
        try
        {
            File.Delete(filePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(
                exception,
                "Failed to delete image file {FileName}.",
                Path.GetFileName(filePath));
        }
    }

    private static void ValidateAvatarUpload(IFormFile uploadedImage, ILogger logger)
    {
        if (uploadedImage.Length is <= 0 or >= MaximumAvatarUploadSize)
        {
            logger.LogWarning(
                "Avatar upload rejected because its size is {SizeBytes} bytes; it must be non-empty and less than {MaximumSizeBytes} bytes.",
                uploadedImage.Length,
                MaximumAvatarUploadSize);
            throw new ArgumentException("The image must be non-empty and less than 1 MB.", nameof(uploadedImage));
        }
    }
}

public sealed record SavedImage(string FileName, string Format, string FilePath);
