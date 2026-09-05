using DnsClient;
using DnsClient.Protocol;

namespace SocialApp.Services;

/// <summary>
/// DNS lookup se tasdeeq karta hai ke domain mail receive kar sakta hai.
///
/// Tarteeb RFC 5321 §5.1 ke mutabiq hai:
///   1) MX record dhoondo — mail servers ka asal jawab yehi hai.
///   2) MX na ho to A/AAAA record dekho — RFC kehta hai us surat mein
///      mail seedha us host par bhej di jaye ("implicit MX").
///   3) Dono na hon → is domain par mail kabhi deliver nahi ho sakti.
///
/// PERFORMANCE: LookupClient singleton hai aur DNS jawabon ko cache karta hai,
/// timeout 3 second hai aur sirf 1 retry — registration DNS par latka nahi rehta.
/// </summary>
public class DnsEmailDomainValidator : IEmailDomainValidator
{
    private readonly ILookupClient _lookup;
    private readonly ILogger<DnsEmailDomainValidator> _logger;

    /// <summary>
    /// Woh providers jin ke typos sab se zyada hote hain. Sirf behtar error
    /// message ke liye — reject karne ka faisla DNS karta hai, ye list nahi.
    /// </summary>
    private static readonly string[] CommonDomains =
    [
        "gmail.com", "googlemail.com", "yahoo.com", "ymail.com",
        "hotmail.com", "outlook.com", "live.com", "msn.com",
        "icloud.com", "me.com", "aol.com", "protonmail.com", "proton.me"
    ];

    public DnsEmailDomainValidator(ILookupClient lookup, ILogger<DnsEmailDomainValidator> logger)
    {
        _lookup = lookup;
        _logger = logger;
    }

    public async Task<EmailDomainResult> CheckAsync(string email, CancellationToken cancellationToken = default)
    {
        var domain = ExtractDomain(email);
        if (domain is null)
            return new EmailDomainResult(EmailDomainCheck.NoMailServer, null);

        var suggestion = SuggestCorrection(domain);

        try
        {
            // 1) MX
            var mx = await _lookup.QueryAsync(domain, QueryType.MX, cancellationToken: cancellationToken);
            if (mx.Answers.OfType<MxRecord>().Any(r => !string.IsNullOrWhiteSpace(r.Exchange?.Value)))
                return new EmailDomainResult(EmailDomainCheck.Deliverable, null);

            // 2) Implicit MX — A record
            var a = await _lookup.QueryAsync(domain, QueryType.A, cancellationToken: cancellationToken);
            if (a.Answers.OfType<ARecord>().Any())
                return new EmailDomainResult(EmailDomainCheck.Deliverable, null);

            // 3) Implicit MX — AAAA (IPv6-only domains)
            var aaaa = await _lookup.QueryAsync(domain, QueryType.AAAA, cancellationToken: cancellationToken);
            if (aaaa.Answers.OfType<AaaaRecord>().Any())
                return new EmailDomainResult(EmailDomainCheck.Deliverable, null);

            _logger.LogInformation("Domain {Domain} ka koi mail server nahi mila.", domain);
            return new EmailDomainResult(EmailDomainCheck.NoMailServer, suggestion);
        }
        catch (Exception ex)
        {
            // DESIGN: yahan jaan-boojh kar "fail-open" karte hain.
            //
            // Agar DNS server down ho, machine offline ho, ya timeout ho jaye — to
            // hum poori registration band nahi karte. DNS check sirf ek suhoolat hai
            // (typo pakarne ke liye); asli darwaza confirmation email hai. Fail-closed
            // karne ka matlab hota: humara DNS kharab, aur naye users signup hi na kar saken.
            _logger.LogWarning(ex, "Domain {Domain} ka DNS check nahi ho saka — ijazat de rahe hain.", domain);
            return new EmailDomainResult(EmailDomainCheck.CouldNotCheck, suggestion);
        }
    }

    private static string? ExtractDomain(string email)
    {
        // Aakhri '@' ke baad wala hissa. LastIndexOf is liye ke local part
        // (quoted form mein) khud '@' rakh sakta hai.
        var at = email.LastIndexOf('@');
        if (at <= 0 || at == email.Length - 1)
            return null;

        var domain = email[(at + 1)..].Trim().TrimEnd('.').ToLowerInvariant();

        // Bina dot wala domain (jaise "you@localhost") public mail ke liye be-maani hai.
        return domain.Contains('.') ? domain : null;
    }

    /// <summary>
    /// Kisi mashhoor domain se 1-2 characters ka farq ho to usay suggest karo.
    /// "gmial.com" → "gmail.com", "gmail.comm" → "gmail.com"
    /// </summary>
    private static string? SuggestCorrection(string domain)
    {
        if (CommonDomains.Contains(domain))
            return null;   // bilkul sahi hai, kuch suggest karne ki zaroorat nahi

        string? best = null;
        var bestDistance = int.MaxValue;

        foreach (var candidate in CommonDomains)
        {
            var d = Levenshtein(domain, candidate);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = candidate;
            }
        }

        // 2 se zyada farq ho to ye typo nahi, koi aur domain hai — chup raho.
        return bestDistance <= 2 ? best : null;
    }

    /// <summary>
    /// Do strings ke darmiyan edit distance. Do rows wala version — poora
    /// matrix banane ki zaroorat nahi, sirf pichli row chahiye hoti hai.
    /// </summary>
    private static int Levenshtein(string a, string b)
    {
        if (a == b) return 0;
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];

        for (var j = 0; j <= b.Length; j++)
            previous[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;

            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1,   // insert
                             previous[j] + 1),      // delete
                    previous[j - 1] + cost);        // substitute
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}
