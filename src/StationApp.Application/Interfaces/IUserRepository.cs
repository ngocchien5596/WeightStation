using StationApp.Domain.Entities;

namespace StationApp.Application.Interfaces;

public interface IUserRepository
{
    Task AddAsync(User user, CancellationToken ct);
    Task UpdateAsync(User user, CancellationToken ct);
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct);
    Task<bool> ExistsByUsernameAsync(string username, CancellationToken ct);
    Task<IReadOnlyList<User>> SearchAsync(string? username, string? displayName, string? roleCode, bool? isActive, CancellationToken ct);
}
