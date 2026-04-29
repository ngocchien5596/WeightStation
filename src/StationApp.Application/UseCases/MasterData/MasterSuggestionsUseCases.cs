using StationApp.Application.Interfaces;
using StationApp.Domain.Entities;

namespace StationApp.Application.UseCases.MasterData;

public class SearchCustomerSuggestionsUseCase
{
    private readonly ICustomerRepository _customerRepo;

    public SearchCustomerSuggestionsUseCase(ICustomerRepository customerRepo)
    {
        _customerRepo = customerRepo;
    }

    public async Task<IReadOnlyList<Customer>> ExecuteAsync(string? keyword, CancellationToken ct)
    {
        return await _customerRepo.SearchAsync(keyword, ct);
    }
}

public class SearchProductSuggestionsUseCase
{
    private readonly IProductRepository _productRepo;

    public SearchProductSuggestionsUseCase(IProductRepository productRepo)
    {
        _productRepo = productRepo;
    }

    public async Task<IReadOnlyList<Product>> ExecuteAsync(string? keyword, CancellationToken ct)
    {
        return await _productRepo.SearchAsync(keyword, ct);
    }
}
