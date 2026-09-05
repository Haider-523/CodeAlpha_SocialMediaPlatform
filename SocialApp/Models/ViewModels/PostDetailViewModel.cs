namespace SocialApp.Models.ViewModels;

/// <summary>
/// /p/12 — ek post ka apna page (permalink) + uske saare comments.
///
/// Comments ko feed ke andar khol kar dikhane ke bajaye alag page rakha hai, do
/// wajah se: (1) har post ka ek asli, share karne wala URL ban jata hai — X par
/// reply ka link nikalna ab bhi mushkil hai; (2) feed ka HTML chhota rehta hai,
/// warna 10 posts ke saath un ke saare comments bhi load hote.
/// </summary>
public class PostDetailViewModel
{
    public PostViewModel Post { get; init; } = new();

    public IReadOnlyList<CommentViewModel> Comments { get; init; } = [];

    /// <summary>Comment form. Validation fail ho to isi mein user ka text wapas aata hai.</summary>
    public CreateCommentViewModel Composer { get; set; } = new();

    // ── Comment likhne wale (yani viewer) ka avatar, form ke saath dikhane ke liye ──
    public string? MyAvatarUrl { get; set; }
    public string MyInitial { get; set; } = "?";

    public PostDetailViewModel WithViewer(string? displayName, string? avatarUrl)
    {
        MyAvatarUrl = avatarUrl;
        MyInitial = PostedAt.Initial(displayName);
        return this;
    }
}
