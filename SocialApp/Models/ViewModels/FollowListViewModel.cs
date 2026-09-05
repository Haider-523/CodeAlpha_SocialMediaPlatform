namespace SocialApp.Models.ViewModels;

/// <summary>
/// Followers aur Following list pages ka view model.
/// Ek hi view model dono lists (followers/following) ko handle karta hai.
/// </summary>
public class FollowListViewModel
{
    public string TargetUserId { get; set; } = string.Empty;
    public string TargetUserName { get; set; } = string.Empty;
    public string TargetDisplayName { get; set; } = string.Empty;
    public string? TargetAvatarUrl { get; set; }

    /// <summary>Active tab: "followers" ya "following"</summary>
    public string Tab { get; set; } = "followers";

    /// <summary>List mein maujood user cards (SearchUserViewModel reuse kiya gaya hai)</summary>
    public IReadOnlyList<SearchUserViewModel> Users { get; set; } = [];

    public int FollowerCount { get; set; }
    public int FollowingCount { get; set; }

    public int Page { get; set; } = 1;
    public bool HasOlder { get; set; }
    public bool HasNewer => Page > 1;
    public bool IsEmpty => Users.Count == 0;

    public bool IsFollowersTab => string.Equals(Tab, "followers", StringComparison.OrdinalIgnoreCase);
    public bool IsFollowingTab => string.Equals(Tab, "following", StringComparison.OrdinalIgnoreCase);

    public string TargetInitial =>
        string.IsNullOrWhiteSpace(TargetDisplayName) ? "?" : TargetDisplayName.Trim()[..1].ToUpperInvariant();
}
