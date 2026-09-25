using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Catalog.Services;

// Stores uploaded product images in the "product-images" blob container (Azurite locally,
// Azure Blob Storage in the cloud). Products keep only the blob name, e.g. "uploads/<guid>.png";
// the image itself is served back through GET /products/images/{name}.
public class ProductImageStorage(BlobContainerClient container)
{
    public const string UploadPrefix = "uploads/";
    public const long MaxBytes = 5 * 1024 * 1024;

    // We pick the extension from the content type, never from the user's file name.
    // SVG is deliberately not allowed: it can carry scripts.
    public static readonly IReadOnlyDictionary<string, string> AllowedTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
        ["image/gif"] = ".gif"
    };

    public static bool IsUploadedImage(string? imageUrl) =>
        imageUrl is not null && imageUrl.StartsWith(UploadPrefix, StringComparison.Ordinal);

    public async Task<string> UploadAsync(Stream content, string contentType)
    {
        await container.CreateIfNotExistsAsync();

        var blobName = $"{UploadPrefix}{Guid.NewGuid():N}{AllowedTypes[contentType]}";
        await container.GetBlobClient(blobName).UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        });

        return blobName;
    }

    public async Task<(Stream Content, string ContentType)?> OpenReadAsync(string blobName)
    {
        try
        {
            var download = await container.GetBlobClient(blobName).DownloadStreamingAsync();
            return (download.Value.Content, download.Value.Details.ContentType ?? "application/octet-stream");
        }
        catch (RequestFailedException ex) when (ex.Status == StatusCodes.Status404NotFound)
        {
            return null;
        }
    }

    public async Task DeleteIfUploadedAsync(string? imageUrl)
    {
        if (IsUploadedImage(imageUrl))
        {
            await container.GetBlobClient(imageUrl).DeleteIfExistsAsync();
        }
    }
}
