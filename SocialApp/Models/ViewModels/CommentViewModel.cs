namespace SocialApp.Models.ViewModels;

/// <summary>
/// Post detail page par ek comment. PostViewModel ki tarah flat — poora SQL
/// projection se bharta hai, koi lazy loading nahi.
/// </summary>
public class CommentViewModel
{
    public int Id { get; init; }
    public int PostId { get; init; }

    public string AuthorUserName { get; init; } = string.Empty;
    public string AuthorDisplayName { get; init; } = string.Empty;
    public string? AuthorAvatarUrl { get; init; }

    public string Content { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Apna comment, YA apni post par kisi ka bhi comment.
    ///
    /// Doosri shart jaan bujh kar hai: apni post par aane wale spam ya gaali ko
    /// hatane ka haq post ke malik ka hona chahiye. Instagram/Facebook dono yahi
    /// karte hain, aur is ke bina moderation ka koi raasta nahi bachta.
    /// </summary>
    public bool CanDelete { get; init; }

    public string AuthorInitial => PostedAt.Initial(AuthorDisplayName);
    public string RelativeTime => PostedAt.Relative(CreatedAt);
    public string ExactTime => PostedAt.Exact(CreatedAt);
    public string IsoTime => PostedAt.Iso(CreatedAt);
}
