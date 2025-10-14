using blog.Helpers;
using blog.Models;
using blog.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;


namespace blog.Services
{
    public class AuthService
    {
        private readonly UserRepository _userRepository;
        private readonly JwtTokenHelper _jwtHelper;
        private readonly EmailService _emailService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            UserRepository userRepository,
            JwtTokenHelper jwtHelper,
            EmailService emailService,
            IHttpContextAccessor httpContextAccessor,
            IMemoryCache cache,
            ILogger<AuthService> logger)
        {
            _userRepository = userRepository;
            _jwtHelper = jwtHelper;
            _emailService = emailService;
            _httpContextAccessor = httpContextAccessor;
            _cache = cache;
            _logger = logger;
        }

        public async Task<LoginResponse> LoginAsync(blog.Models.LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                return new LoginResponse { Success = false, Message = "Email and password are required" };

            var normalizedEmail = request.Email.Trim().ToLower();

            // Cache user lookup for frequent logins
            var cacheKey = $"user_{normalizedEmail}";
            if (!_cache.TryGetValue(cacheKey, out User user))
            {
                user = _userRepository.GetUserByEmail(normalizedEmail);
                if (user != null)
                    _cache.Set(cacheKey, user, TimeSpan.FromMinutes(5));
            }

            if (user == null || !PasswordHelper.VerifyPassword(request.Password, user.PasswordHash))
                return new LoginResponse { Success = false, Message = "Invalid email or password" };

            var token = _jwtHelper.GenerateToken(user.UserId, user.Email);
            var expiresAt = DateTime.UtcNow.AddHours(1);

            // Fire all background tasks without awaiting - IMMEDIATE RESPONSE
            _ = Task.Run(async () =>
            {
                try
                {
                    await _userRepository.CreateSession(user.UserId, token, expiresAt);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to create session: {ex.Message}");
                }
            });

            _ = Task.Run(async () =>
            {
                try
                {
                    await _userRepository.UpdateLastLogin(user.UserId);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to update last login: {ex.Message}");
                }
            });

            _ = Task.Run(async () =>
            {
                try
                {
                    await SendLoginNotificationAsync(user);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to send login notification: {ex.Message}");
                }
            });

            // Return immediately with token
            return new LoginResponse
            {
                Success = true,
                Message = "Login successful",
                Token = token,
                User = new UserDto
                {
                    UserId = user.UserId,
                    Email = user.Email,
                    FullName = user.FullName
                }
            };
        }

        public async Task<LoginResponse> RegisterAsync(blog.Models.RegisterRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                return new LoginResponse { Success = false, Message = "Email and password are required" };

            var normalizedEmail = request.Email.Trim().ToLower();
            request.Email = normalizedEmail;

            var existingUser = _userRepository.GetUserByEmail(normalizedEmail);
            if (existingUser != null)
                return new LoginResponse { Success = false, Message = "User with this email already exists" };

            var success = _userRepository.CreateUser(request);
            if (!success)
                return new LoginResponse { Success = false, Message = "Failed to create user" };

            // Fire and forget welcome email - IMMEDIATE RESPONSE
            _ = Task.Run(async () =>
            {
                try
                {
                    await _emailService.SendWelcomeEmailAsync(request.FullName, request.Email, "Standard");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to send welcome email: {ex.Message}");
                }
            });

            // Return immediately
            return new LoginResponse { Success = true, Message = "Registration successful" };
        }

        public async Task<ForgotPasswordResponse> ForgotPasswordAsync(blog.Models.ForgotPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
                return new ForgotPasswordResponse { Success = false, Message = "Email is required" };

            var normalizedEmail = request.Email.Trim().ToLower();
            var user = _userRepository.GetUserByEmail(normalizedEmail);

            // Always return success for security
            if (user == null)
                return new ForgotPasswordResponse { Success = true, Message = "If the email exists, a password reset link has been sent" };

            var token = GenerateSecureToken();
            var success = _userRepository.CreatePasswordResetToken(user.UserId, token);

            if (!success)
                return new ForgotPasswordResponse { Success = false, Message = "Failed to create reset token" };

            // Fire and forget email - IMMEDIATE RESPONSE
            _ = Task.Run(async () =>
            {
                try
                {
                    await SendPasswordResetEmailAsync(user, token);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to send password reset email: {ex.Message}");
                }
            });

            // Return immediately
            return new ForgotPasswordResponse { Success = true, Message = "If the email exists, a password reset link has been sent" };
        }

        public async Task<ResetPasswordResponse> ResetPasswordAsync(blog.Models.ResetPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.NewPassword))
                return new ResetPasswordResponse { Success = false, Message = "Email, token and new password are required" };

            if (request.NewPassword.Length < 6)
                return new ResetPasswordResponse { Success = false, Message = "Password must be at least 6 characters long" };

            var normalizedEmail = request.Email.Trim().ToLower();

            var isValidToken = _userRepository.VerifyResetToken(normalizedEmail, request.Token);
            if (!isValidToken)
                return new ResetPasswordResponse { Success = false, Message = "Invalid or expired reset token" };

            var success = _userRepository.ResetPassword(normalizedEmail, request.Token, request.NewPassword);
            if (!success)
                return new ResetPasswordResponse { Success = false, Message = "Failed to reset password" };

            // Clear user cache
            _cache.Remove($"user_{normalizedEmail}");

            // Fire and forget confirmation email - IMMEDIATE RESPONSE
            _ = Task.Run(async () =>
            {
                try
                {
                    await SendPasswordResetConfirmationAsync(normalizedEmail);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to send password reset confirmation: {ex.Message}");
                }
            });

            // Return immediately
            return new ResetPasswordResponse { Success = true, Message = "Password reset successfully" };
        }

        public async Task<bool> VerifyResetTokenAsync(string email, string token)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
                return false;

            var normalizedEmail = email.Trim().ToLower();
            return _userRepository.VerifyResetToken(normalizedEmail, token);
        }

        private async Task SendLoginNotificationAsync(blog.Models.User user)
        {
            try
            {
                var clientIP = GetClientIPAddress();
                var userAgent = _httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].ToString() ?? "Unknown";

                await _emailService.SendLoginNotificationAsync(user.FullName, user.Email, clientIP, userAgent);
            }
            catch (Exception ex)
            {
                _logger.LogError($"⚠️ Failed to send login notification: {ex.Message}");
            }
        }


        private async Task SendPasswordResetEmailAsync(blog.Models.User user, string token)
        {
            try
            {
                var resetLink = $"https://cpre.netlify.app/reset-password?email={Uri.EscapeDataString(user.Email)}&token={token}";
                await _emailService.SendPasswordResetEmailAsync(user.FullName, user.Email, resetLink);
            }
            catch (Exception ex)
            {
                _logger.LogError($"⚠️ Failed to send reset email: {ex.Message}");
            }
        }

        private async Task SendPasswordResetConfirmationAsync(string email)
        {
            try
            {
                var user = _userRepository.GetUserByEmail(email);
                if (user != null)
                    await _emailService.SendPasswordResetConfirmationAsync(user.FullName, user.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError($"⚠️ Failed to send reset confirmation: {ex.Message}");
            }
        }

        private string GenerateSecureToken()
        {
            return $"{Guid.NewGuid():N}{Guid.NewGuid():N}";
        }

        private string GetClientIPAddress()
        {
            try
            {
                var context = _httpContextAccessor.HttpContext;
                if (context == null) return "Unknown";

                return context.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',').FirstOrDefault()?.Trim()
                    ?? context.Request.Headers["X-Real-IP"].FirstOrDefault()
                    ?? context.Connection.RemoteIpAddress?.ToString()
                    ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}