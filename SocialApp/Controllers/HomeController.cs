using System.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SocialApp.Models;
using SocialApp.Models.Entities;
using SocialApp.Models.ViewModels;
using SocialApp.Services;

namespace SocialApp.Controllers;

public class HomeController : Controller
{
    private readonly IFeedService _feed;
    private readonly UserManager<ApplicationUser> _userManager;

    public HomeController(IFeedService feed, UserManager<ApplicationUser> userManager)
    {
        _feed = feed;
        _userManager = userManager;
    }

    /// <summary>
    /// Home feed — jinhein follow karte ho, aur apni posts. Newest first.
    ///
    /// [Authorize] jaan bujh kar nahi lagaya: logged-out banda / par aaye to
    /// login page par phenk dena rukha lagta hai. Usse ek chhota welcome milta hai.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            ViewData["Title"] = "Welcome";
            return View("Welcome");
        }

        var me = await _userManager.GetUserAsync(User);

        // Cookie mojood hai magar user DB se ja chuka hai — cookie saaf karwao.
        if (me is null) return Challenge();

        var vm = await _feed.GetFollowingFeedAsync(me.Id, page, cancellationToken);
        vm.WithViewer(me.DisplayName, me.AvatarUrl);
        vm.Composer = new CreatePostViewModel();

        ViewData["Title"] = "Home";
        return View(vm);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [Route("Home/Error/{statusCode:int?}")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(int? statusCode = null)
    {
        var code = statusCode ?? (Response.StatusCode != 200 ? Response.StatusCode : (int?)null);
        var vm = new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            StatusCode = code
        };

        ViewData["Title"] = vm.Title;
        return View(vm);
    }
}
