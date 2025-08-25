using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ShipMvp.Application.Identity;
using ShipMvp.Application.Email;
using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;

namespace ShipMvp.Api.Controllers;

/// <summary>
/// Authentication endpoints for user management operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[AllowAnonymous]
public class AuthController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IEmailApplicationService _emailService;
    private readonly ILogger<AuthController> _logger;

    /// <summary>
    /// Initializes a new instance of the AuthController
    /// </summary>
    /// <param name="userService">User service</param>
    /// <param name="emailService">Email service</param>
    /// <param name="logger">Logger instance</param>
    public AuthController(
        IUserService userService,
        IEmailApplicationService emailService,
        ILogger<AuthController> logger)
    {
        _userService = userService;
        _emailService = emailService;
        _logger = logger;
    }

    /// <summary>
    /// Register a new user account
    /// </summary>
    /// <param name="request">User registration data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Registration result with user info</returns>
    [AllowAnonymous]
    [HttpPost("register")]
    [SwaggerOperation(
        Summary = "User registration",
        Description = "Register a new user account and send a confirmation email"
    )]
    [SwaggerResponse(201, "Registration successful", typeof(RegisterResultDto))]
    [SwaggerResponse(400, "Invalid registration data")]
    [SwaggerResponse(409, "Username or email already exists")]
    public async Task<ActionResult<RegisterResultDto>> RegisterAsync(
        [FromBody] RegisterDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Registration attempt for user: {Username}", request.Username);

            // Create the user
            var createUserRequest = new CreateUserDto
            {
                Username = request.Username,
                Name = request.Name,
                Surname = request.Surname,
                Email = request.Email,
                Password = request.Password,
                PhoneNumber = request.PhoneNumber,
                IsActive = false // User starts inactive until email is confirmed
            };

            var user = await _userService.CreateAsync(createUserRequest, cancellationToken);

            // Generate email confirmation token (simplified for this demo)
            var confirmationToken = Guid.NewGuid().ToString("N");

            // Send signup confirmation email
            var emailResult = await _emailService.SendSignupConfirmationEmailAsync(
                user.Id,
                user.Email,
                $"{user.Name} {user.Surname}",
                confirmationToken,
                cancellationToken);

            var result = new RegisterResultDto
            {
                Success = true,
                UserId = user.Id.ToString(),
                Username = user.Username,
                Email = user.Email,
                Message = "Registration successful. Please check your email to confirm your account.",
                EmailSent = emailResult.IsSuccess
            };

            if (!emailResult.IsSuccess)
            {
                _logger.LogWarning("User registered but email sending failed: {UserId}, Email error: {EmailError}",
                    user.Id, emailResult.ErrorMessage);
                result.Message += " Note: There was an issue sending the confirmation email. Please contact support.";
            }

            _logger.LogInformation("User registered successfully: {Username}, Email sent: {EmailSent}",
                request.Username, emailResult.IsSuccess);

            return CreatedAtAction(nameof(GetUserStatus), new { userId = user.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Registration failed for user: {Username}. Reason: {Reason}", request.Username, ex.Message);
            return Conflict(new RegisterResultDto
            {
                Success = false,
                ErrorMessage = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registration failed for user: {Username}. Error: {ErrorMessage}", request.Username, ex.Message);
            return BadRequest(new RegisterResultDto
            {
                Success = false,
                ErrorMessage = "An error occurred during registration"
            });
        }
    }

    /// <summary>
    /// Get user registration status
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>User status information</returns>
    [HttpGet("status/{userId}")]
    [SwaggerOperation(
        Summary = "Get user status",
        Description = "Get user registration and email confirmation status"
    )]
    [SwaggerResponse(200, "User status retrieved successfully", typeof(UserStatusDto))]
    [SwaggerResponse(404, "User not found")]
    public async Task<ActionResult<UserStatusDto>> GetUserStatus(
        [FromRoute] string userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _userService.GetByIdAsync(Guid.Parse(userId), cancellationToken);
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            return Ok(new UserStatusDto
            {
                UserId = user.Id.ToString(),
                Username = user.Username,
                Email = user.Email,
                IsActive = user.IsActive,
                EmailConfirmed = user.IsActive // Simplified: using IsActive as email confirmation status
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user status for ID: {UserId}", userId);
            return BadRequest(new { message = "An error occurred while getting user status" });
        }
    }

    /// <summary>
    /// DTO for user registration result
    /// </summary>
    public class RegisterResultDto
    {
        /// <summary>
        /// Indicates if the registration was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// User ID of the registered user
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Username of the registered user
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Email of the registered user
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Confirmation message or error message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Indicates if the confirmation email was sent successfully
        /// </summary>
        public bool EmailSent { get; set; }

        /// <summary>
        /// Error message if registration failed
        /// </summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// DTO for user status information
    /// </summary>
    public class UserStatusDto
    {
        /// <summary>
        /// User ID
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Username
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Email address
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Indicates if the user account is active
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Indicates if the email is confirmed
        /// </summary>
        public bool EmailConfirmed { get; set; }
    }

    /// <summary>
    /// DTO for user registration
    /// </summary>
    public record RegisterDto
    {
        /// <summary>
        /// Username for the new account
        /// </summary>
        [Required]
        [StringLength(50, MinimumLength = 3)]
        public string Username { get; init; } = string.Empty;

        /// <summary>
        /// User's first name
        /// </summary>
        [Required]
        [StringLength(100)]
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// User's last name
        /// </summary>
        [Required]
        [StringLength(100)]
        public string Surname { get; init; } = string.Empty;

        /// <summary>
        /// Email address
        /// </summary>
        [Required]
        [EmailAddress]
        public string Email { get; init; } = string.Empty;

        /// <summary>
        /// Password for the new account
        /// </summary>
        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string Password { get; init; } = string.Empty;

        /// <summary>
        /// Phone number (optional)
        /// </summary>
        [Phone]
        public string? PhoneNumber { get; init; }
    }
}
