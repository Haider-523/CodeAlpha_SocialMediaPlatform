namespace SocialApp.Models.ViewModels;

/// <summary>
/// Waqt aur avatar-initial ko dikhane wale ka kaam — ek hi jagah.
///
/// Post aur Comment dono ko bilkul yehi teen cheezein chahiye. Pehle ye logic
/// PostViewModel ke andar tha; Comment aate hi usse copy karna parta, aur phir
/// kal timezone ka bug ek jagah theek hota aur doosri jagah reh jata. Is liye
/// static helper.
/// </summary>
public static class PostedAt
{
    /// <summary>
    /// SQL Server ka <c>datetime2</c> timezone yaad nahi rakhta, is liye EF value
    /// ko <c>Kind=Unspecified</c> de kar wapas karta hai. Hum UTC hi save karte
    /// hain, to Kind wapas UTC lagana zaroori hai — warna <c>ToLocalTime()</c>
    /// usse "pehle hi local hai" samajh kar kuch convert hi nahi karta (Karachi
    /// mein 5 ghante ka farq).
    /// </summary>
    public static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    /// <summary>
    /// "now" / "5m" / "3h" / "2d" / "12 Aug".
    ///
    /// FIX #4 — X sirf "2h" dikhata hai aur asli waqt jaanne ka koi tareeqa nahi
    /// deta. Hum ye chhota label dikhate hain magar poori tareekh
    /// <see cref="Exact"/> se <c>title</c> attribute mein bhi bhejte hain.
    /// </summary>
    public static string Relative(DateTime value)
    {
        var utc = AsUtc(value);
        var span = DateTime.UtcNow - utc;

        // Server aur DB ki clock mein zara sa farq ho to span manfi aa sakta hai —
        // "in -3 seconds" jaisi bewaqoofi se bachne ke liye clamp.
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;

        if (span.TotalSeconds < 60) return "now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays}d";

        var local = utc.ToLocalTime();
        return local.Year == DateTime.Now.Year
            ? local.ToString("d MMM")
            : local.ToString("d MMM yyyy");
    }

    /// <summary>Hover/focus par poora waqt — <c>title</c> attribute ke liye.</summary>
    public static string Exact(DateTime value) =>
        AsUtc(value).ToLocalTime().ToString("d MMM yyyy, h:mm tt");

    /// <summary>
    /// ISO-8601 UTC (Z ke saath). site.js isse parh kar title ko browser ke local
    /// timezone mein badal deta hai — deploy hone ke baad server ka timezone user
    /// ka timezone nahi hota, is liye ye zaroori hai.
    /// </summary>
    public static string Iso(DateTime value) => AsUtc(value).ToString("o");

    /// <summary>Avatar na ho to naam ka pehla harf circle mein.</summary>
    public static string Initial(string? displayName) =>
        string.IsNullOrWhiteSpace(displayName)
            ? "?"
            : displayName.Trim()[..1].ToUpperInvariant();
}
