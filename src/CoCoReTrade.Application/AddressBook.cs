using System.Text.RegularExpressions;
using CoCoReTrade.Domain;

namespace CoCoReTrade.Application;

public sealed record AddressInput(Guid? Id, string RecipientName, string RecipientPhone,
    string Province, string Ward, string StreetAddress);
public sealed record AddressDto(Guid Id, string RecipientName, string RecipientPhone,
    string Province, string Ward, string StreetAddress);

public interface IAddressRepository
{
    Task<IReadOnlyList<Address>> ForUserAsync(string userId, CancellationToken ct);
    Task<Address?> OwnedAsync(Guid id, string userId, CancellationToken ct);
    void Add(Address address);
    void Remove(Address address);
    Task SaveAsync(CancellationToken ct);
}

public interface IAddressBookService
{
    Task<IReadOnlyList<AddressDto>> ListAsync(string userId, CancellationToken ct);
    Task<AddressDto> SaveAsync(string userId, AddressInput input, CancellationToken ct);
    Task DeleteAsync(string userId, Guid id, CancellationToken ct);
}

public sealed class AddressBookService(IAddressRepository repository) : IAddressBookService
{
    public async Task<IReadOnlyList<AddressDto>> ListAsync(string userId, CancellationToken ct)
    {
        RequireUser(userId);
        return (await repository.ForUserAsync(userId, ct)).Select(ToDto).ToList();
    }
    public async Task<AddressDto> SaveAsync(string userId, AddressInput input, CancellationToken ct)
    {
        RequireUser(userId);
        if (string.IsNullOrWhiteSpace(input.RecipientName) || input.RecipientName.Trim().Length is < 2 or > 150 ||
            !Regex.IsMatch(input.RecipientPhone ?? "", @"^0[35789][0-9]{8}$") ||
            string.IsNullOrWhiteSpace(input.Province) || input.Province.Trim().Length is < 2 or > 120 ||
            string.IsNullOrWhiteSpace(input.Ward) || input.Ward.Trim().Length is < 2 or > 120 ||
            string.IsNullOrWhiteSpace(input.StreetAddress) || input.StreetAddress.Trim().Length is < 4 or > 300)
            throw new ArgumentException("Vui lòng nhập đầy đủ tên, số điện thoại và địa chỉ hợp lệ.");
        var address = input.Id.HasValue
            ? await repository.OwnedAsync(input.Id.Value, userId, ct) ?? throw new KeyNotFoundException("Địa chỉ không tồn tại.")
            : new Address { Id = Guid.NewGuid(), UserId = userId };
        address.RecipientName = input.RecipientName.Trim();
        address.RecipientPhone = input.RecipientPhone!.Trim();
        address.Province = input.Province.Trim();
        address.Ward = input.Ward.Trim();
        address.StreetAddress = input.StreetAddress.Trim();
        if (!input.Id.HasValue) repository.Add(address);
        await repository.SaveAsync(ct);
        return ToDto(address);
    }
    public async Task DeleteAsync(string userId, Guid id, CancellationToken ct)
    {
        RequireUser(userId);
        var address = await repository.OwnedAsync(id, userId, ct) ?? throw new KeyNotFoundException("Địa chỉ không tồn tại.");
        repository.Remove(address);
        await repository.SaveAsync(ct);
    }
    private static AddressDto ToDto(Address x) => new(x.Id, x.RecipientName, x.RecipientPhone,
        x.Province, x.Ward, x.StreetAddress);
    private static void RequireUser(string userId)
    { if (string.IsNullOrWhiteSpace(userId)) throw new UnauthorizedAccessException("Hãy đăng nhập."); }
}
