using System.ComponentModel.DataAnnotations;

namespace SocialApp.Models.Entities;

/// <summary>
/// A self-referencing many-to-many relationship on ApplicationUser:
/// one row means "Follower follows Followee".
/// A unique index on (FollowerId, FolloweeId) prevents duplicate follow rows.
/// </summary>
public class Follow
{
    public int Id { get; set; }

    /// <summary>The user who clicked "Follow".</summary>
    [Required]
    public string FollowerId { get; set; } = string.Empty;
    public ApplicationUser? Follower { get; set; }

    /// <summary>The user who is being followed.</summary>
    [Required]
    public string FolloweeId { get; set; } = string.Empty;
    public ApplicationUser? Followee { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
