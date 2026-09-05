using System.ComponentModel.DataAnnotations;

namespace SocialApp.Models.ViewModels;

/// <summary>Login form ka data carrier.</summary>
public class LoginViewModel
{
    [Required(ErrorMessage = "Please enter your email.")]
    [EmailAddress(ErrorMessage = "That doesn't look like a valid email.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your password.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// True = cookie browser band karne ke baad bhi rehti hai (7 din, jo humne
    /// Program.cs mein ExpireTimeSpan set kiya tha). False = session cookie.
    /// </summary>
    [Display(Name = "Keep me signed in")]
    public bool RememberMe { get; set; } = true;
}
