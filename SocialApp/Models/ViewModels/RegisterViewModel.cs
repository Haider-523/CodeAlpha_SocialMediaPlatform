using System.ComponentModel.DataAnnotations;

namespace SocialApp.Models.ViewModels;

/// <summary>
/// Sirf Register form ke liye data carrier (ye EF entity NAHI hai).
/// ViewModel isliye alag rakhte hain taake form ke apne validation rules
/// database entity se mix na hon — form aur table ki zaroorat alag hoti hai.
/// </summary>
public class RegisterViewModel
{
    [Required(ErrorMessage = "Please enter a display name.")]
    [MaxLength(50)]
    [Display(Name = "Display name")]
    public string DisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please pick a username.")]
    [RegularExpression(@"^[a-zA-Z0-9_]{3,20}$",
        ErrorMessage = "Username 3-20 characters, sirf letters, numbers ya underscore.")]
    [Display(Name = "Username")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your email.")]
    [EmailAddress(ErrorMessage = "That doesn't look like a valid email.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please choose a password.")]
    [StringLength(100, MinimumLength = 6,
        ErrorMessage = "Password must be at least 6 characters.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your password.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "The two passwords don't match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
