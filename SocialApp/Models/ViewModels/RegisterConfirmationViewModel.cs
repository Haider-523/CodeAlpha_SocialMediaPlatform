namespace SocialApp.Models.ViewModels;

/// <summary>
/// "Check your inbox" page ka data. Register hone ke baad user yahan land karta hai.
/// </summary>
public class RegisterConfirmationViewModel
{
    /// <summary>Jis address par link gaya — user ko dikhate hain take typo pakra ja sake.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// false = account ban gaya magar SMTP ne mail nahi bheji (network/credentials issue).
    /// Us surat mein user ko chup-chaap intezaar karwane ke bajaye saaf batate hain.
    /// </summary>
    public bool EmailSent { get; set; }

    /// <summary>
    /// SMTP nakami ki asli wajah. Controller ise SIRF Development mein bharta hai —
    /// production mein exception messages user ko dikhana information leak hai.
    /// </summary>
    public string? DevEmailError { get; set; }

    /// <summary>
    /// Woh confirmation link jo email mein gaya tha. SIRF Development mein set hota
    /// hai, taake SMTP theek hone se pehle bhi poora flow test kiya ja sake.
    /// </summary>
    public string? DevConfirmUrl { get; set; }
}
