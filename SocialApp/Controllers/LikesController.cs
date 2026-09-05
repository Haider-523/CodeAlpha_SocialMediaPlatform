using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SocialApp.Data;
using SocialApp.Models.Entities;

namespace SocialApp.Controllers;

/// <summary>
/// Like / unlike — ek hi action, toggle.
///
/// Do alag actions (Like aur Unlike) banane ka faida nahi: client ko pehle se
/// pata hona parta ke abhi kya state hai, aur do tabs khuli hon to wo state
/// purani ho sakti hai. Toggle hamesha DB ki asli haalat dekh kar faisla karta
/// hai, is liye galat state se ghalat result nahi milta.
///
/// Response do shakl mein: AJAX ho to JSON (count wapas, page reload nahi),
/// warna redirect. Matlab JS band ho to bhi like kaam karta hai — feature
/// JavaScript par depend nahi karta.
/// </summary>
[Authorize]
public class LikesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<LikesController> _logger;

    public LikesController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        ILogger<LikesController> logger)
    {
        _db = db;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int postId, string? returnUrl, CancellationToken cancellationToken)
    {
        var myId = _userManager.GetUserId(User)!;

        // Post ka wujood pehle check — warna delete ho chuki post par like row
        // banane ki koshish FK violation aur 500 deti. AnyAsync sirf EXISTS
        // chalata hai, poora row nahi laata.
        if (!await _db.Posts.AnyAsync(p => p.Id == postId, cancellationToken))
            return NotFound();

        var existing = await _db.Likes
            .FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == myId, cancellationToken);

        bool liked;

        if (existing is not null)
        {
            _db.Likes.Remove(existing);
            liked = false;
        }
        else
        {
            _db.Likes.Add(new Like { PostId = postId, UserId = myId });
            liked = true;
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Do tabs (ya double-click) se ek hi waqt like: unique index
            // (PostId, UserId) ne doosri insert rok di. Ye user ke liye error
            // nahi hai — uski marzi pehle se poori ho chuki hai. Change tracker
            // saaf kar ke DB se asli state pooch lete hain.
            _logger.LogInformation(ex, "Like ka race — post #{PostId}", postId);
            _db.ChangeTracker.Clear();
            liked = await _db.Likes.AnyAsync(l => l.PostId == postId && l.UserId == myId, cancellationToken);
        }

        var count = await _db.Likes.CountAsync(l => l.PostId == postId, cancellationToken);

        // AJAX: sirf naya count wapas. Poora page dobara banane ki zaroorat nahi.
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { liked, count });

        return SafeRedirect(returnUrl, postId);
    }

    /// <summary>
    /// JS band ho to redirect. Fragment #post-N is liye ke user ko wapas usi post
    /// par le jaye — warna 10 posts wale feed mein wo sabse upar phenk diya jata.
    /// </summary>
    private IActionResult SafeRedirect(string? returnUrl, int postId)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect($"{returnUrl}#post-{postId}");

        return RedirectToAction("Index", "Home");
    }
}
