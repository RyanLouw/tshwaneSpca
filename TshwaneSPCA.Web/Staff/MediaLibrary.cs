using TshwaneSPCA.Data;

namespace TshwaneSPCA.Web.Staff;

public sealed class MediaLibrary(string directory)
{
    public const int MaxFileBytes = 10 * 1024 * 1024;
    public const int MaxImageBytes = 5 * 1024 * 1024;

    public async Task<(string Name, string ContentType)?> StoreAsync(IFormFile file, AssetKind kind,
        CancellationToken cancellationToken)
    {
        var limit = kind == AssetKind.Gallery ? MaxImageBytes : MaxFileBytes;
        if (file.Length < 16 || file.Length > limit)
            return null;

        var content = new byte[(int)file.Length];
        await using (var source = file.OpenReadStream())
            await source.ReadExactlyAsync(content, cancellationToken);

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var contentType = Identify(content, extension, kind);
        if (contentType is null)
            return null;

        Directory.CreateDirectory(directory);
        var name = $"{Guid.NewGuid():N}{extension}";
        var created = false;
        try
        {
            await using var destination = new FileStream(Path.Combine(directory, name), FileMode.CreateNew, FileAccess.Write,
                FileShare.None, bufferSize: 8192, useAsync: true);
            created = true;
            await destination.WriteAsync(content, cancellationToken);
        }
        catch
        {
            if (created)
                Delete(name);
            throw;
        }
        return (name, contentType);
    }

    public Stream? Open(PublicAsset asset)
    {
        if (Path.GetFileName(asset.StorageName) != asset.StorageName ||
            !Guid.TryParseExact(Path.GetFileNameWithoutExtension(asset.StorageName), "N", out _))
            return null;
        try
        {
            return File.OpenRead(Path.Combine(directory, asset.StorageName));
        }
        catch (IOException)
        {
            return null;
        }
    }

    public void Delete(string generatedName) => File.Delete(Path.Combine(directory, generatedName));

    public static string DownloadName(PublicAsset asset) => $"{asset.Kind.ToString().ToLowerInvariant()}-{asset.Id:N}.pdf";

    private static string? Identify(ReadOnlySpan<byte> bytes, string extension, AssetKind kind)
    {
        if (kind != AssetKind.Gallery)
        {
            var end = bytes.Length;
            while (end > 0 && bytes[end - 1] is 0 or 9 or 10 or 12 or 13 or 32)
                end--;
            return extension == ".pdf" && bytes.StartsWith("%PDF-"u8) && bytes[..end].EndsWith("%%EOF"u8)
                ? "application/pdf" : null;
        }

        if (extension is ".jpg" or ".jpeg" && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff &&
            bytes[^2] == 0xff && bytes[^1] == 0xd9)
            return "image/jpeg";

        if (extension == ".png" && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            && bytes[12..16].SequenceEqual("IHDR"u8) && bytes[^8..^4].SequenceEqual("IEND"u8))
            return "image/png";

        return null;
    }
}
