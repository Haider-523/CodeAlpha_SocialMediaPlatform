using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SocialApp.Models.Entities;
using SocialApp.Services;

namespace SocialApp.Controllers;

/// <summary>
/// Explore — poori app ki posts, naye se purani.
///
/// FIX #2 — Instagram aur X ka Explore ek algorithm hai: kyun koi cheez saamne
/// aayi, kisi ko nahi pata. Yahan tarteeb sirf waqt ki hai. Koi "suggested",
/// koi "because you liked" nahi. User jo dekh raha hai, samajh sakta hai ke
/// kyun dekh raha hai.
/// </summary>
[Authorize]
public class ExploreController : Controller
{
    private readonly IFeedService _feed;
    private readonly UserManager<ApplicationUser> _userManager;

    public ExploreController(IFeedService feed, UserManager<ApplicationUser> userManager)
    {
        _feed = feed;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
    {
        var myId = _userManager.GetUserId(User);
        if (myId is null) return Challenge();

        var vm = await _feed.GetExploreFeedAsync(myId, page, cancellationToken);

        ViewData["Title"] = "Explore";
        return View(vm);
    }
}
