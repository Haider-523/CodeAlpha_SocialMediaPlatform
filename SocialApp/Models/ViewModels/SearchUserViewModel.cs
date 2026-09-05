namespace SocialApp.Models.ViewModels;

/// <summary>
/// Search results mein har user card ka data.
/// EF projection se direct bharta hai taake pura ApplicationUser load na karna pare
/// aur correlated subqueries se follower count aur follow status bina extra round-trips ke mil jayein.
/// </summary>
public class SearchUserViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public int FollowerCount { get; set; }

    /// <summary>Log-in shakhs is user ko pehle se follow kar raha hai ya nahi</summary>
    public bool IsFollowing { get; set; }

    /// <summary>true = ye user main khud hoon, to follow button na dikhao</summary>
    public bool IsMe { get; set; }

    /// <summary>Avatar na hone par pehla harf dikhane ke liye fallback</summary>
    public string Initial =>
        string.IsNullOrWhiteSpace(DisplayName) ? "?" : DisplayName.Trim()[..1].ToUpperInvariant();
}
