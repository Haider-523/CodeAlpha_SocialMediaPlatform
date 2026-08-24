using System.ComponentModel.DataAnnotations;

namespace SocialApp.Models.Entities;

/// <summary>
/// One row = one user liking one post.
/// A unique index on (PostId, UserId) in ApplicationDbContext guarantees
/// that the same user can never like the same post twice.
/// </summary>
public class Like
{
    public int Id { get; set; }

    public int PostId { get; set; }
    public Post? Post { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
