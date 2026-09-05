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
/// Post banana, dekhna aur delete karna.
///
/// Sirf ek GET hai — Details (permalink). Baqi sab POST hain: GET se kabhi data
/// nahi badalna chahiye, warna ek &lt;img src&gt; bhi tumhari post uda sakti hai.
/// </summary>
[Authorize]
public class PostsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IImageStorage _images;
    private readonly IFeedService _feed;
    private readonly ILogger<PostsController> _logger;

    public PostsController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IImageStorage images,
        IFeedService feed,
        ILogger<PostsController> logger)
    {
        _db = db;
        _userManager = userManager;
        _images = images;
        _feed = feed;
        _logger = logger;
    }

    /// <summary>
    /// /p/12 — ek post ka apna page, uske saare comments ke saath.
    ///
    /// Route "p/{id:int}" is liye ke share kiya jane wala link chhota rahe.
    /// {id:int} constraint zaroori hai: iske bina /p/abc bhi is action tak aata
    /// aur model binding fail hone par 400 ki jagah ek adhoora page banta.
    /// </summary>
    [HttpGet("p/{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var me = await _userManager.GetUserAsync(User);
        if (me is null) return Challenge();

        var post = await _feed.GetPostAsync(id, me.Id, cancellationToken);
        if (post is null) return NotFound();

        var vm = new PostDetailViewModel
        {
            Post = post,
            Comments = await _feed.GetCommentsAsync(id, me.Id, cancellationToken),
            Composer = new CreateCommentViewModel { PostId = id }
        }.WithViewer(me.DisplayName, me.AvatarUrl);

        ViewData["Title"] = $"{post.AuthorDisplayName}'s post";
        return View(vm);
    }

    /// <summary>
    /// Composer ka POST target.
    ///
    /// RequestSizeLimit 5 MB: LocalImageStorage khud 4 MB par mana karta hai magar
    /// wo check tab chalta hai jab file server par aa chuki ho. Ye attribute bari
    /// request ko pipeline mein hi rok deta hai.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> Create(CreatePostViewModel model, CancellationToken cancellationToken)
    {
        var me = await _userManager.GetUserAsync(User);
        if (me is null) return Challenge();

        if (!ModelState.IsValid)
            return await RenderFeedWithErrorsAsync(me, model, cancellationToken);

        string? imageUrl = null;
        int? imageWidth = null, imageHeight = null;

        if (model.Image is { Length: > 0 })
        {
            // ImageShape.Post — crop nahi hota, composition user ki hai. Sirf
            // 1280px se bari tasveer chhoti hoti hai aur WebP ban jati hai.
            var saved = await _images.SaveAsync(model.Image, "posts", ImageShape.Post, cancellationToken);

            if (!saved.Succeeded)
            {
                ModelState.AddModelError(nameof(model.Image), saved.Error!);
                return await RenderFeedWithErrorsAsync(me, model, cancellationToken);
            }

            imageUrl = saved.Url;
            imageWidth = saved.Width;
            imageHeight = saved.Height;
        }

        var post = new Post
        {
            UserId = me.Id,
            Content = model.Content.Trim(),
            ImageUrl = imageUrl,
            ImageWidth = imageWidth,
            ImageHeight = imageHeight,
            CreatedAt = DateTime.UtcNow
        };

        _db.Posts.Add(post);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // DB ne mana kar diya to disk par pari nayi file orphan hai — hata do.
            _images.Delete(imageUrl);
            _logger.LogError(ex, "Post save nahi hui — {UserId}", me.Id);

            ModelState.AddModelError(string.Empty, "Couldn't save your post. Please try again.");
            return await RenderFeedWithErrorsAsync(me, model, cancellationToken);
        }

        _logger.LogInformation("Post #{PostId} bani — {UserName}", post.Id, me.UserName);

        // POST → Redirect → GET. Is ke bina user refresh dabaye to browser wahi
        // post dobara bhej deta hai aur "Confirm form resubmission" dikhata hai.
        TempData["Flash"] = "Posted.";
        return RedirectToAction("Index", "Home");
    }

    /// <summary>
    /// /p/12/edit — Apni post edit karne ka GET form.
    /// Authorization query ka hissa hai: doosre ki post par seedha NotFound() milta hai.
    /// </summary>
    [HttpGet("p/{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, string? returnUrl, CancellationToken cancellationToken)
    {
        var myId = _userManager.GetUserId(User)!;

        var post = await _db.Posts
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == myId, cancellationToken);

        if (post is null) return NotFound();

        var vm = new EditPostViewModel
        {
            Id = post.Id,
            Content = post.Content,
            CurrentImageUrl = post.ImageUrl,
            ImageWidth = post.ImageWidth,
            ImageHeight = post.ImageHeight,
            ReturnUrl = returnUrl
        };

        ViewData["Title"] = "Edit post";
        return View(vm);
    }

    /// <summary>
    /// Post update ka POST handler.
    /// Text update, image removal/replacement, aur UpdatedAt = DateTime.UtcNow set karta hai.
    /// </summary>
    [HttpPost("p/{id:int}/edit")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> Edit(int id, EditPostViewModel model, CancellationToken cancellationToken)
    {
        if (id != model.Id) return BadRequest();

        var myId = _userManager.GetUserId(User)!;

        var post = await _db.Posts
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == myId, cancellationToken);

        if (post is null) return NotFound();

        // Server se current image properties preserve karte hain
        model.CurrentImageUrl = post.ImageUrl;
        model.ImageWidth = post.ImageWidth;
        model.ImageHeight = post.ImageHeight;

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Edit post";
            return View(model);
        }

        string? imageToDelete = null;

        if (model.NewImage is { Length: > 0 })
        {
            // Nayi image upload — ImageShape.Post pipeline (max 1280px, WebP, EXIF stripped)
            var saved = await _images.SaveAsync(model.NewImage, "posts", ImageShape.Post, cancellationToken);

            if (!saved.Succeeded)
            {
                ModelState.AddModelError(nameof(model.NewImage), saved.Error!);
                ViewData["Title"] = "Edit post";
                return View(model);
            }

            imageToDelete = post.ImageUrl;
            post.ImageUrl = saved.Url;
            post.ImageWidth = saved.Width;
            post.ImageHeight = saved.Height;
        }
        else if (model.RemoveImage)
        {
            imageToDelete = post.ImageUrl;
            post.ImageUrl = null;
            post.ImageWidth = null;
            post.ImageHeight = null;
        }

        post.Content = model.Content.Trim();
        post.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Nayi image upload ho chuki thi magar DB save fail ho gaya to orphaned file delete kar do
            if (model.NewImage is { Length: > 0 } && post.ImageUrl != model.CurrentImageUrl)
            {
                _images.Delete(post.ImageUrl);
            }

            _logger.LogError(ex, "Post #{PostId} edit fail hui — {UserId}", id, myId);
            ModelState.AddModelError(string.Empty, "Couldn't save your changes. Please try again.");
            ViewData["Title"] = "Edit post";
            return View(model);
        }

        // DB save kamyab hone ke BAAD purani image delete karte hain
        _images.Delete(imageToDelete);

        _logger.LogInformation("Post #{PostId} update hui — {UserId}", id, myId);

        TempData["Flash"] = "Post updated.";
        return SafeRedirect(model.ReturnUrl);
    }

    /// <summary>
    /// Apni post delete karna. returnUrl is liye hai ke user profile se delete
    /// kare to profile par wapas aaye, feed par nahi.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, string? returnUrl, CancellationToken cancellationToken)
    {
        var myId = _userManager.GetUserId(User)!;

        // Authorization query ka HISSA hai — pehle post laa kar phir "if (owner)"
        // check karne se ek race window khulti hai, aur bhoolna bhi aasan hai.
        // Yahan doosre ki post is query mein hi nahi aati.
        var post = await _db.Posts
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == myId, cancellationToken);

        if (post is null)
        {
            // NotFound, Forbid nahi. Forbid batata hai ke "post mojood hai magar
            // tumhari nahi" — ye information leak hai.
            return NotFound();
        }

        var imageToDelete = post.ImageUrl;

        _db.Posts.Remove(post);
        await _db.SaveChangesAsync(cancellationToken);

        // File DB ke BAAD delete hoti hai. Pehle karte aur DB fail hota to post
        // zinda reh jati par uski tasveer gayab hoti — toota hua record.
        _images.Delete(imageToDelete);

        _logger.LogInformation("Post #{PostId} delete hui — {UserId}", id, myId);

        TempData["Flash"] = "Post deleted.";
        return SafeRedirect(returnUrl);
    }

    /// <summary>
    /// Composer ki validation fail hui to Home ka feed dobara bana kar wahi view
    /// wapas karte hain — redirect nahi. Redirect se ModelState (aur user ka
    /// likha hua text) zaya ho jata hai.
    /// </summary>
    private async Task<IActionResult> RenderFeedWithErrorsAsync(
        ApplicationUser me, CreatePostViewModel model, CancellationToken cancellationToken)
    {
        var feed = await _feed.GetFollowingFeedAsync(me.Id, 1, cancellationToken);
        feed.WithViewer(me.DisplayName, me.AvatarUrl);
        feed.Composer = model;

        ViewData["Title"] = "Home";
        return View("~/Views/Home/Index.cshtml", feed);
    }

    /// <summary>
    /// Open-redirect guard. Bahar ki URL par bharosa nahi — bina is check ke koi
    /// /Posts/Delete?returnUrl=https://phishing.site bana kar bhej sakta hai.
    /// </summary>
    private IActionResult SafeRedirect(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl)
            : RedirectToAction("Index", "Home");
}
