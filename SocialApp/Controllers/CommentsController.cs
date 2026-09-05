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
/// Comment likhna aur delete karna. Dikhane ka kaam PostsController.Details
/// karta hai, is liye yahan sirf POST actions hain.
/// </summary>
[Authorize]
public class CommentsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IFeedService _feed;
    private readonly ILogger<CommentsController> _logger;

    public CommentsController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IFeedService feed,
        ILogger<CommentsController> logger)
    {
        _db = db;
        _userManager = userManager;
        _feed = feed;
        _logger = logger;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateCommentViewModel model, CancellationToken cancellationToken)
    {
        var me = await _userManager.GetUserAsync(User);
        if (me is null) return Challenge();

        // Post mojood hai? Delete ho chuki post par comment banane ki koshish
        // FK violation aur 500 deti — 404 zyada sahi jawab hai.
        if (!await _db.Posts.AnyAsync(p => p.Id == model.PostId, cancellationToken))
            return NotFound();

        if (!ModelState.IsValid)
            return await RenderDetailsWithErrorsAsync(me, model, cancellationToken);

        _db.Comments.Add(new Comment
        {
            PostId = model.PostId,
            UserId = me.Id,
            Content = model.Content.Trim(),
            CreatedAt = DateTime.UtcNow
        });

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Comment save nahi hua — post #{PostId}", model.PostId);
            ModelState.AddModelError(string.Empty, "Couldn't save your comment. Please try again.");
            return await RenderDetailsWithErrorsAsync(me, model, cancellationToken);
        }

        // POST → Redirect → GET, warna refresh par wahi comment dobara chala jata hai.
        // Fragment #comments: user ko wahin le jao jahan uska comment aaya hai,
        // page ke sabse upar nahi.
        var url = Url.Action(nameof(PostsController.Details), "Posts", new { id = model.PostId }) ?? "/";
        return Redirect($"{url}#comments");
    }

    /// <summary>
    /// Apna comment, ya apni post par kisi ka bhi comment.
    ///
    /// Doosri shart moderation ke liye hai: apni post par aane wale spam ko hatane
    /// ka haq post ke malik ka hona chahiye.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var myId = _userManager.GetUserId(User)!;

        // Ijazat query ka HISSA hai — jis comment par haq nahi, wo is query mein
        // hi nahi aata. Pehle laa kar phir "if" lagane se bhoolna aasan hai.
        var comment = await _db.Comments
            .Include(c => c.Post)
            .FirstOrDefaultAsync(
                c => c.Id == id && (c.UserId == myId || c.Post!.UserId == myId),
                cancellationToken);

        // NotFound, Forbid nahi — Forbid batata hai ke "comment mojood hai magar
        // tumhara nahi", aur ye khud ek information leak hai.
        if (comment is null) return NotFound();

        var postId = comment.PostId;

        _db.Comments.Remove(comment);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Comment #{CommentId} delete hua — {UserId}", id, myId);

        TempData["Flash"] = "Comment deleted.";
        return RedirectToAction(nameof(PostsController.Details), "Posts", new { id = postId });
    }

    /// <summary>
    /// Validation fail hui to poora detail page dobara banate hain — redirect
    /// nahi, warna ModelState (aur user ka likha hua text) zaya ho jata hai.
    /// </summary>
    private async Task<IActionResult> RenderDetailsWithErrorsAsync(
        ApplicationUser me, CreateCommentViewModel model, CancellationToken cancellationToken)
    {
        var post = await _feed.GetPostAsync(model.PostId, me.Id, cancellationToken);
        if (post is null) return NotFound();

        var vm = new PostDetailViewModel
        {
            Post = post,
            Comments = await _feed.GetCommentsAsync(model.PostId, me.Id, cancellationToken),
            Composer = model
        }.WithViewer(me.DisplayName, me.AvatarUrl);

        ViewData["Title"] = $"{post.AuthorDisplayName}'s post";
        return View("~/Views/Posts/Details.cshtml", vm);
    }
}
