namespace SocialApp.Models.ViewModels;

/// <summary>
/// Confirmation link click karne ka nateeja. Heading/Message controller set karta hai
/// take view mein if-else ka jungle na bane.
/// </summary>
public class ConfirmEmailViewModel
{
    /// <summary>true hone par view "Log in" button dikhata hai, warna "Send a new link".</summary>
    public bool Succeeded { get; set; }

    /// <summary>Pehle hi confirm ho chuki thi — na error hai na naya confirmation.</summary>
    public bool AlreadyConfirmed { get; set; }

    public string Heading { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}
