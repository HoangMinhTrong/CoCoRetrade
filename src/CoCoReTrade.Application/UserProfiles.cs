namespace CoCoReTrade.Application;

public sealed record UserProfileDto(string Id, string FullName, int TrustScore, bool HasAvatar);
public sealed record AvatarLocation(string StorageKey, string ContentType);

public interface IUserProfileRepository
{
    Task<UserProfileDto?> GetAsync(string userId, CancellationToken ct);
    Task<AvatarLocation?> AvatarAsync(string userId, CancellationToken ct);
    Task<string?> SetAvatarAsync(string userId, string key, string contentType, CancellationToken ct);
}

public interface IUserProfileService
{
    Task<UserProfileDto?> GetAsync(string userId, CancellationToken ct);
    Task SetAvatarAsync(string userId, ImageUpload image, CancellationToken ct);
    Task<(Stream Stream, string ContentType)?> OpenAvatarAsync(string userId, CancellationToken ct);
}

public sealed class UserProfileService(IUserProfileRepository repository, IPrivateImageStore images) : IUserProfileService
{
    public Task<UserProfileDto?> GetAsync(string userId, CancellationToken ct) => repository.GetAsync(userId, ct);
    public async Task SetAvatarAsync(string userId, ImageUpload image, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new UnauthorizedAccessException("Hãy đăng nhập.");
        var (key, contentType) = await images.SaveAsync(image, ct);
        string? previous;
        try { previous = await repository.SetAvatarAsync(userId, key, contentType, ct); }
        catch { await images.DeleteAsync(key, CancellationToken.None); throw; }
        if (!string.IsNullOrWhiteSpace(previous)) await images.DeleteAsync(previous, ct);
    }
    public async Task<(Stream Stream, string ContentType)?> OpenAvatarAsync(string userId, CancellationToken ct)
    {
        var avatar = await repository.AvatarAsync(userId, ct);
        return avatar is null ? null : (await images.OpenAsync(avatar.StorageKey, ct), avatar.ContentType);
    }
}
