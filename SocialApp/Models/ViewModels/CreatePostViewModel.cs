using System.ComponentModel.DataAnnotations;

namespace SocialApp.Models.ViewModels;

/// <summary>Composer ka input — text zaroori, image optional.</summary>
public class CreatePostViewModel
{
    [Required(ErrorMessage = "Write something before posting.")]
    [StringLength(500, ErrorMessage = "A post can be at most 500 characters.")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Optional tasveer. Validation IImageStorage karta hai (size, asli format,
    /// pixel limit) — DataAnnotations file ke andar nahi dekh sakte, sirf naam
    /// dekh sakte hain, aur naam jhoot bol sakta hai.
    /// </summary>
    public IFormFile? Image { get; set; }
}
