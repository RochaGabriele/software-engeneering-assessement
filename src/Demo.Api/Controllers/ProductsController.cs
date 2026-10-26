using Demo.Api.ErrorHandling;
using Demo.Application.Common;
using Demo.Application.Modules.Inventory;
using Demo.Application.Modules.Inventory.GetProductBySku;
using Microsoft.AspNetCore.Mvc;

namespace Demo.Api.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController : ControllerBase
{
    private readonly IQueryHandler<GetProductBySkuQuery, Result<ProductDto>> _getProductBySku;

    public ProductsController(IQueryHandler<GetProductBySkuQuery, Result<ProductDto>> getProductBySku)
    {
        _getProductBySku = getProductBySku;
    }

    [HttpGet("{sku}")]
    [ProducesResponseType<ProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> GetBySkuAsync(string sku, CancellationToken cancellationToken)
    {
        var result = await _getProductBySku.HandleAsync(new GetProductBySkuQuery(sku), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : this.ToProblemResult(result.Error!);
    }
}
