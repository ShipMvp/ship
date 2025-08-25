using System.ComponentModel.DataAnnotations;

namespace ShipMvp.Application.Identity;

// Application DTOs
public record UserDto
{
    public Guid Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Surname { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
    public bool IsActive { get; init; }
    public bool IsEmailConfirmed { get; init; }
    public bool IsPhoneNumberConfirmed { get; init; }
    public bool IsLockoutEnabled { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? LastLoginAt { get; init; }
    public List<string> Roles { get; init; } = new();
}

public record CreateUserDto
{
    [Required]
    [StringLength(50, MinimumLength = 3)]
    public string Username { get; init; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Surname { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 6)]
    public string Password { get; init; } = string.Empty;

    [Phone]
    public string? PhoneNumber { get; init; }

    public bool IsActive { get; init; } = true;
}

public record UpdateUserDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Surname { get; init; } = string.Empty;

    [Phone]
    public string? PhoneNumber { get; init; }

    public bool IsActive { get; init; }
    public bool IsEmailConfirmed { get; init; }
    public bool IsPhoneNumberConfirmed { get; init; }
}

public record LoginDto
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;

    public bool RememberMe { get; init; }
}

public record AuthResultDto
{
    public bool Success { get; init; }
    public UserDto? User { get; init; }
    public string? ErrorMessage { get; init; }
}

public record GetUsersQuery
{
    public string? SearchText { get; init; }
    public string? Role { get; init; }
    public bool? IsActive { get; init; }
    public int PageSize { get; init; } = 10;
    public int PageNumber { get; init; } = 1;
}