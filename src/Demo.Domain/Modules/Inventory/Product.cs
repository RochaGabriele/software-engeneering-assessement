namespace Demo.Domain.Modules.Inventory
{
    public class Product
    {
        public required string Sku { get; set; }
        public required string Name { get; set; }
        public required decimal Price { get; set; }
    }
}
