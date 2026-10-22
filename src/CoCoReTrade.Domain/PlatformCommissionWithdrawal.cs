namespace CoCoReTrade.Domain;

public sealed class PlatformCommissionWithdrawal
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid RequestKey { get; private set; }
    public string AdminId { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string BankAccountNumber { get; private set; } = string.Empty;
    public string BankName { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }

    private PlatformCommissionWithdrawal() { }

    public PlatformCommissionWithdrawal(Guid requestKey, string adminId, decimal amount,
        string bankAccountNumber, string bankName, DateTime nowUtc)
    {
        if (requestKey == Guid.Empty || string.IsNullOrWhiteSpace(adminId) || amount <= 0 ||
            amount != decimal.Truncate(amount) || nowUtc.Kind != DateTimeKind.Utc ||
            !System.Text.RegularExpressions.Regex.IsMatch(bankAccountNumber ?? "", @"^[0-9]{6,30}$") ||
            string.IsNullOrWhiteSpace(bankName) || bankName.Trim().Length is < 2 or > 100)
            throw new ArgumentException("Yêu cầu rút hoa hồng không hợp lệ.");
        RequestKey = requestKey;
        AdminId = adminId;
        Amount = amount;
        BankAccountNumber = bankAccountNumber!.Trim();
        BankName = bankName.Trim();
        CreatedAtUtc = nowUtc;
    }
}

public sealed class AdminPayoutAccount
{
    public string AdminId { get; private set; } = string.Empty;
    public string BankAccountNumber { get; private set; } = string.Empty;
    public string BankName { get; private set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; private set; }

    private AdminPayoutAccount() { }

    public AdminPayoutAccount(string adminId, string bankAccountNumber, string bankName, DateTime nowUtc)
    {
        AdminId = adminId;
        Update(bankAccountNumber, bankName, nowUtc);
    }

    public void Update(string bankAccountNumber, string bankName, DateTime nowUtc)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(bankAccountNumber ?? "", @"^[0-9]{6,30}$") ||
            string.IsNullOrWhiteSpace(bankName) || bankName.Trim().Length is < 2 or > 100 ||
            nowUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Thông tin ngân hàng không hợp lệ.");
        BankAccountNumber = bankAccountNumber!.Trim();
        BankName = bankName.Trim();
        UpdatedAtUtc = nowUtc;
    }
}
