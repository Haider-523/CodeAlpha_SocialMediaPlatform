using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace SocialApp.Models.Entities;

/// <summary>
/// Application user. Inherits from IdentityUser, which already provides
/// Id, UserName, Email, PasswordHash, PhoneNumber, SecurityStamp, lockout fields etc.
/// Here we only add the extra profile columns our social app needs.
/// </summary>
public class ApplicationUser : IdentityUser
{
    [Required]
    [MaxLength(50)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(250)]
    public string? Bio { get; set; }

    /// <summary>Relative path of the uploaded avatar, e.g. /uploads/avatars/abc.jpg</summary>
    [MaxLength(300)]
    public string? AvatarUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ---------- Navigation properties ----------

    /// <summary>Posts written by this user.</summary>
    public ICollection<Post> Posts { get; set; } = new List<Post>();

    /// <summary>Comments written by this user.</summary>
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();

    /// <summary>Likes given by this user.</summary>
    public ICollection<Like> Likes { get; set; } = new List<Like>();

    /// <summary>Follow rows where THIS user is the follower (i.e. people this user follows).</summary>
    public ICollection<Follow> Following { get; set; } = new List<Follow>();

    /// <summary>Follow rows where THIS user is the followee (i.e. people who follow this user).</summary>
    public ICollection<Follow> Followers { get; set; } = new List<Follow>();
}
