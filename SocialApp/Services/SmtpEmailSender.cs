using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace SocialApp.Services;

/// <summary>
/// MailKit ke zariye asli SMTP par email bhejta hai.
///
/// MailKit kyun, System.Net.Mail.SmtpClient kyun nahi? Microsoft ne khud
/// SmtpClient ko "obsolete for new development" mark kiya hai — uska
/// STARTTLS handling aur async support kamzor hai. MailKit Microsoft ki
/// hi recommended replacement hai.
/// </summary>
public class SmtpEmailSender : IAppEmailSender
{
    private readonly SmtpSettings _settings;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<SmtpSettings> settings, ILogger<SmtpEmailSender> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendAsync(
        string toEmail,
        string toName,
        EmailContent content,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.IsConfigured)
        {
            throw new InvalidOperationException(
                "SMTP settings adhoori hain. Ye chalao: " +
                "dotnet user-secrets set \"Smtp:UserName\" \"you@gmail.com\" — " +
                "dotnet user-secrets set \"Smtp:Password\" \"<16-char app password>\" — " +
                "dotnet user-secrets set \"Smtp:FromEmail\" \"you@gmail.com\"");
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = content.Subject;

        // BodyBuilder multipart/alternative banata hai: client jo support karta ho
        // woh chun leta hai (HTML), warna plain text par gir jata hai.
        var body = new BodyBuilder
        {
            HtmlBody = content.HtmlBody,
            TextBody = content.PlainTextBody
        };
        message.Body = body.ToMessageBody();

        // MailKit ka SmtpClient IDisposable hai — har mail ke liye naya banao,
        // ye thread-safe nahi hai.
        using var client = new SmtpClient();

        var security = _settings.UseStartTls
            ? SecureSocketOptions.StartTls      // port 587: plain connect, phir TLS upgrade
            : SecureSocketOptions.SslOnConnect; // port 465: shuru se hi TLS

        await client.ConnectAsync(_settings.Host, _settings.Port, security, cancellationToken);

        // AuthUserName / AuthPassword — raw values nahi. AuthPassword copy-paste
        // se aayi spaces hata deta hai; Gmail unpar 535 BadCredentials deta hai.
        await client.AuthenticateAsync(_settings.AuthUserName, _settings.AuthPassword, cancellationToken);
        await client.SendAsync(message, cancellationToken);

        // quit: true — server ko saaf QUIT bhejo, socket beech mein na toro.
        await client.DisconnectAsync(quit: true, cancellationToken);

        _logger.LogInformation("Email bhej diya {Email} ko — {Subject}", toEmail, content.Subject);
    }
}
