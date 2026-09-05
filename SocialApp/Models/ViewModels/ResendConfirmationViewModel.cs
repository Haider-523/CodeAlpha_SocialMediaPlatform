using System.ComponentModel.DataAnnotations;

namespace SocialApp.Models.ViewModels;

/// <summary>
/// Naya confirmation link mangne ka form — jab pehla email spam mein chala jaye
/// ya 24 ghante mein expire ho jaye.
/// </summary>
public class ResendConfirmationViewModel
{
    [Required(ErrorMessage = "Please enter your email.")]
    [EmailAddress(ErrorMessage = "That doesn't look like a valid email.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;
}
