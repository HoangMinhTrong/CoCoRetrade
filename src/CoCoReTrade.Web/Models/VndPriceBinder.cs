using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CoCoReTrade.Web.Models;

public sealed partial class VndPriceBinder : IModelBinder
{
    [GeneratedRegex(@"^(?:[0-9]+|[0-9]{1,3}(?:,[0-9]{3})+)$")]
    private static partial Regex PricePattern();

    public Task BindModelAsync(ModelBindingContext context)
    {
        var value = context.ValueProvider.GetValue(context.ModelName);
        if (value == ValueProviderResult.None) return Task.CompletedTask;
        context.ModelState.SetModelValue(context.ModelName, value);

        var entered = value.FirstValue?.Trim();
        if (string.IsNullOrEmpty(entered) || !PricePattern().IsMatch(entered) ||
            !decimal.TryParse(entered.Replace(",", ""), NumberStyles.None,
                CultureInfo.InvariantCulture, out var price))
        {
            context.ModelState.TryAddModelError(context.ModelName, "Vui lòng nhập giá bằng số VNĐ hợp lệ.");
            return Task.CompletedTask;
        }

        context.Result = ModelBindingResult.Success(price);
        return Task.CompletedTask;
    }
}
