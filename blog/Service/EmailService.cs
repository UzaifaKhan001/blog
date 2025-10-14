using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json;

public class EmailService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _fromEmail;
    private readonly string _fromName;

    public EmailService(
        IConfiguration configuration,
        ILogger<EmailService> logger,
        IHttpContextAccessor httpContextAccessor,
        HttpClient httpClient)
    {
        _configuration = configuration;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _httpClient = httpClient;

        _fromEmail = _configuration["SmtpSettings:Username"] ?? Environment.GetEnvironmentVariable("SMTP_USERNAME");
        _fromName = "UG Crypto Trading";
    }

    public async Task<bool> SendEmailAsync(string recipientEmail, string subject, string htmlBody)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            _logger.LogWarning("Email sending failed: Recipient email is empty");
            return false;
        }

        var success = await TrySendViaSmtpAsync(recipientEmail, subject, htmlBody);

        if (success)
            _logger.LogInformation($"✅ Email sent successfully to {recipientEmail}");
        else
            _logger.LogError($"❌ Failed to send email to {recipientEmail}");

        return success;
    }

    private async Task<bool> TrySendViaSmtpAsync(string recipientEmail, string subject, string htmlBody)
    {
        var maxRetries = 3;
        var retryDelay = 2000;

        string host = _configuration["SmtpSettings:Host"] ?? Environment.GetEnvironmentVariable("SMTP_HOST");
        int port = int.Parse(_configuration["SmtpSettings:Port"] ?? Environment.GetEnvironmentVariable("SMTP_PORT") ?? "587");
        string username = _configuration["SmtpSettings:Username"] ?? Environment.GetEnvironmentVariable("SMTP_USERNAME");
        string password = _configuration["SmtpSettings:Password"] ?? Environment.GetEnvironmentVariable("SMTP_PASSWORD");
        bool enableSSL = bool.Parse(_configuration["SmtpSettings:EnableSSL"] ?? Environment.GetEnvironmentVariable("SMTP_SSL") ?? "true");

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation($"[SMTP Attempt {attempt}/{maxRetries}] Sending to {recipientEmail}");

                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(username, _fromName),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true,
                    Priority = MailPriority.Normal
                };
                mailMessage.To.Add(recipientEmail);

                using var smtpClient = new SmtpClient(host, port)
                {
                    Credentials = new NetworkCredential(username, password),
                    EnableSsl = enableSSL,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Timeout = 30000
                };

                await Task.Run(() => smtpClient.Send(mailMessage));
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ [Attempt {attempt}] SMTP error: {ex.Message}");

                if (attempt < maxRetries)
                {
                    _logger.LogInformation($"Retrying in {retryDelay / 1000}s...");
                    await Task.Delay(retryDelay);
                }
                else
                {
                    return false;
                }
            }
        }

        return false;
    }
    private bool IsGmailBlockedError(SmtpException ex)
    {
        return ex.Message.Contains("blocked", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("not accepted", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("authentication", StringComparison.OrdinalIgnoreCase) ||
               ex.StatusCode == SmtpStatusCode.ClientNotPermitted;
    }

    // ✅ Keep your existing template + helper methods below (unchanged)
    private string GetEmailTemplate(string title, string content, string buttonText = "", string buttonUrl = "")
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; margin: 0; padding: 0; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0; }}
        .content {{ background: #f9f9f9; padding: 30px; border-radius: 0 0 10px 10px; }}
        .button {{ display: inline-block; padding: 12px 30px; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
        .footer {{ text-align: center; margin-top: 20px; font-size: 12px; color: #666; }}
        .info-box {{ background: white; padding: 15px; border-left: 4px solid #667eea; margin: 15px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🚀 UG Crypto Trading</h1>
            <h2>{title}</h2>
        </div>
        <div class='content'>
            {content}
            {(string.IsNullOrEmpty(buttonText) ? "" : $@"<div style='text-align: center;'><a href='{buttonUrl}' class='button'>{buttonText}</a></div>")}
            <div class='footer'>
                <p>If you have any questions, contact our support team.</p>
                <p>© {DateTime.Now.Year} UG Crypto Trading. All rights reserved.</p>
            </div>
        </div>
    </div>
</body>
</html>";
    }
    public Task<bool> SendWelcomeEmailAsync(string fullName, string email, string plan)
    {
        string subject = "🎉 Welcome to UG Crypto Trading!";
        string content = $@"
            <h3>Dear {fullName ?? "User"},</h3>
            <p>Welcome to UG Crypto Trading! We're excited to have you on board.</p>
            
            <div class='info-box'>
                <strong>Your Account Details:</strong><br/>
                <strong>Plan:</strong> {plan}<br/>
                <strong>Email:</strong> {email}<br/>
                <strong>Joined:</strong> {DateTime.Now:MMMM dd, yyyy}
            </div>
            
            <p>With your account, you can:</p>
            <ul>
                <li>Trade multiple cryptocurrencies</li>
                <li>Access real-time market data</li>
                <li>Use advanced trading tools</li>
                <li>Monitor your portfolio performance</li>
            </ul>
            
            <p><strong>Get started by exploring our platform features!</strong></p>";

        string body = GetEmailTemplate("Welcome Aboard!", content, "Start Trading", "https://cpre.netlify.app/dashboard");
        return SendEmailAsync(email, subject, body);
    }

    public async Task<bool> SendLoginNotificationAsync(string fullName, string email, string ipAddress, string userAgent)
    {
        string location = await GetIPLocationAsync(ipAddress);
        DateTime utcTime = DateTime.UtcNow;
        string timeString = utcTime.ToString("yyyy-MM-dd hh:mm tt") + " UTC";

        string subject = "🔔 Login Notification";
        string content = $@"
        <h3>Hello {fullName},</h3>
        <p>Your account was just accessed.</p>
        
        <div class='info-box'>
            <strong>Login Details:</strong><br/>
            <strong>IP Address:</strong> {ipAddress}<br/>
            <strong>Location:</strong> {location}<br/>
            <strong>Time:</strong> {timeString}<br/>
            <strong>Device/Browser:</strong> {userAgent}
        </div>
        
        <p>If this wasn't you, please reset your password immediately.</p>";

        string body = GetEmailTemplate("Security Notification", content);
        return await SendEmailAsync(email, subject, body);
    }

    private async Task<string> GetIPLocationAsync(string ip)
    {
        if (ip == "Unknown IP" || ip == "::1" || ip == "127.0.0.1")
            return "Localhost / Unknown Location";

        try
        {
            var response = await _httpClient.GetStringAsync($"https://ipapi.co/{ip}/json/");
            var data = JsonDocument.Parse(response).RootElement;

            string city = data.GetProperty("city").GetString();
            string region = data.GetProperty("region").GetString();
            string country = data.GetProperty("country_name").GetString();

            return $"{city}, {region}, {country}";
        }
        catch
        {
            return "Unknown Location";
        }
    }

    public Task<bool> SendPasswordResetConfirmationAsync(string fullName, string email)
    {
        string subject = "✅ Password Reset Successful";
        string content = $@"
            <h3>Hello {fullName ?? "User"},</h3>
            <p>Your UG Crypto Trading account password has been successfully reset.</p>
            
            <div class='info-box'>
                <strong>Reset Time:</strong> {DateTime.Now:MMMM dd, yyyy 'at' HH:mm:ss 'UTC'}<br/>
                <strong>Account:</strong> {email}
            </div>
            
            <p><strong>Security Notice:</strong></p>
            <p>If you did not perform this action, please contact our support team immediately.</p>";

        string body = GetEmailTemplate("Password Reset Confirmation", content, "Login to Your Account", "https://cpre.netlify.app/login");
        return SendEmailAsync(email, subject, body);
    }

    public Task<bool> SendPasswordResetEmailAsync(string fullName, string email, string resetLink)
    {
        string subject = "🔑 Password Reset Request";
        string content = $@"
            <h3>Hello {fullName ?? "User"},</h3>
            <p>We received a request to reset your password for your UG Crypto Trading account.</p>
            
            <div class='info-box'>
                <strong>Request Time:</strong> {DateTime.Now:MMMM dd, yyyy 'at' HH:mm:ss 'UTC'}<br/>
                <strong>Account:</strong> {email}
            </div>
            
            <p>Click the button below to reset your password. This link will expire in 1 hour.</p>
            
            <p><strong>Didn't request this?</strong><br/>
            If you didn't request a password reset, please ignore this email.</p>";

        string body = GetEmailTemplate("Password Reset", content, "Reset Your Password", resetLink);
        return SendEmailAsync(email, subject, body);
    }
    public Task<bool> SendEmailChangeNotificationAsync(string fullName, string oldEmail, string newEmail)
    {
        string subject = "✉️ Email Address Changed Successfully";

        string content = $@"
        <h3>Hello {fullName ?? "User"},</h3>
        <p>This is to inform you that your UG Crypto Trading account email has been changed.</p>
        
        <div class='info-box'>
            <strong>Previous Email:</strong> {oldEmail}<br/>
            <strong>New Email:</strong> {newEmail}<br/>
            <strong>Change Date:</strong> {DateTime.UtcNow:MMMM dd, yyyy 'at' HH:mm:ss 'UTC'}
        </div>

        <p>If you did not make this change, please contact our support team immediately to secure your account.</p>";

        string body = GetEmailTemplate("Email Address Changed", content, "Go to Dashboard", "https://cpre.netlify.app/dashboard");
        return SendEmailAsync(newEmail, subject, body);
    }

}