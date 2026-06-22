namespace SaasTemplate.Api.Email;

public interface IEmailService
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);
    Task SendMagicLinkAsync(string toEmail, string magicLinkUrl, CancellationToken cancellationToken = default);
    Task SendPasswordResetAsync(string toEmail, string resetUrl, CancellationToken cancellationToken = default);
    Task SendEmailVerificationAsync(string toEmail, string verifyUrl, CancellationToken cancellationToken = default);
    Task SendWelcomeEmailAsync(string toEmail, string tier, CancellationToken cancellationToken = default);
    Task SendOnboardingEmailAsync(string toEmail, int stage, string tier, CancellationToken cancellationToken = default);
    Task SendPaymentFailedAsync(string toEmail, string billingPortalUrl, CancellationToken cancellationToken = default);
}
