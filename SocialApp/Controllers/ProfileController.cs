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
/// Profile dekhna aur apna profile edit karna.
/// </summary>
[Authorize]
public class ProfileController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IImageStorage _images;
    private readonly IFeedService _feed;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IImageStorage images,
        IFeedService feed,
        ILogger<ProfileController> logger)
    {
        _db = db;
        _userManager = userManager;
        _images = images;
        _feed = feed;
        _logger = logger;
    }

    /// <summary>
    /// /Profile/Me — seedha apne profile par redirect. Redirect is liye (page
    /// render karne ke bajaye) ke user address bar mein apna shareable URL
    /// dekhe: /u/haider. Instagram/X dono yahi karte hain.
    /// </summary>
    [HttpGet]
    public IActionResult Me()
    {
        var userName = _userManager.GetUserName(User);
        if (userName is null) return Challenge();

        return RedirectToAction(nameof(Index), new { username = userName });
    }

    /// <summary>
    /// /u/haider — kisi ka bhi profile.
    ///
    /// PERFORMANCE: sab kuch EK hi SQL query hai. Counts ko
    /// u.Posts.Count / u.Followers.Count likh kar EF correlated subqueries
    /// banata hai — agar hum pehle user load karte, phir teen alag Count()
    /// chalate, to 4 round-trips hote. AsNoTracking bhi hai kyunki ye
    /// read-only page hai, change tracker ki zaroorat nahi.
    /// </summary>
    [HttpGet("u/{username}")]
    public async Task<IActionResult> Index(string username, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username)) return NotFound();

        var myId = _userManager.GetUserId(User)!;

        // Identity UserName ko NormalizedUserName (upper-case) mein store karta hai.
        // Usi column par match karte hain kyunki uspar unique index mojood hai —
        // UserName par ToUpper() lagate to index bekaar ho jata.
        var normalized = _userManager.NormalizeName(username);

        var vm = await _db.Users
            .AsNoTracking()
            .Where(u => u.NormalizedUserName == normalized)
            .Select(u => new ProfileViewModel
            {
                UserId = u.Id,
                UserName = u.UserName!,
                DisplayName = u.DisplayName,
                Bio = u.Bio,
                AvatarUrl = u.AvatarUrl,
                JoinedAt = u.CreatedAt,
                PostCount = u.Posts.Count,
                FollowerCount = u.Followers.Count,
                FollowingCount = u.Following.Count,
                IsMe = u.Id == myId,
                IsFollowing = u.Followers.Any(f => f.FollowerId == myId)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (vm is null) return NotFound();

        // Posts alag query mein — jaan bujh kar. Header ke counts aur posts ko ek
        // hi query mein jorne se SQL ek cartesian join bana deta hai aur user ke
        // fields har post ke saath dobara aate hain.
        vm.Posts = await _feed.GetUserPostsAsync(vm.UserId, myId, cancellationToken: cancellationToken);

        ViewData["Title"] = vm.DisplayName;
        return View(vm);
    }

    /// <summary>
    /// /u/{username}/followers — is user ke followers ki list.
    /// </summary>
    [HttpGet("u/{username}/followers")]
    public async Task<IActionResult> Followers(string username, int page = 1, CancellationToken cancellationToken = default)
    {
        return await BuildFollowListAsync(username, "followers", page, cancellationToken);
    }

    /// <summary>
    /// /u/{username}/following — ye user jinhein follow karta hai unki list.
    /// </summary>
    [HttpGet("u/{username}/following")]
    public async Task<IActionResult> Following(string username, int page = 1, CancellationToken cancellationToken = default)
    {
        return await BuildFollowListAsync(username, "following", page, cancellationToken);
    }

    private async Task<IActionResult> BuildFollowListAsync(
        string username, string tab, int page, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username)) return NotFound();

        var normalized = _userManager.NormalizeName(username);

        // Target user ke main details aur stats ek query mein
        var target = await _db.Users
            .AsNoTracking()
            .Where(u => u.NormalizedUserName == normalized)
            .Select(u => new
            {
                u.Id,
                u.UserName,
                u.DisplayName,
                u.AvatarUrl,
                FollowerCount = u.Followers.Count,
                FollowingCount = u.Following.Count
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (target is null) return NotFound();

        if (page < 1) page = 1;
        const int pageSize = 20;
        var myId = _userManager.GetUserId(User)!;

        // Followers ya Following query — single round trip with pagination
        IQueryable<ApplicationUser> usersQuery = tab == "followers"
            ? _db.Follows.Where(f => f.FolloweeId == target.Id).OrderByDescending(f => f.CreatedAt).Select(f => f.Follower!)
            : _db.Follows.Where(f => f.FollowerId == target.Id).OrderByDescending(f => f.CreatedAt).Select(f => f.Followee!);

        var users = await usersQuery
            .AsNoTracking()
            .Skip((page - 1) * pageSize)
            .Take(pageSize + 1)
            .Select(u => new SearchUserViewModel
            {
                UserId = u.Id,
                UserName = u.UserName!,
                DisplayName = u.DisplayName,
                AvatarUrl = u.AvatarUrl,
                Bio = u.Bio,
                FollowerCount = u.Followers.Count,
                IsFollowing = u.Followers.Any(f => f.FollowerId == myId),
                IsMe = u.Id == myId
            })
            .ToListAsync(cancellationToken);

        var hasOlder = users.Count > pageSize;
        if (hasOlder) users.RemoveAt(users.Count - 1);

        var vm = new FollowListViewModel
        {
            TargetUserId = target.Id,
            TargetUserName = target.UserName!,
            TargetDisplayName = target.DisplayName,
            TargetAvatarUrl = target.AvatarUrl,
            Tab = tab,
            Users = users,
            FollowerCount = target.FollowerCount,
            FollowingCount = target.FollowingCount,
            Page = page,
            HasOlder = hasOlder
        };

        ViewData["Title"] = tab == "followers"
            ? $"{target.DisplayName}'s Followers"
            : $"{target.DisplayName} is Following";

        return View("FollowList", vm);
    }

    [HttpGet]
    public async Task<IActionResult> Edit()
    {
        var me = await _userManager.GetUserAsync(User);
        if (me is null) return Challenge();

        return View(new EditProfileViewModel
        {
            DisplayName = me.DisplayName,
            Bio = me.Bio,
            CurrentAvatarUrl = me.AvatarUrl,
            UserName = me.UserName!
        });
    }

    /// <summary>
    /// RequestSizeLimit 5 MB: IImageStorage khud 4 MB par mana kar deta hai, magar
    /// woh check TAB chalta hai jab poori file server par aa chuki ho. Ye attribute
    /// bari request ko pipeline mein hi rok deta hai — disk aur memory bachti hai.
    /// (5 aur 4 ka farq form ke baqi fields + multipart boundaries ke liye hai.)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> Edit(EditProfileViewModel model, CancellationToken cancellationToken)
    {
        var me = await _userManager.GetUserAsync(User);
        if (me is null) return Challenge();

        // Display-only fields hamesha server se — form se aayi value par bharosa nahi.
        model.UserName = me.UserName!;
        model.CurrentAvatarUrl = me.AvatarUrl;

        if (!ModelState.IsValid) return View(model);

        string? avatarToDelete = null;

        if (model.Avatar is { Length: > 0 })
        {
            // ImageShape.Avatar → server par beech se square crop + 320×320 + WebP.
            // Is liye user ko crop UI ki zaroorat nahi parti, aur har avatar
            // app mein bilkul ek jaisa dikhta hai.
            var saved = await _images.SaveAsync(model.Avatar, "avatars", ImageShape.Avatar, cancellationToken);
            if (!saved.Succeeded)
            {
                ModelState.AddModelError(nameof(model.Avatar), saved.Error!);
                return View(model);
            }

            avatarToDelete = me.AvatarUrl;
            me.AvatarUrl = saved.Url;
        }
        else if (model.RemoveAvatar)
        {
            avatarToDelete = me.AvatarUrl;
            me.AvatarUrl = null;
        }

        me.DisplayName = model.DisplayName.Trim();
        me.Bio = string.IsNullOrWhiteSpace(model.Bio) ? null : model.Bio.Trim();

        var result = await _userManager.UpdateAsync(me);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            // DB save nahi hui, to nayi file bekaar pari hai — usse hata do.
            if (me.AvatarUrl is not null && me.AvatarUrl != model.CurrentAvatarUrl)
                _images.Delete(me.AvatarUrl);

            return View(model);
        }

        // Purani file DB update ke BAAD delete hoti hai. Pehle karte to update
        // fail hone par user ki tasveer bina wajah ja chuki hoti.
        _images.Delete(avatarToDelete);

        _logger.LogInformation("Profile update hua: {UserName}", me.UserName);

        TempData["Flash"] = "Profile update ho gaya.";
        return RedirectToAction(nameof(Index), new { username = me.UserName });
    }
}
