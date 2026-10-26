namespace Demo.Domain.Modules.Inventory
{
    public interface IProductRepository
    {
        Task<Product> GetProduct(string sku);
    }
}
