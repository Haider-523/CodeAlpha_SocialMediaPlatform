using System.ComponentModel.DataAnnotations;

namespace SocialApp.Models.Entities;

/// <summary>A comment written by a user on a post.</summary>
public class Comment
{
    public int Id { get; set; }

    // ---------- Which post ----------
    public int PostId { get; set; }
    public Post? Post { get; set; }

    // ---------- Who wrote it ----------
    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    [Required]
    [MaxLength(300)]
    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
