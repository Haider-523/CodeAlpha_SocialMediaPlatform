using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SocialApp.Data;
using SocialApp.Models.Entities;
using SocialApp.Models.ViewModels;
using SocialApp.Services;

namespace SocialApp.Controllers;

/// <summary>
/// Search — People (UserName / DisplayName) aur Posts (Content).
///
/// GET request use karte hain taake search linkable aur bookmark-able ho: /search?q=haider&tab=people.
/// SQL Server / LocalDB par LIKE '%pattern%' full table scan karta hai (leading wildcard B-tree index use nahi kar sakta).
/// Production mein iska hal Full-Text Search (FTS) ya Elasticsearch hota hai, magar is internship scope ke liye
/// EF.Functions.Like with pattern escaping bilkul munasib aur tez hai.
/// </summary>
[Authorize]
public class SearchController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IFeedService _feed;

    public SearchController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IFeedService feed)
    {
        _db = db;
        _userManager = userManager;
        _feed = feed;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Index(
        string? q,
        string tab = "people",
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var myId = _userManager.GetUserId(User);
        if (myId is null) return Challenge();

        var trimmedQuery = q?.Trim();
        var activeTab = string.Equals(tab, "posts", StringComparison.OrdinalIgnoreCase) ? "posts" : "people";

        var vm = new SearchViewModel
        {
            Query = trimmedQuery,
            Tab = activeTab
        };

        ViewData["Title"] = string.IsNullOrWhiteSpace(trimmedQuery)
            ? "Search"
            : $"Search: {trimmedQuery}";

        if (string.IsNullOrWhiteSpace(trimmedQuery))
        {
            return View(vm);
        }

        // SQL LIKE wildcards ko escape karte hain taake user ka input literal search ho
        var escaped = trimmedQuery
            .Replace("[", "[[]")
            .Replace("%", "[%]")
            .Replace("_", "[_]");
        var pattern = $"%{escaped}%";

        // Dono categories ke total counts — badges aur tabs ke liye
        vm.PeopleCount = await _db.Users
            .AsNoTracking()
            .CountAsync(u => EF.Functions.Like(u.UserName!, pattern) || EF.Functions.Like(u.DisplayName, pattern), cancellationToken);

        vm.PostsCount = await _db.Posts
            .AsNoTracking()
            .CountAsync(p => EF.Functions.Like(p.Content, pattern), cancellationToken);

        if (vm.IsPeopleTab)
        {
            // People query: projection + subqueries se single round-trip mein avatar, bio, follower count aur follow status aata hai
            vm.Users = await _db.Users
                .AsNoTracking()
                .Where(u => EF.Functions.Like(u.UserName!, pattern) || EF.Functions.Like(u.DisplayName, pattern))
                .OrderBy(u => u.DisplayName)
                .Take(30)
                .Select(u => new SearchUserViewModel
                {
                    UserId = u.Id,
                    UserName = u.UserName!,
                    DisplayName = u.DisplayName,
                    Bio = u.Bio,
                    AvatarUrl = u.AvatarUrl,
                    FollowerCount = u.Followers.Count,
                    IsFollowing = u.Followers.Any(f => f.FollowerId == myId),
                    IsMe = u.Id == myId
                })
                .ToListAsync(cancellationToken);
        }
        else
        {
            // Posts query FeedService ke through chalti hai jismein unified projection aur pagination mojood hai
            vm.PostsFeed = await _feed.SearchPostsAsync(trimmedQuery, myId, page, cancellationToken);
        }

        return View(vm);
    }
}
