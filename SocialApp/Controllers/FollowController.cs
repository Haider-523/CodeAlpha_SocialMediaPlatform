using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SocialApp.Data;
using SocialApp.Models.Entities;

namespace SocialApp.Controllers;

/// <summary>
/// Follow / unfollow — Likes ki tarah ek hi toggle action.
///
/// Yahan username se kaam hota hai, Id se nahi: form profile page par hai jahan
/// username pehle se URL mein hai, aur user ki Id ko HTML mein daalna bekaar
/// exposure hai.
/// </summary>
[Authorize]
public class FollowController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<FollowController> _logger;

    public FollowController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        ILogger<FollowController> logger)
    {
        _db = db;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(string username, string? returnUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username)) return NotFound();

        var myId = _userManager.GetUserId(User)!;

        // NormalizedUserName par match — usi column par unique index hai.
        var normalized = _userManager.NormalizeName(username);

        var target = await _db.Users
            .AsNoTracking()
            .Where(u => u.NormalizedUserName == normalized)
            .Select(u => new { u.Id, u.UserName, u.DisplayName })
            .FirstOrDefaultAsync(cancellationToken);

        if (target is null) return NotFound();

        // Khud ko follow karna DB level par bhi bekar row hai aur counts jhoote
        // kar deta hai. UI ye button dikhata hi nahi, magar request haath se
        // banai ja sakti hai — is liye check server par bhi.
        if (target.Id == myId) return BadRequest();

        var existing = await _db.Follows
            .FirstOrDefaultAsync(f => f.FollowerId == myId && f.FolloweeId == target.Id, cancellationToken);

        bool following;

        if (existing is not null)
        {
            _db.Follows.Remove(existing);
            following = false;
        }
        else
        {
            _db.Follows.Add(new Follow { FollowerId = myId, FolloweeId = target.Id });
            following = true;
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Unique index (FollowerId, FolloweeId) ne double-click rok diya.
            _logger.LogInformation(ex, "Follow ka race — {Target}", target.UserName);
            _db.ChangeTracker.Clear();
            following = await _db.Follows
                .AnyAsync(f => f.FollowerId == myId && f.FolloweeId == target.Id, cancellationToken);
        }

        var followerCount = await _db.Follows
            .CountAsync(f => f.FolloweeId == target.Id, cancellationToken);

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { following, followerCount });

        TempData["Flash"] = following
            ? $"You're following {target.DisplayName}."
            : $"You unfollowed {target.DisplayName}.";

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Index", "Profile", new { username = target.UserName });
    }
}
