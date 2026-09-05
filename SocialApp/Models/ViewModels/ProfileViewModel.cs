namespace SocialApp.Models.ViewModels;

/// <summary>
/// Kisi bhi user ka profile page. Ye poora object EF ki EK query se bharta hai
/// (projection + subquery counts), is liye N+1 ka koi risk nahi.
/// </summary>
public class ProfileViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime JoinedAt { get; set; }

    public int PostCount { get; set; }
    public int FollowerCount { get; set; }
    public int FollowingCount { get; set; }

    /// <summary>true = ye mera hi profile hai, to "Edit profile" dikhao, "Follow" nahi.</summary>
    public bool IsMe { get; set; }

    /// <summary>Follow button ka Step 7 ke liye state.</summary>
    public bool IsFollowing { get; set; }

    /// <summary>
    /// Is user ki posts, nayi se purani. Header ki query se alag bharti hai —
    /// wajah ProfileController mein likhi hai.
    /// </summary>
    public IReadOnlyList<PostViewModel> Posts { get; set; } = [];

    /// <summary>Avatar na ho to pehla harf dikhate hain.</summary>
    public string Initial =>
        string.IsNullOrWhiteSpace(DisplayName) ? "?" : DisplayName.Trim()[..1].ToUpperInvariant();
}
