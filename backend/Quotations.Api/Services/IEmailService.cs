namespace Quotations.Api.Services;

public interface IEmailService
{
    Task SendEmailVerificationAsync(string toEmail, string displayName, string verifyLink);
    Task SendPasswordResetAsync(string toEmail, string displayName, string resetLink);
}
