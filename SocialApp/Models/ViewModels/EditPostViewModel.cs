using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace SocialApp.Models.ViewModels;

/// <summary>
/// Post edit form ka view model.
/// Content update, existing image removal, ya new image replace handle karta hai.
/// </summary>
public class EditPostViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Post content cannot be empty.")]
    [StringLength(500, ErrorMessage = "Post cannot exceed 500 characters.")]
    public string Content { get; set; } = string.Empty;

    public string? CurrentImageUrl { get; set; }
    public int? ImageWidth { get; set; }
    public int? ImageHeight { get; set; }

    public string ImageAspectRatio =>
        ImageWidth.HasValue && ImageHeight.HasValue && ImageHeight > 0
            ? $"{ImageWidth.Value} / {ImageHeight.Value}"
            : "16 / 10";

    /// <summary>User ne "Remove image" checkbox check kiya ho</summary>
    public bool RemoveImage { get; set; }

    /// <summary>User nayi image upload karke replace karna chahta ho</summary>
    public IFormFile? NewImage { get; set; }

    public string? ReturnUrl { get; set; }
}
