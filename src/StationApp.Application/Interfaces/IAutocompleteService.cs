using StationApp.Application.DTOs;

namespace StationApp.Application.Interfaces;

public interface IAutocompleteService
{
    Task<IReadOnlyList<AutocompleteItem>> SearchAsync(AutocompleteQuery query, CancellationToken ct);
}
