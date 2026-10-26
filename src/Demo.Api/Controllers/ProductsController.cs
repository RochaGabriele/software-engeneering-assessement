using Demo.Application.Modules;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductController : ControllerBase
{
    private readonly IInventoryModule _inventoryModule;

    public ProductController(
        IInventoryModule inventoryModule
    )
    {
        _inventoryModule = inventoryModule;
    }

    [HttpGet("{sku}")]
    public async Task<IActionResult> GetProduct(string sku)
    {
        var product = await _inventoryModule.GetProduct(sku);

        return Ok(product);
    }
}
