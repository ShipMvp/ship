using ShipMvp.Domain.Identity;
using ShipMvp.Core.Security;
using ShipMvp.Core.Persistence;
using Microsoft.Extensions.Logging;

namespace ShipMvp.Application.Identity;

// Authentication Service - OpenIddict handles token generation
public class AuthService : IAuthService
{
    private readonly IUserRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository repository, 
        IUnitOfWork unitOfWork, 
        IPasswordHasher passwordHasher, 
        ILogger<AuthService> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<AuthResultDto> LoginAsync(LoginDto request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.ToLowerInvariant();
        _logger.LogDebug("AuthService: Attempting login for email: {Email}", normalizedEmail);
        
        // Find user by email (normalized to lowercase)
        var user = await _repository.GetByEmailAsync(normalizedEmail, cancellationToken);
        _logger.LogDebug("AuthService: User found by email: {Found}", user != null);

        if (user == null || !user.IsActive)
        {
            _logger.LogWarning("AuthService: Login failed - user not found or inactive. Email: {Email}, User found: {Found}, IsActive: {IsActive}", 
                normalizedEmail, user != null, user?.IsActive);
            return new AuthResultDto
            {
                Success = false,
                ErrorMessage = "Invalid email or password"
            };
        }

        // Verify password
        if (!_passwordHasher.VerifyPassword(user.PasswordHash, request.Password))
        {
            _logger.LogWarning("AuthService: Login failed - invalid password for user: {Email}", normalizedEmail);
            return new AuthResultDto
            {
                Success = false,
                ErrorMessage = "Invalid email or password"
            };
        }

        _logger.LogDebug("AuthService: Password verified successfully for user: {Email}", normalizedEmail);

        var updatedUser = user.RecordLogin();
        await _repository.UpdateAsync(updatedUser, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Token generation is handled by OpenIddict /connect/token endpoint
        return new AuthResultDto
        {
            Success = true,
            User = MapToDto(updatedUser)
        };
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
