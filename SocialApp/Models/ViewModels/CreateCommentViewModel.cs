using System.ComponentModel.DataAnnotations;

namespace SocialApp.Models.ViewModels;

/// <summary>
/// Comment likhne ka form. Entity ki 300-character limit yahan bhi hai — dono
/// jagah rakhna zaroori hai: entity DB ko batati hai, ye user ko sahi message
/// dikhati hai (aur browser side par bhi rok deti hai).
/// </summary>
public class CreateCommentViewModel
{
    public int PostId { get; set; }

    [Required(ErrorMessage = "Write something before you post the comment.")]
    [StringLength(300, ErrorMessage = "A comment can be at most 300 characters.")]
    public string Content { get; set; } = string.Empty;
}
