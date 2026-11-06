using System.ComponentModel.DataAnnotations;
using CoCoReTrade.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;

namespace CoCoReTrade.Tests;

public sealed class ListingFormTests
{
    [Fact]
    public void EmptyBrandIsValidForListingAndInspection()
    {
        var listing = new ListingForm
        {
            Name = "Áo khoác denim", Brand = null, Category = "Áo", Size = "M",
            OriginalPrice = 200_000, AskingPrice = 150_000, ConditionPercent = 95,
            AcknowledgedDeliveryInstructions = true
        };
        var inspection = new InspectionForm
        {
            ProductId = Guid.NewGuid(), ExpectedRowVersion = "version", Passed = true,
            Name = "Áo khoác denim", Brand = null, Category = "Áo", Size = "M",
            OriginalPrice = 200_000, AskingPrice = 150_000, VerifiedCondition = 95
        };

        Assert.True(Validator.TryValidateObject(listing, new ValidationContext(listing), [], true));
        Assert.True(Validator.TryValidateObject(inspection, new ValidationContext(inspection), [], true));
        Assert.Equal("", listing.ToRequest().Brand);
        Assert.Equal("", inspection.ToRequest().Brand);
    }

    [Fact]
    public void ListingCannotBeSubmittedWithoutAcknowledgingDeliveryInstructions()
    {
        var form = new ListingForm
        {
            Name = "Áo khoác denim", Category = "Áo", Size = "M",
            OriginalPrice = 200_000, AskingPrice = 150_000, ConditionPercent = 95
        };
        var errors = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(form, new ValidationContext(form), errors, true));
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(ListingForm.AcknowledgedDeliveryInstructions)));
        form.AcknowledgedDeliveryInstructions = true;
        Assert.True(Validator.TryValidateObject(form, new ValidationContext(form), [], true));
    }

    [Theory]
    [InlineData("150000", 150000, true)]
    [InlineData("150,000", 150000, true)]
    [InlineData("150,00", 0, false)]
    [InlineData("150.5", 0, false)]
    public async Task PriceBinderHandlesFormattedWholeVnd(string input, int expected, bool valid)
    {
        var state = new ModelStateDictionary();
        var action = new ActionContext(new DefaultHttpContext(), new RouteData(),
            new ActionDescriptor(), state);
        var metadata = new EmptyModelMetadataProvider().GetMetadataForType(typeof(decimal));
        var context = DefaultModelBindingContext.CreateBindingContext(action,
            new OneValueProvider("OriginalPrice", input), metadata, null, "OriginalPrice");

        await new VndPriceBinder().BindModelAsync(context);

        Assert.Equal(valid, context.Result.IsModelSet);
        if (valid) Assert.Equal((decimal)expected, Assert.IsType<decimal>(context.Result.Model));
        else Assert.False(state.IsValid);
    }

    private sealed class OneValueProvider(string field, string value) : IValueProvider
    {
        public bool ContainsPrefix(string prefix) => prefix == field;
        public ValueProviderResult GetValue(string key) =>
            key == field ? new ValueProviderResult(value) : ValueProviderResult.None;
    }
}
