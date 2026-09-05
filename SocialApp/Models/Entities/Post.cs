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

    /// <summary>Relative path of the uploaded image, e.g. /uploads/posts/abc.webp</summary>
    [MaxLength(300)]
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Saved image ki naap (pixels). Ye jaan bujh kar DB mein rakhi hai: view
    /// isse &lt;img width/height&gt; aur CSS aspect-ratio bharta hai, jis se image
    /// load hote waqt feed hilta nahi. Naap ke bina jagah reserve karna namumkin
    /// hai — Facebook ke feed ka mashhoor "jumping" masla yehi hai.
    /// Null = post par koi image nahi (ya purani post jo dimensions se pehle bani).
    /// </summary>
    public int? ImageWidth { get; set; }
    public int? ImageHeight { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Set only when the post is edited; null means never edited.</summary>
    public DateTime? UpdatedAt { get; set; }

    // ---------- Navigation properties ----------
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<Like> Likes { get; set; } = new List<Like>();
}
