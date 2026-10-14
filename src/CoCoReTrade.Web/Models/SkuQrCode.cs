using System.Text.RegularExpressions;
using QRCoder;

namespace CoCoReTrade.Web.Models;

public static class SkuQrCode
{
    public static string Svg(string sku)
    {
        if (string.IsNullOrWhiteSpace(sku) || !Regex.IsMatch(sku, @"^CC-\d{4}-\d{6}$"))
            throw new ArgumentException("Invalid inventory SKU.");
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(sku, QRCodeGenerator.ECCLevel.Q);
        using var qr = new SvgQRCode(data);
        return qr.GetGraphic(4);
    }
}
