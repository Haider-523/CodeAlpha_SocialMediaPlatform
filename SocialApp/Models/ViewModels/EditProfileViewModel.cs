using System.ComponentModel.DataAnnotations;

namespace SocialApp.Models.ViewModels;

/// <summary>Apna profile edit karne ka form.</summary>
public class EditProfileViewModel
{
    [Required(ErrorMessage = "Naam khali nahi chhor sakte.")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "Naam 2 se 50 harf ke darmiyan ho.")]
    [Display(Name = "Display name")]
    public string DisplayName { get; set; } = string.Empty;

    [StringLength(250, ErrorMessage = "Bio 250 harf se zyada nahi.")]
    [Display(Name = "Bio")]
    public string? Bio { get; set; }

    /// <summary>Nayi avatar file. null = avatar wesa hi rehne do.</summary>
    [Display(Name = "Profile photo")]
    public IFormFile? Avatar { get; set; }

    /// <summary>Checkbox — mojooda avatar hata do (naya upload kiye bina).</summary>
    [Display(Name = "Remove current photo")]
    public bool RemoveAvatar { get; set; }

    // ---- Sirf dikhane ke liye; POST par controller inhein khud dobara bharta hai
    //      take koi form mein badal kar na bhej de. ----
    public string? CurrentAvatarUrl { get; set; }
    public string UserName { get; set; } = string.Empty;
}
