namespace SocialApp.Services;

/// <summary>
/// SMTP configuration, strongly-typed. Ye "Smtp" section se bind hoti hai.
///
/// Non-secret values (Host, Port, FromName)  → appsettings.json
/// Secrets (UserName, Password, FromEmail)   → dotnet user-secrets
///
/// IConfiguration dono sources ko ek hi "Smtp" section mein merge kar deta hai,
/// is liye code ko farq nahi parta ke value kahan se aayi.
/// </summary>
public class SmtpSettings
{
    public const string SectionName = "Smtp";

    /// <summary>Gmail ke liye smtp.gmail.com</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>587 = STARTTLS (recommended), 465 = implicit SSL.</summary>
    public int Port { get; set; } = 587;

    /// <summary>true = port 587 par plain connect kar ke TLS mein upgrade karo.</summary>
    public bool UseStartTls { get; set; } = true;

    /// <summary>Poora Gmail address.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>16-character Gmail App Password — normal password kaam nahi karega.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Gmail sirf usi address se bhejne deta hai jo authenticate hua hai,
    /// is liye ye <see cref="UserName"/> ke barabar hona chahiye.
    /// </summary>
    public string FromEmail { get; set; } = string.Empty;

    /// <summary>Inbox mein sender ka jo naam nazar aayega.</summary>
    public string FromName { get; set; } = "SocialApp";

    /// <summary>
    /// Auth ke liye saaf kiya hua password.
    ///
    /// Google App Password ko apni UI mein "abcd efgh ijkl mnop" ki tarah —
    /// spaces ke saath — dikhata hai, aur log usi tarah copy kar lete hain.
    /// Gmail SMTP us par 535 BadCredentials deta hai. Har asli mail client ye
    /// spaces khud hata deta hai, is liye hum bhi hata dete hain. Newline aur
    /// zero-width space bhi copy-paste mein chale aate hain, woh bhi nikal rahe hain.
    /// </summary>
    public string AuthPassword =>
        new string(Password.Where(c => !char.IsWhiteSpace(c) && c != '\u200B').ToArray());

    /// <summary>Login username — aage-peeche ki spaces hata kar.</summary>
    public string AuthUserName => UserName.Trim();

    /// <summary>
    /// Config adhoori hai to hum email bhejne ki koshish se pehle hi
    /// saaf error dena chahte hain, SMTP timeout ka intezaar nahi.
    /// </summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host) &&
        !string.IsNullOrWhiteSpace(UserName) &&
        !string.IsNullOrWhiteSpace(Password) &&
        !string.IsNullOrWhiteSpace(FromEmail);

    /// <summary>
    /// Development-only diagnostic. Secret KABHI nahi dikhata — sirf uski shakl:
    /// lambai, aur kya usme aise characters hain jo Google ke App Password mein
    /// nahi hote (woh sirf 16 lowercase a–z hote hain). 535 ki wajah aksar isi
    /// ek line se pakri jati hai.
    /// </summary>
    public string DescribeSecretShape()
    {
        var p = AuthPassword;
        var raw = Password.Length;
        var odd = p.Where(c => c is < 'a' or > 'z').Distinct().ToArray();

        return $"UserName='{AuthUserName}', FromEmail='{FromEmail.Trim()}', " +
               $"password length {raw} raw / {p.Length} cleaned " +
               $"(Google ka App Password saaf hone ke baad 16 hona chahiye), " +
               $"unexpected chars: {(odd.Length == 0 ? "none" : string.Join(" ", odd.Select(c => $"'{c}'")))}, " +
               $"UserName == FromEmail: {string.Equals(AuthUserName, FromEmail.Trim(), StringComparison.OrdinalIgnoreCase)}";
    }
}
