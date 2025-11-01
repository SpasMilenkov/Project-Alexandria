using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Models;

namespace API.Features.Auth.Register;


public class RegisterEndpoint : Endpoint<RegisterRequest, RegisterResponse>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<RegisterEndpoint> _logger;

    public RegisterEndpoint(
        UserManager<ApplicationUser> userManager,
        ILogger<RegisterEndpoint> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("/api/auth/register");
        AllowAnonymous();
        // Add rate limiting if available
        Throttle(5, 60); // 1 request per 60 seconds per IP
    }

    public override async Task HandleAsync(RegisterRequest req, CancellationToken ct)
    {
        // Check if user already exists
        var existingUser = await _userManager.FindByEmailAsync(req.Email);
        if (existingUser != null)
        {
            await Send.ResponseAsync(new RegisterResponse
            {
                Success = false,
                Message = "User with this email already exists",
                Errors = new Dictionary<string, List<string>>
                {
                    ["Email"] = new List<string> { "Email is already registered" }
                }
            }, statusCode: 400, cancellation: ct);
            return;
        }

        // Create new user
        var user = new ApplicationUser
        {
            UserName = string.IsNullOrWhiteSpace(req.UserName)
                ? req.Email
                : req.UserName,
            Email = req.Email,
            EmailConfirmed = false,
            CreatedAt = DateTime.UtcNow,
            Name = req.Name
        };

        var result = await _userManager.CreateAsync(user, req.Password);

        if (!result.Succeeded)
        {
            var errors = new Dictionary<string, List<string>>();
            foreach (var error in result.Errors)
            {
                var key = error.Code.Contains("Password") ? "Password" : 
                         error.Code.Contains("Email") ? "Email" : 
                         error.Code.Contains("UserName") ? "UserName" : "General";
                
                if (!errors.ContainsKey(key))
                {
                    errors[key] = new List<string>();
                }
                errors[key].Add(error.Description);
            }

            await Send.ResponseAsync(new RegisterResponse
            {
                Success = false,
                Message = "Registration failed",
                Errors = errors
            }, statusCode: 400, cancellation: ct);
            return;
        }

        // Add default role
        try
        {
            await _userManager.AddToRoleAsync(user, "User");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to add default role to user {UserId}", user.Id);
            // Continue anyway - user is created
        }

        _logger.LogInformation("New user registered: {Email}", user.Email);

        // TODO: Send email confirmation in production
        // var emailToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        // await _emailService.SendConfirmationEmailAsync(user.Email, emailToken);

        await Send.ResponseAsync(new RegisterResponse
        {
            Success = true,
            UserId = user.Id,
            Message = "Registration successful"
        }, cancellation: ct);
    }
}