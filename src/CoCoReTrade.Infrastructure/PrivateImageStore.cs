using System.Text.RegularExpressions;
using CoCoReTrade.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace CoCoReTrade.Infrastructure;

public sealed class PrivateImageStore : IPrivateImageStore
{
    private readonly string _root;
    public PrivateImageStore(IHostEnvironment environment, IConfiguration configuration)
    {
        _root = Path.GetFullPath(configuration["Storage:Root"] ?? Path.Combine(environment.ContentRootPath, "PrivateUploads"));
        Directory.CreateDirectory(_root);
    }
    public async Task<(string Key, string ContentType)> SaveAsync(ImageUpload image, CancellationToken ct)
    {
        if (image.Length is < 100 or > 5_000_000) throw new ArgumentException("Mỗi ảnh cần nhỏ hơn 5 MB.");
        var ext = Path.GetExtension(image.FileName).ToLowerInvariant();
        var expectedType = ext switch { ".jpg" or ".jpeg" => "image/jpeg", ".png" => "image/png", ".webp" => "image/webp", _ => "" };
        if (expectedType.Length == 0 || image.ContentType != expectedType) throw new ArgumentException("Chỉ nhận ảnh JPG, PNG hoặc WebP.");
        await using var input = image.OpenReadStream();
        var header = new byte[12];
        try { await input.ReadExactlyAsync(header, ct); }
        catch (EndOfStreamException) { throw new ArgumentException("Nội dung ảnh không hợp lệ."); }
        if (!Matches(header, ext)) throw new ArgumentException("Nội dung ảnh không hợp lệ.");
        var key = Guid.NewGuid().ToString("N") + ext;
        var path = Path.Combine(_root, key);
        try
        {
            await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
            await output.WriteAsync(header, ct);
            var buffer = new byte[81920];
            long total = header.Length;
            int read;
            while ((read = await input.ReadAsync(buffer, ct)) > 0)
            {
                total += read;
                if (total > 5_000_000) throw new ArgumentException("Ảnh vượt quá 5 MB.");
                await output.WriteAsync(buffer.AsMemory(0, read), ct);
            }
        }
        catch { File.Delete(path); throw; }
        return (key, expectedType);
    }
    public Task DeleteAsync(string key, CancellationToken ct)
    {
        var path = SafePath(key);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }
    public Task<Stream> OpenAsync(string key, CancellationToken ct) =>
        Task.FromResult<Stream>(File.OpenRead(SafePath(key)));
    private string SafePath(string key)
    {
        if (!Regex.IsMatch(key, "^[a-f0-9]{32}\\.(jpg|jpeg|png|webp)$")) throw new ArgumentException("Invalid storage key.");
        return Path.Combine(_root, key);
    }
    private static bool Matches(byte[] h, string ext) => ext switch
    {
        ".jpg" or ".jpeg" => h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF,
        ".png" => h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47,
        ".webp" => h[0] == 0x52 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x46 && h[8] == 0x57 && h[9] == 0x45,
        _ => false
    };
}
