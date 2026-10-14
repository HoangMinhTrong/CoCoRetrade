using CoCoReTrade.Application;
using Microsoft.AspNetCore.Mvc;

namespace CoCoReTrade.Web.Controllers.Api.V1;

[ApiController]
[Route("api/v1/products")]
public sealed class ProductsController(IProductService products) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProductCard>>> Get([FromQuery] ProductFilter filter, CancellationToken ct) =>
        Ok(await products.ShopAsync(filter, ct));
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDetails>> GetOne(Guid id, CancellationToken ct)
    {
        var product = await products.VisibleDetailsAsync(id, null, false, ct);
        return product is null ? NotFound() : Ok(product);
    }
}
