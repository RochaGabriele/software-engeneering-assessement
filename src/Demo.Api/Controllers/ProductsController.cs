using Demo.Api.ErrorHandling;
using Demo.Application.Common;
using Demo.Application.Modules.Inventory;
using Demo.Application.Modules.Inventory.GetProductBySku;
using Demo.Application.Modules.Inventory.GetProductCatalog;
using Microsoft.AspNetCore.Mvc;

namespace Demo.Api.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController : ControllerBase
{
    private readonly IQueryHandler<GetProductCatalogQuery, Result<IReadOnlyList<ProductDto>>> _getProductCatalog;
    private readonly IQueryHandler<GetProductBySkuQuery, Result<ProductDto>> _getProductBySku;

    public ProductsController(
        IQueryHandler<GetProductCatalogQuery, Result<IReadOnlyList<ProductDto>>> getProductCatalog,
        IQueryHandler<GetProductBySkuQuery, Result<ProductDto>> getProductBySku)
    {
        _getProductCatalog = getProductCatalog;
        _getProductBySku = getProductBySku;
    }

    /// <summary>Lists the entire product catalog, optionally converted to another currency.</summary>
    /// <param name="currency">ISO 4217 code (USD, EUR, CAD, GBP or CHF, case-insensitive). Omit for USD.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ProductDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<IActionResult> GetCatalogAsync([FromQuery] string? currency, CancellationToken cancellationToken)
    {
        var result = await _getProductCatalog.HandleAsync(new GetProductCatalogQuery(currency), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : this.ToProblemResult(result.Error!);
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
