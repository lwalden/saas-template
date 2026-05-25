using SaasTemplate.Api.Auth;
using Resend;

namespace SaasTemplate.Api.Email;

public sealed class ResendEmailService : IEmailService
{
    // TODO: Replace these with your product's brand identity
    private const string FromAddress = "SaasTemplate <noreply@example.com>";
    private const string SupportEmail = "support@example.com";
    private const string ProductName = "SaasTemplate";

    private readonly IResend _resend;
    private readonly ILogger<ResendEmailService> _logger;
    private readonly JwtSettings _jwtSettings;
    private readonly AppSettings _appSettings;

    public ResendEmailService(IResend resend, AppSettings appSettings, ILogger<ResendEmailService> logger, JwtSettings jwtSettings)
    {
        _resend = resend;
        _logger = logger;
        _jwtSettings = jwtSettings;
        _appSettings = appSettings;
    }

    private string EmailHeader() =>
        $"""
        <div style="text-align:center;padding:24px 0 16px;">
          <strong style="font-size:1.25rem;">{ProductName}</strong>
        </div>
        """;

    private string UnsubscribeFooter(string email, bool isSubscriber)
    {
        var token = UnsubscribeToken.Generate(email, _jwtSettings.Secret);
        var encodedEmail = Uri.EscapeDataString(email);
        var url = $"{_appSettings.BaseUrl}/unsubscribe?email={encodedEmail}&token={token}";
        var reason = isSubscriber
            ? $"You are receiving this as part of your {ProductName} subscription."
            : $"You are receiving this because you signed up at {ProductName}.";
        return $"""
            <div style="margin-top:32px;padding-top:16px;border-top:1px solid #e5e7eb;font-size:0.75rem;color:#6b7280;text-align:center;">
              {reason}
              <br>
              <a href="{url}" style="color:#6b7280;text-decoration:underline;">Unsubscribe</a>
              &nbsp;&middot;&nbsp;
              <a href="mailto:{SupportEmail}" style="color:#6b7280;text-decoration:underline;">Contact support</a>
              &nbsp;&middot;&nbsp;
              <a href="{_appSettings.FrontendUrl}/privacy.html" style="color:#6b7280;text-decoration:underline;">Privacy Policy</a>
            </div>
            """;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var message = new EmailMessage
        {
            From = FromAddress,
            ReplyTo = [SupportEmail],
            To = [toEmail],
            Subject = subject,
            HtmlBody = htmlBody
        };
        var response = await _resend.EmailSendAsync(message, cancellationToken);
        ThrowIfFailed(response, "generic", toEmail);
        _logger.LogInformation("Email sent to {Email}, subject={Subject}, Resend ID: {Id}", toEmail, subject, response.Content);
    }

    public async Task SendMagicLinkAsync(string toEmail, string magicLinkUrl, CancellationToken cancellationToken = default)
    {
        var message = new EmailMessage
        {
            From = FromAddress,
            To = [toEmail],
            Subject = $"Sign in to {ProductName}",
            HtmlBody = $"""
                <div style="font-family:sans-serif;max-width:600px;margin:0 auto;padding:24px">
                  {EmailHeader()}
                  <h2 style="color:#1e293b">Sign in to {ProductName}</h2>
                  <p>Click the link below to sign in to your account. This link expires in 15 minutes.</p>
                  <p><a href="{System.Web.HttpUtility.HtmlAttributeEncode(magicLinkUrl)}" style="display:inline-block;padding:12px 24px;background:#0069D1;color:#fff;text-decoration:none;border-radius:6px;font-weight:bold;">Sign In</a></p>
                  <p style="color:#666;font-size:0.875rem;">If you did not request this link, you can safely ignore this email.</p>
                </div>
                """
        };
        var response = await _resend.EmailSendAsync(message, cancellationToken);
        ThrowIfFailed(response, "magic-link", toEmail);
        _logger.LogInformation("Magic link email sent to {Email}, Resend ID: {Id}", toEmail, response.Content);
    }

    public async Task SendWelcomeEmailAsync(string toEmail, string tier, CancellationToken cancellationToken = default)
    {
        await SendOnboardingEmailAsync(toEmail, 1, tier, cancellationToken);
    }

    public async Task SendOnboardingEmailAsync(string toEmail, int stage, string tier, CancellationToken cancellationToken = default)
    {
        string subject;
        string body;
        switch (stage)
        {
            case 1:
                subject = $"Welcome to {ProductName}";
                body = $"""
                    <div style="font-family:sans-serif;max-width:600px;margin:0 auto;padding:24px">
                      {EmailHeader()}
                      <h2 style="color:#1e293b">Welcome to {ProductName}!</h2>
                      <p>Your <strong>{System.Web.HttpUtility.HtmlEncode(tier)}</strong> subscription is active.</p>
                      <p><a href="{_appSettings.BaseUrl}"
                            style="display:inline-block;padding:12px 24px;background:#0069D1;color:#fff;text-decoration:none;border-radius:6px;font-weight:bold;">
                        Open Your Dashboard
                      </a></p>
                      <p style="color:#475569;font-size:0.875rem;margin-top:16px">If you have any questions, reply to this email.</p>
                      {UnsubscribeFooter(toEmail, isSubscriber: true)}
                    </div>
                    """;
                break;
            case 2:
                subject = "Tips for getting the most from your subscription";
                body = $"""
                    <div style="font-family:sans-serif;max-width:600px;margin:0 auto;padding:24px">
                      {EmailHeader()}
                      <h2 style="color:#1e293b">Getting the most from {ProductName}</h2>
                      <p>Here are some tips to help you get started with your {System.Web.HttpUtility.HtmlEncode(tier)} plan.</p>
                      <p><a href="{_appSettings.BaseUrl}"
                            style="display:inline-block;padding:12px 24px;background:#0069D1;color:#fff;text-decoration:none;border-radius:6px;font-weight:bold;">
                        Check Your Dashboard
                      </a></p>
                      {UnsubscribeFooter(toEmail, isSubscriber: true)}
                    </div>
                    """;
                break;
            case 3:
                subject = "One week in - how's it going?";
                body = $"""
                    <div style="font-family:sans-serif;max-width:600px;margin:0 auto;padding:24px">
                      {EmailHeader()}
                      <h2 style="color:#1e293b">One week in</h2>
                      <p>You have been using {ProductName} for a week. We would love to hear how it is going.</p>
                      <p><a href="{_appSettings.BaseUrl}"
                            style="display:inline-block;padding:12px 24px;background:#0069D1;color:#fff;text-decoration:none;border-radius:6px;font-weight:bold;">
                        Open Your Dashboard
                      </a></p>
                      {UnsubscribeFooter(toEmail, isSubscriber: true)}
                    </div>
                    """;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(stage), $"Unknown onboarding stage: {stage}");
        }

        var message = new EmailMessage
        {
            From = FromAddress,
            ReplyTo = [SupportEmail],
            To = [toEmail],
            Subject = subject,
            HtmlBody = body
        };
        var response = await _resend.EmailSendAsync(message, cancellationToken);
        ThrowIfFailed(response, $"onboarding-{stage}", toEmail);
        _logger.LogInformation("Onboarding email stage {Stage} sent to {Email}, Resend ID: {Id}", stage, toEmail, response.Content);
    }

    public async Task SendPaymentFailedAsync(string toEmail, string billingPortalUrl, CancellationToken cancellationToken = default)
    {
        var message = new EmailMessage
        {
            From = FromAddress,
            ReplyTo = [SupportEmail],
            To = [toEmail],
            Subject = $"Action required: update your payment method - {ProductName}",
            HtmlBody = $"""
                <div style="font-family:sans-serif;max-width:600px;margin:0 auto;padding:24px">
                  {EmailHeader()}
                  <h2 style="color:#1e293b">Payment failed</h2>
                  <p>We were unable to process your most recent payment. Your subscription is now past due.</p>
                  <p>Update your payment method to restore your subscription:</p>
                  <p><a href="{System.Web.HttpUtility.HtmlEncode(billingPortalUrl)}"
                        style="display:inline-block;padding:12px 24px;background:#0069D1;color:#fff;text-decoration:none;border-radius:6px;font-weight:bold;">
                    Update Payment Method
                  </a></p>
                  <p style="font-size:0.8125rem;color:#64748b;margin-top:16px;">If you believe this is an error, please contact us at {SupportEmail}.</p>
                  {UnsubscribeFooter(toEmail, isSubscriber: true)}
                </div>
                """
        };
        var response = await _resend.EmailSendAsync(message, cancellationToken);
        ThrowIfFailed(response, "payment-failed", toEmail);
        _logger.LogInformation("Payment failed dunning email sent to {Email}, Resend ID: {Id}", toEmail, response.Content);
    }

    private void ThrowIfFailed(ResendResponse<Guid> response, string emailType, string toEmail)
    {
        if (response.Success) return;
        var ex = response.Exception;
        _logger.LogError(
            "Resend: {EmailType} email FAILED for {Email}. ErrorType={ErrorType}, HttpStatus={StatusCode}, Message={Message}.",
            emailType, toEmail, ex?.ErrorType, ex?.StatusCode, ex?.Message);
        throw new InvalidOperationException($"Email delivery failed ({ex?.ErrorType}): {ex?.Message}");
    }
}
