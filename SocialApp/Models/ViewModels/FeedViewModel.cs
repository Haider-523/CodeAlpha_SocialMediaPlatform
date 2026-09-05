namespace SocialApp.Models.ViewModels;

/// <summary>Home aur Explore dono isi model par chalte hain.</summary>
public class FeedViewModel
{
    /// <summary>"following" ya "explore" — segmented control mein active tab.</summary>
    public string Tab { get; init; } = "following";

    /// <summary>Composer sirf Home par hota hai; Explore par null.</summary>
    public CreatePostViewModel? Composer { get; set; }

    public IReadOnlyList<PostViewModel> Posts { get; init; } = [];

    public int Page { get; init; } = 1;

    /// <summary>Is se aage bhi posts hain? (Neeche "Older posts" button dikhane ke liye.)</summary>
    public bool HasOlder { get; init; }

    /// <summary>Composer ke avatar ke liye — logged-in user ka.</summary>
    public string? MyAvatarUrl { get; set; }
    public string MyInitial { get; set; } = "?";
    public string MyFirstName { get; set; } = "there";

    public bool HasNewer => Page > 1;
    public bool IsEmpty => Posts.Count == 0;

    /// <summary>
    /// Composer ke liye logged-in user ki chhoti si maloomat bhar deta hai.
    /// Entity type ke bajaye do plain strings leta hai taake ViewModels layer
    /// Entities par depend na kare.
    /// </summary>
    public FeedViewModel WithViewer(string displayName, string? avatarUrl)
    {
        MyAvatarUrl = avatarUrl;

        var name = (displayName ?? string.Empty).Trim();
        MyInitial = name.Length == 0 ? "?" : name[..1].ToUpperInvariant();

        // Placeholder mein poora naam mat likho — "What's on your mind, Nawab
        // Siddiqui?" bhaari lagta hai. Sirf pehla lafz.
        var space = name.IndexOf(' ');
        MyFirstName = name.Length == 0 ? "there" : (space > 0 ? name[..space] : name);

        return this;
    }
}
