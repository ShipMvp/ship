using ShipMvp.Core.Abstractions;

namespace ShipMvp.Application.Identity;

// Application Service Interfaces
public interface IUserService : IScopedService
{
    Task<UserDto> CreateAsync(CreateUserDto request, CancellationToken cancellationToken = default);
    Task<UserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<UserDto?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<IEnumerable<UserDto>> GetListAsync(GetUsersQuery query, CancellationToken cancellationToken = default);
    Task<UserDto> UpdateAsync(Guid id, UpdateUserDto request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<UserDto> AddToRoleAsync(Guid id, string role, CancellationToken cancellationToken = default);
    Task<UserDto> RemoveFromRoleAsync(Guid id, string role, CancellationToken cancellationToken = default);
}
