using Demo.Application.Common;
using Demo.Application.Modules.Inventory;
using Demo.Application.Modules.Inventory.GetProductBySku;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Demo.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IQueryHandler<GetProductBySkuQuery, Result<ProductDto>>, GetProductBySkuQueryHandler>();

        return services;
    }
}
