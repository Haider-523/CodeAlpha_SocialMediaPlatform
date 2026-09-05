namespace SocialApp.Models.ViewModels;

/// <summary>
/// Feed mein ek post dikhane ke liye jitna data chahiye — utna hi. Entity ko
/// seedha view par bhejna aasan lagta hai magar phir view ko DB ki shakl ka
/// pata hona zaroori ho jata hai, aur EF lazy-load ki koshish mein N+1 queries
/// chala deta hai. Ye flat shape ek hi SQL projection se bhar jaati hai.
/// </summary>
public class PostViewModel
{
    public int Id { get; init; }

    // ── Author ──
    public string AuthorUserName { get; init; } = string.Empty;
    public string AuthorDisplayName { get; init; } = string.Empty;
    public string? AuthorAvatarUrl { get; init; }

    // ── Content ──
    public string Content { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public int? ImageWidth { get; init; }
    public int? ImageHeight { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }

    public int LikeCount { get; init; }
    public int CommentCount { get; init; }

    /// <summary>Sirf apni post par delete/edit dikhta hai.</summary>
    public bool IsMine { get; init; }

    /// <summary>Heart bhara hua dikhega ya khali — aur button "Unlike" karega.</summary>
    public bool IsLikedByMe { get; init; }

    public bool WasEdited => UpdatedAt.HasValue;

    /// <summary>
    /// CSS <c>aspect-ratio</c> ki value — image ke liye jagah PEHLE se reserve
    /// karne ke liye (FIX #5, zero layout shift).
    ///
    /// Ek clamp bhi hai: 4:5 se zyada lambi tasveer feed mein poori screen kha
    /// jati hai aur neeche wali post nazar hi nahi aati. Instagram bhi 4:5 par
    /// rok deta hai. Utni hi jagah dete hain aur baqi centre se crop ho jata hai.
    /// Naap na ho (purani post) to 16:10 par gir jate hain.
    /// </summary>
    public string ImageAspectRatio
    {
        get
        {
            if (ImageWidth is not > 0 || ImageHeight is not > 0) return "16 / 10";

            int w = ImageWidth.Value, h = ImageHeight.Value;

            // h/w > 5/4  ⟺  4h > 5w
            return 4 * h > 5 * w ? "4 / 5" : $"{w} / {h}";
        }
    }

    /// <summary>Avatar na ho to naam ka pehla harf circle mein.</summary>
    public string AuthorInitial => PostedAt.Initial(AuthorDisplayName);

    /// <summary>"now" / "5m" / "3h" / "2d" / "12 Aug" — tafseel PostedAt mein.</summary>
    public string RelativeTime => PostedAt.Relative(CreatedAt);

    /// <summary>Hover/focus par poora waqt — <c>title</c> attribute ke liye.</summary>
    public string ExactTime => PostedAt.Exact(CreatedAt);

    /// <summary>ISO-8601 UTC — site.js isse reader ke timezone mein badalta hai.</summary>
    public string IsoTime => PostedAt.Iso(CreatedAt);
}
