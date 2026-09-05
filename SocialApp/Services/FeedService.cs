using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SocialApp.Data;
using SocialApp.Models.Entities;
using SocialApp.Models.ViewModels;

namespace SocialApp.Services;

/// <summary>
/// Feed ki queries ek hi jagah. Home, Explore, Profile aur (validation fail hone
/// par) PostsController — chaaron ko wahi posts chahiye. Query controllers mein
/// copy-paste karte to kal ek jagah paging theek karte aur teen jagah bhool jate.
/// </summary>
public interface IFeedService
{
    /// <summary>Jinhein follow karte ho + apni posts. Yehi Home ka feed hai.</summary>
    Task<FeedViewModel> GetFollowingFeedAsync(string viewerId, int page, CancellationToken cancellationToken = default);

    /// <summary>Har kisi ki posts, naye se purani. Explore.</summary>
    Task<FeedViewModel> GetExploreFeedAsync(string viewerId, int page, CancellationToken cancellationToken = default);

    /// <summary>Ek user ki posts — profile page ke liye.</summary>
    Task<IReadOnlyList<PostViewModel>> GetUserPostsAsync(
        string authorId, string viewerId, int take = 20, CancellationToken cancellationToken = default);

    /// <summary>Ek post — permalink page (/p/12) ke liye. Na mile to null.</summary>
    Task<PostViewModel?> GetPostAsync(int postId, string viewerId, CancellationToken cancellationToken = default);

    /// <summary>Ek post ke comments, purane se naye (baat-cheet upar se neeche parhi jati hai).</summary>
    Task<IReadOnlyList<CommentViewModel>> GetCommentsAsync(
        int postId, string viewerId, CancellationToken cancellationToken = default);

    /// <summary>Posts mein search — content par case-insensitive matching.</summary>
    Task<FeedViewModel> SearchPostsAsync(
        string query, string viewerId, int page, CancellationToken cancellationToken = default);
}

public class FeedService : IFeedService
{
    /// <summary>
    /// Ek page par 10. Chhota page = tez pehla paint. Infinite scroll jaan bujh
    /// kar nahi hai (FIX #1) — feed khatam hota hai aur user ko pata chalta hai.
    /// </summary>
    public const int PageSize = 10;

    private readonly ApplicationDbContext _db;

    public FeedService(ApplicationDbContext db) => _db = db;

    public async Task<FeedViewModel> GetFollowingFeedAsync(
        string viewerId, int page, CancellationToken cancellationToken = default)
    {
        // Ye ek subquery ban jati hai, alag round-trip nahi.
        var followeeIds = _db.Follows
            .Where(f => f.FollowerId == viewerId)
            .Select(f => f.FolloweeId);

        var query = _db.Posts
            .Where(p => p.UserId == viewerId || followeeIds.Contains(p.UserId));

        return await BuildFeedAsync(query, viewerId, page, "following", cancellationToken);
    }

    public async Task<FeedViewModel> GetExploreFeedAsync(
        string viewerId, int page, CancellationToken cancellationToken = default)
        => await BuildFeedAsync(_db.Posts, viewerId, page, "explore", cancellationToken);

    public async Task<FeedViewModel> SearchPostsAsync(
        string query, string viewerId, int page, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new FeedViewModel { Tab = "search", Posts = [], Page = 1, HasOlder = false };
        }

        // Wildcards ([ , %, _) ko escape karte hain taake SQL LIKE crash ya galat pattern match na kare.
        var escaped = query.Trim()
            .Replace("[", "[[]")
            .Replace("%", "[%]")
            .Replace("_", "[_]");
        var pattern = $"%{escaped}%";

        // EF.Functions.Like SQL Server par CASE-INSENSITIVE LIKE query generate karta hai
        var postsQuery = _db.Posts
            .Where(p => EF.Functions.Like(p.Content, pattern));

        return await BuildFeedAsync(postsQuery, viewerId, page, "search", cancellationToken);
    }

    public async Task<IReadOnlyList<PostViewModel>> GetUserPostsAsync(
        string authorId, string viewerId, int take = 20, CancellationToken cancellationToken = default)
        => await _db.Posts
            .AsNoTracking()
            .Where(p => p.UserId == authorId)
            .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
            .Take(take)
            .Select(Project(viewerId))
            .ToListAsync(cancellationToken);

    public async Task<PostViewModel?> GetPostAsync(
        int postId, string viewerId, CancellationToken cancellationToken = default)
        => await _db.Posts
            .AsNoTracking()
            .Where(p => p.Id == postId)
            // Wahi projection jo feed use karti hai — is liye permalink par post
            // bilkul waise dikhti hai jaise feed mein, aur kal koi field add
            // karna ho to sirf ek jagah badalna parta hai.
            .Select(Project(viewerId))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<CommentViewModel>> GetCommentsAsync(
        int postId, string viewerId, CancellationToken cancellationToken = default)
        => await _db.Comments
            .AsNoTracking()
            .Where(c => c.PostId == postId)
            // Comments purane se naye — posts ka ulta. Feed mein taza khabar upar
            // chahiye, magar guftagu upar se neeche parhi jati hai.
            .OrderBy(c => c.CreatedAt).ThenBy(c => c.Id)
            .Select(c => new CommentViewModel
            {
                Id = c.Id,
                PostId = c.PostId,
                AuthorUserName = c.User!.UserName!,
                AuthorDisplayName = c.User.DisplayName,
                AuthorAvatarUrl = c.User.AvatarUrl,
                Content = c.Content,
                CreatedAt = c.CreatedAt,
                // Apna comment YA apni post par kisi ka bhi comment.
                CanDelete = c.UserId == viewerId || c.Post!.UserId == viewerId
            })
            .ToListAsync(cancellationToken);

    private static async Task<FeedViewModel> BuildFeedAsync(
        IQueryable<Post> query, string viewerId, int page, string tab, CancellationToken cancellationToken)
    {
        if (page < 1) page = 1;

        var posts = await query
            .AsNoTracking()
            // ThenBy(Id) zaroori hai: ek hi millisecond ki do posts ka order warna
            // har query mein badal sakta hai, aur paging par ek post do dafa ya
            // bilkul nazar hi nahi aati.
            .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
            .Skip((page - 1) * PageSize)
            // PageSize + 1 laate hain sirf ye jaanne ke liye ke aage bhi kuch hai.
            // Warna ek alag COUNT(*) query chalani parti — poore table par.
            .Take(PageSize + 1)
            .Select(Project(viewerId))
            .ToListAsync(cancellationToken);

        var hasOlder = posts.Count > PageSize;
        if (hasOlder) posts.RemoveAt(posts.Count - 1);

        return new FeedViewModel
        {
            Tab = tab,
            Posts = posts,
            Page = page,
            HasOlder = hasOlder
        };
    }

    /// <summary>
    /// Post → PostViewModel ka naqsha, ek hi jagah.
    ///
    /// Ye SQL mein tarjuma hota hai, memory mein nahi chalta — is liye counts
    /// correlated subqueries ban jati hain aur poore Likes/Comments rows kabhi
    /// network par nahi aate. Author ke fields Include() ke bajaye seedha select
    /// kar rahe hain: sirf 3 columns aate hain, poora user row nahi.
    /// </summary>
    private static Expression<Func<Post, PostViewModel>> Project(string viewerId) =>
        p => new PostViewModel
        {
            Id = p.Id,
            AuthorUserName = p.User!.UserName!,
            AuthorDisplayName = p.User.DisplayName,
            AuthorAvatarUrl = p.User.AvatarUrl,
            Content = p.Content,
            ImageUrl = p.ImageUrl,
            ImageWidth = p.ImageWidth,
            ImageHeight = p.ImageHeight,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt,
            LikeCount = p.Likes.Count,
            CommentCount = p.Comments.Count,
            IsMine = p.UserId == viewerId,
            IsLikedByMe = p.Likes.Any(l => l.UserId == viewerId)
        };
}
