using ShipMvp.Domain.Identity;
using ShipMvp.Core;
using ShipMvp.Core.Security;
using ShipMvp.Core.Persistence;
using ShipMvp.Domain.Shared;

namespace ShipMvp.Application.Identity;

// Application Service Implementation
public class UserService : IUserService
{
    private readonly IUserRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;

    public UserService(IUserRepository repository, IUnitOfWork unitOfWork, IPasswordHasher passwordHasher)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
    }

    public async Task<UserDto> CreateAsync(CreateUserDto request, CancellationToken cancellationToken = default)
    {
        // Check uniqueness
        if (!await _repository.IsUsernameUniqueAsync(request.Username, cancellationToken: cancellationToken))
            throw new InvalidOperationException($"Username '{request.Username}' is already taken");

        if (!await _repository.IsEmailUniqueAsync(request.Email, cancellationToken: cancellationToken))
            throw new InvalidOperationException($"Email '{request.Email}' is already taken");

        var email = request.Email.ToLowerInvariant();
        var phoneNumber = PhoneNumber.CreateOrDefault(request.PhoneNumber);
        var passwordHash = _passwordHasher.HashPassword(request.Password);

        var user = new User(
            Guid.NewGuid(),
            request.Username,
            request.Name,
            request.Surname,
            email,
            passwordHash,
            phoneNumber,
            request.IsActive
        );

        await _repository.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(user);
    }

    public async Task<UserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _repository.GetByIdAsync(id, cancellationToken);
        return user != null ? MapToDto(user) : null;
    }

    public async Task<UserDto?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var user = await _repository.GetByUsernameAsync(username, cancellationToken);
        return user != null ? MapToDto(user) : null;
    }

    public async Task<IEnumerable<UserDto>> GetListAsync(GetUsersQuery query, CancellationToken cancellationToken = default)
    {
        IEnumerable<User> users;

        if (!string.IsNullOrEmpty(query.Role))
        {
            users = await _repository.GetByRoleAsync(query.Role, cancellationToken);
        }
        else
        {
            users = await _repository.GetAllAsync(cancellationToken);
        }

        // Apply filters
        if (!string.IsNullOrEmpty(query.SearchText))
        {
            var searchLower = query.SearchText.ToLower();
            users = users.Where(u =>
                u.Username.ToLower().Contains(searchLower) ||
                u.Name.ToLower().Contains(searchLower) ||
                u.Surname.ToLower().Contains(searchLower) ||
                u.Email.ToLower().Contains(searchLower));
        }

        if (query.IsActive.HasValue)
        {
            users = users.Where(u => u.IsActive == query.IsActive.Value);
        }

        return users.Select(MapToDto);
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserDto request, CancellationToken cancellationToken = default)
    {
        var user = await _repository.GetByIdAsync(id, cancellationToken);
        if (user == null)
            throw new InvalidOperationException($"User {id} not found");

        var phoneNumber = PhoneNumber.CreateOrDefault(request.PhoneNumber);

        var updatedUser = user
            .UpdateInfo(request.Name, request.Surname, phoneNumber)
            .SetActive(request.IsActive);

        if (request.IsEmailConfirmed && !user.IsEmailConfirmed)
            updatedUser = updatedUser.ConfirmEmail();

        if (request.IsPhoneNumberConfirmed && !user.IsPhoneNumberConfirmed)
            updatedUser = updatedUser.ConfirmPhoneNumber();

        await _repository.UpdateAsync(updatedUser, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(updatedUser);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteAsync(id, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserDto> AddToRoleAsync(Guid id, string role, CancellationToken cancellationToken = default)
    {
        var user = await _repository.GetByIdAsync(id, cancellationToken);
        if (user == null)
            throw new InvalidOperationException($"User {id} not found");

        var updatedUser = user.AddRole(role);
        await _repository.UpdateAsync(updatedUser, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(updatedUser);
    }

    public async Task<UserDto> RemoveFromRoleAsync(Guid id, string role, CancellationToken cancellationToken = default)
    {
        var user = await _repository.GetByIdAsync(id, cancellationToken);
        if (user == null)
            throw new InvalidOperationException($"User {id} not found");

        var updatedUser = user.RemoveRole(role);
        await _repository.UpdateAsync(updatedUser, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(updatedUser);
    }

    private static UserDto MapToDto(User user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        Name = user.Name,
        Surname = user.Surname,
        Email = user.Email,
        PhoneNumber = user.PhoneNumber?.Value,
        IsActive = user.IsActive,
        IsEmailConfirmed = user.IsEmailConfirmed,
        IsPhoneNumberConfirmed = user.IsPhoneNumberConfirmed,
        IsLockoutEnabled = user.IsLockoutEnabled,
        CreatedAt = user.CreatedAt,
        LastLoginAt = user.LastLoginAt,
        Roles = user.Roles
    };
}
