using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Quotations.Api.Configuration;

namespace Quotations.Api.Services;

public class ResendEmailService : IEmailService
{
    private readonly HttpClient _http;
    private readonly ResendSettings _settings;
    private readonly ILogger<ResendEmailService> _logger;

    public ResendEmailService(HttpClient http, IOptions<ResendSettings> settings, ILogger<ResendEmailService> logger)
    {
        _http = http;
        _settings = settings.Value;
        _logger = logger;
        _http.BaseAddress = new Uri("https://api.resend.com/");
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
    }

    public async Task SendEmailVerificationAsync(string toEmail, string displayName, string verifyLink)
    {
        var html = $"""
            <div style="font-family:sans-serif;max-width:480px;margin:0 auto">
              <h2 style="color:#212529">Verify your email address</h2>
              <p>Hi {HtmlEncode(displayName)},</p>
              <p>Thanks for creating an account. Click the button below to verify your email address.</p>
              <p style="margin:2rem 0">
                <a href="{verifyLink}"
                   style="background:#212529;color:#fff;padding:0.6rem 1.4rem;border-radius:4px;text-decoration:none;font-size:0.95rem">
                  Verify Email
                </a>
              </p>
              <p style="color:#6c757d;font-size:0.85rem">This link expires in 24 hours. If you didn't create this account you can safely ignore this email.</p>
            </div>
            """;

        await SendAsync(toEmail, "Verify your email address", html);
    }

    public async Task SendPasswordResetAsync(string toEmail, string displayName, string resetLink)
    {
        var html = $"""
            <div style="font-family:sans-serif;max-width:480px;margin:0 auto">
              <h2 style="color:#212529">Reset your password</h2>
              <p>Hi {HtmlEncode(displayName)},</p>
              <p>We received a request to reset the password for your account. Click the button below to choose a new password.</p>
              <p style="margin:2rem 0">
                <a href="{resetLink}"
                   style="background:#212529;color:#fff;padding:0.6rem 1.4rem;border-radius:4px;text-decoration:none;font-size:0.95rem">
                  Reset Password
                </a>
              </p>
              <p style="color:#6c757d;font-size:0.85rem">This link expires in 1 hour. If you didn't request a password reset you can safely ignore this email — your password has not been changed.</p>
            </div>
            """;

        await SendAsync(toEmail, "Reset your password", html);
    }

    private async Task SendAsync(string toEmail, string subject, string html)
    {
        var payload = new
        {
            from = $"{_settings.FromName} <{_settings.FromEmail}>",
            to = new[] { toEmail },
            subject,
            html
        };

        var body = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await _http.PostAsync("emails", body);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Resend API error {Status} sending to {Email}: {Error}",
                response.StatusCode, toEmail, error);
            throw new InvalidOperationException($"Failed to send email: {response.StatusCode}");
        }
    }

    private static string HtmlEncode(string value) =>
        System.Net.WebUtility.HtmlEncode(value);
}
