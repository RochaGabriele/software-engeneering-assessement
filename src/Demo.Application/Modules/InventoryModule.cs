using Demo.Domain.Modules.Inventory;
using Microsoft.Extensions.Logging;

namespace Demo.Application.Modules
{
    public interface IInventoryModule
    {
        Task<Product> GetProduct(string sku);
    }

    public class InventoryModule : IInventoryModule
    {
        private readonly ILogger _logger;
        private readonly IProductRepository _productRepository;

        public InventoryModule(
            ILogger<InventoryModule> logger,
            IProductRepository productRepository
        )
        {
            _logger = logger;
            _productRepository = productRepository;
        }

        public Task<Product> GetProduct(string sku)
        {
            return _productRepository.GetProduct(sku);
        }
    }
}
