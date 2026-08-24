using System.ComponentModel.DataAnnotations;

namespace SocialApp.Models.Entities;

/// <summary>A single post in the feed: text plus an optional image.</summary>
public class Post
{
    public int Id { get; set; }

    // ---------- Foreign key to the author ----------
    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    [Required]
    [MaxLength(500)]
    public string Content { get; set; } = string.Empty;

    /// <summary>Relative path of the uploaded image, e.g. /uploads/posts/abc.jpg</summary>
    [MaxLength(300)]
    public string? ImageUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Set only when the post is edited; null means never edited.</summary>
    public DateTime? UpdatedAt { get; set; }

    // ---------- Navigation properties ----------
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<Like> Likes { get; set; } = new List<Like>();
}
