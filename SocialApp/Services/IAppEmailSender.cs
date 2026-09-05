namespace SocialApp.Services;

/// <summary>
/// Ek email ka poora content. Har mail ke do versions bhejte hain:
/// HTML (jo log dekhte hain) aur plain text (fallback + spam score behtar hota hai —
/// HTML-only mails ko spam filters shaqi samajhte hain).
/// </summary>
/// <param name="Subject">Inbox mein nazar aane wali subject line.</param>
/// <param name="HtmlBody">Rich version.</param>
/// <param name="PlainTextBody">Text-only version.</param>
public record EmailContent(string Subject, string HtmlBody, string PlainTextBody);

/// <summary>
/// Email bhejne ka abstraction.
///
/// Naam "IAppEmailSender" hai (sirf "IEmailSender" nahi) kyunke
/// Microsoft.AspNetCore.Identity mein bhi isi naam ka interface maujood hai —
/// alag naam se ambiguity nahi hoti.
///
/// Controller is interface par depend karta hai, SmtpEmailSender par nahi. Is liye
/// baad mein SendGrid/Brevo par shift karna ho to sirf Program.cs ki ek line badlegi.
/// </summary>
public interface IAppEmailSender
{
    Task SendAsync(
        string toEmail,
        string toName,
        EmailContent content,
        CancellationToken cancellationToken = default);
}
