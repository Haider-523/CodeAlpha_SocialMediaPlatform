using System.Net;

namespace SocialApp.Services;

/// <summary>
/// SocialApp ke transactional emails ka markup.
///
/// Email HTML web HTML nahi hai. Gmail/Outlook 2007+ modern CSS ka bara hissa
/// gira dete hain, is liye yahan jaan-boojh kar purane tareeqe use kiye hain:
///   • saara CSS inline (external/&lt;style&gt; blocks strip ho jate hain)
///   • layout &lt;table&gt; se, flexbox/grid se nahi (Outlook Word engine par render karta hai)
///   • system font stack — Plus Jakarta Sans email clients mein load nahi hoti
///   • max-width 560px, phone par comfortable
/// Colors wahi hain jo site.css ke light-theme tokens hain, take mail aur app ek lagen.
/// </summary>
public static class EmailTemplates
{
    // site.css ke light tokens ki copy — dono ko manually sync rakhna hai,
    // kyunke email CSS variables support nahi karti.
    private const string Accent = "#4F5DF5";
    private const string Ink = "#191C24";
    private const string Muted = "#5A6272";
    private const string Faint = "#7A828F";
    private const string Border = "#E4E7EE";
    private const string Bg = "#F7F8FA";
    private const string Surface = "#FFFFFF";

    private const string Font =
        "-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif";

    /// <summary>Register ke foran baad jane wala email — is mein confirmation link hota hai.</summary>
    public static EmailContent Confirmation(string displayName, string confirmUrl)
    {
        // Naam user ka apna input hai — HTML mein daalne se pehle encode karo.
        var name = HtmlEncode(displayName);
        var url = HtmlEncode(confirmUrl);

        var inner = $"""
                    <h1 style="margin:0 0 10px;font:700 22px/1.3 {Font};color:{Ink};letter-spacing:-0.02em;">Confirm your email</h1>
                    <p style="margin:0 0 22px;font:400 15px/1.65 {Font};color:{Muted};">
                      Hi {name} — you're one tap away. Confirm this address to activate your SocialApp account.
                    </p>

                    <table role="presentation" cellpadding="0" cellspacing="0" border="0" style="margin:0 0 24px;">
                      <tr>
                        <td style="border-radius:999px;background:{Accent};">
                          <a href="{url}" style="display:inline-block;padding:14px 28px;font:600 15px/1 {Font};color:#FFFFFF;text-decoration:none;border-radius:999px;">Confirm email</a>
                        </td>
                      </tr>
                    </table>

                    <p style="margin:0 0 6px;font:400 13px/1.6 {Font};color:{Faint};">Or paste this link into your browser:</p>
                    <p style="margin:0 0 24px;font:400 13px/1.6 {Font};word-break:break-all;">
                      <a href="{url}" style="color:{Accent};">{url}</a>
                    </p>

                    <div style="height:1px;background:{Border};margin:0 0 18px;"></div>

                    <p style="margin:0;font:400 13px/1.6 {Font};color:{Faint};">
                      This link works once and expires in 24 hours. If you didn't create a SocialApp account, you can ignore this email — nothing will happen.
                    </p>
                    """;

        var text = $"""
                    Confirm your email

                    Hi {displayName} — you're one tap away. Confirm this address to activate
                    your SocialApp account by opening the link below:

                    {confirmUrl}

                    This link works once and expires in 24 hours. If you didn't create a
                    SocialApp account, you can ignore this email.
                    """;

        return new EmailContent(
            Subject: "Confirm your SocialApp email",
            HtmlBody: Shell("Confirm your email to activate your SocialApp account.", inner),
            PlainTextBody: text);
    }

    /// <summary>
    /// Confirm hone ke BAAD jata hai, register ke waqt nahi — warna ye typo/fake
    /// addresses par bhi jata aur sender reputation kharab karta.
    /// </summary>
    public static EmailContent Welcome(string displayName, string appUrl)
    {
        var name = HtmlEncode(displayName);
        var url = HtmlEncode(appUrl);

        var inner = $"""
                    <h1 style="margin:0 0 10px;font:700 22px/1.3 {Font};color:{Ink};letter-spacing:-0.02em;">You're in, {name}</h1>
                    <p style="margin:0 0 22px;font:400 15px/1.65 {Font};color:{Muted};">
                      Your email is confirmed and your account is live. Here's a good order to start in:
                    </p>

                    <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" style="margin:0 0 26px;">
                      {Step("Fill in your profile", "Add a photo and a line about yourself so people know who they're following.")}
                      {Step("Write your first post", "Say hello. Short is fine.")}
                      {Step("Follow a few people", "Your feed is built from who you follow — nothing algorithmic, nothing you didn't ask for.")}
                    </table>

                    <table role="presentation" cellpadding="0" cellspacing="0" border="0" style="margin:0 0 24px;">
                      <tr>
                        <td style="border-radius:999px;background:{Accent};">
                          <a href="{url}" style="display:inline-block;padding:14px 28px;font:600 15px/1 {Font};color:#FFFFFF;text-decoration:none;border-radius:999px;">Open SocialApp</a>
                        </td>
                      </tr>
                    </table>

                    <div style="height:1px;background:{Border};margin:0 0 18px;"></div>

                    <p style="margin:0;font:400 13px/1.6 {Font};color:{Faint};">
                      Glad to have you here.
                    </p>
                    """;

        var text = $"""
                    You're in, {displayName}

                    Your email is confirmed and your account is live. Here's a good order
                    to start in:

                    1. Fill in your profile — add a photo and a line about yourself.
                    2. Write your first post — say hello. Short is fine.
                    3. Follow a few people — your feed is built from who you follow.

                    Open SocialApp: {appUrl}

                    Glad to have you here.
                    """;

        return new EmailContent(
            Subject: "Welcome to SocialApp",
            HtmlBody: Shell("Your account is confirmed — here's how to get started.", inner),
            PlainTextBody: text);
    }

    /// <summary>Welcome email ki checklist ka ek row: accent dot + title + description.</summary>
    private static string Step(string title, string description) => $"""
        <tr>
          <td width="26" valign="top" style="padding:0 0 14px;">
            <span style="display:inline-block;width:7px;height:7px;border-radius:50%;background:{Accent};margin-top:7px;"></span>
          </td>
          <td valign="top" style="padding:0 0 14px;">
            <div style="font:600 15px/1.5 {Font};color:{Ink};">{HtmlEncode(title)}</div>
            <div style="font:400 14px/1.6 {Font};color:{Muted};">{HtmlEncode(description)}</div>
          </td>
        </tr>
        """;

    /// <summary>
    /// Har email ka bahar ka dhancha: background, branded card, footer.
    /// </summary>
    /// <param name="preheader">
    /// Woh chhoti line jo inbox mein subject ke baad preview mein dikhti hai.
    /// Ye set na karo to clients khud body ka pehla text utha lete hain
    /// (jo aksar "Or paste this link..." jaisa bekar hota hai).
    /// </param>
    private static string Shell(string preheader, string innerHtml) => $"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width,initial-scale=1">
        <title>SocialApp</title>
        </head>
        <body style="margin:0;padding:0;background:{Bg};-webkit-font-smoothing:antialiased;">

          <div style="display:none;font-size:1px;line-height:1px;max-height:0;max-width:0;opacity:0;overflow:hidden;color:{Bg};">{HtmlEncode(preheader)}</div>

          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background:{Bg};padding:32px 12px;">
            <tr>
              <td align="center">

                <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="max-width:560px;background:{Surface};border:1px solid {Border};border-radius:18px;">
                  <tr>
                    <td style="padding:26px 32px 0;">
                      <span style="display:inline-block;width:10px;height:10px;border-radius:50%;background:{Accent};vertical-align:middle;"></span>
                      <span style="font:700 17px/1 {Font};color:{Ink};vertical-align:middle;margin-left:9px;letter-spacing:-0.01em;">SocialApp</span>
                    </td>
                  </tr>
                  <tr>
                    <td style="padding:24px 32px 32px;">{innerHtml}</td>
                  </tr>
                </table>

                <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="max-width:560px;">
                  <tr>
                    <td align="center" style="padding:18px 32px 0;font:400 12px/1.6 {Font};color:{Faint};">
                      Sent by SocialApp because someone signed up with this address.
                    </td>
                  </tr>
                </table>

              </td>
            </tr>
          </table>

        </body>
        </html>
        """;

    private static string HtmlEncode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
