using System.Text.RegularExpressions;
using CoCoReTrade.Domain;
using Microsoft.EntityFrameworkCore;

namespace CoCoReTrade.Infrastructure;

internal static partial class NotificationProductNames
{
    [GeneratedRegex(@"\b(?:CC|CD)-[A-Za-z0-9-]+\b", RegexOptions.IgnoreCase)]
    private static partial Regex ProductCodeRegex();

    public static async Task ApplyAsync(AppDbContext db, IReadOnlyCollection<MarketplaceNotification> notifications,
        CancellationToken ct)
    {
        var codes = notifications.SelectMany(n => ProductCodeRegex().Matches(n.Message).Select(m => m.Value))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (codes.Length == 0) return;

        var products = await db.Products.AsNoTracking()
            .Where(p => codes.Contains(p.Sku) || codes.Contains(p.ConsignmentCode))
            .Select(p => new { p.Sku, p.ConsignmentCode, p.Name }).ToListAsync(ct);
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var product in products)
        {
            if (!string.IsNullOrWhiteSpace(product.Sku)) names[product.Sku] = product.Name;
            if (!string.IsNullOrWhiteSpace(product.ConsignmentCode)) names[product.ConsignmentCode] = product.Name;
        }
        foreach (var notification in notifications)
        {
            var message = notification.Message;
            foreach (var code in codes.Where(code => code.StartsWith("CC-", StringComparison.OrdinalIgnoreCase) &&
                                                names.ContainsKey(code)))
                message = message.Replace($" với mã SKU {code}", string.Empty, StringComparison.OrdinalIgnoreCase);
            notification.Message = ProductCodeRegex().Replace(message, match =>
                names.TryGetValue(match.Value, out var name) && !string.IsNullOrWhiteSpace(name) &&
                !message.Contains(name, StringComparison.OrdinalIgnoreCase) ? name : match.Value);
        }
    }
}
