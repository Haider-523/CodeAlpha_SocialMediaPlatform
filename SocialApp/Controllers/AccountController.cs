using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using SocialApp.Models.Entities;
using SocialApp.Models.ViewModels;
using SocialApp.Services;

namespace SocialApp.Controllers;

/// <summary>
/// Register / Login / Logout + email confirmation. Ye Identity ki ready-made
/// scaffolded UI use nahi karta — sab kuch apna hai, taake poora flow samajh
/// mein aaye aur design humare hi system se match kare.
/// </summary>
public class AccountController : Controller
{
    // Identity ke do managers — Program.cs mein AddIdentity() ne inhein DI
    // container mein register kiya tha, isliye constructor mein mil jaate hain.
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    // Note: hum concrete SmtpEmailSender par depend nahi kar rahe, interface par
    // kar rahe hain. Kal Brevo/SendGrid par jana ho to sirf Program.cs badlegi.
    private readonly IAppEmailSender _emailSender;
    private readonly IEmailDomainValidator _domainValidator;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IAppEmailSender emailSender,
        IEmailDomainValidator domainValidator,
        IWebHostEnvironment env,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _emailSender = emailSender;
        _domainValidator = domainValidator;
        _env = env;
        _logger = logger;
    }

    // ══════════════════════════ REGISTER ══════════════════════════

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register(string? returnUrl = null)
    {
        // Already logged in banda register page par nahi jana chahiye.
        if (_signInManager.IsSignedIn(User))
            return RedirectToAction("Index", "Home");

        ViewData["ReturnUrl"] = returnUrl;
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]   // CSRF protection — form ka hidden token check karta hai
    public async Task<IActionResult> Register(RegisterViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        // 1) DataAnnotations validation (RegisterViewModel ke rules).
        if (!ModelState.IsValid)
            return View(model);

        // 2) Domain waqai mail le sakta hai? [EmailAddress] sirf shakal dekhta hai,
        //    to "you@smal.comm" us se guzar jata hai. DNS se poochte hain ke is
        //    domain ka mail server hai ya nahi.
        var domainCheck = await _domainValidator.CheckAsync(model.Email);
        if (domainCheck.Status == EmailDomainCheck.NoMailServer)
        {
            ModelState.AddModelError(nameof(model.Email), domainCheck.Suggestion is null
                ? "We can't find a mail server for that domain. Check the spelling."
                : $"We can't find a mail server for that domain. Did you mean {domainCheck.Suggestion}?");

            return View(model);
        }

        // 3) Entity banao. NOTE: password yahan set NAHI karte —
        //    CreateAsync() usay khud hash karke PasswordHash column mein daalta hai.
        //    Hum plain-text password kahin store nahi karte.
        var user = new ApplicationUser
        {
            UserName = model.UserName,
            Email = model.Email,
            DisplayName = model.DisplayName,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (!result.Succeeded)
        {
            // Identity ki apni errors (duplicate email/username, weak password waghera)
            // ko form ke summary mein daal do.
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return View(model);
        }

        _logger.LogInformation("Naya account bana: {UserName}", user.UserName);

        // 4) Ab SignInAsync() NAHI karte (pehle karte thay).
        //
        //    Wajah do hain:
        //    a) Foran login kara dena confirmation ka maqsad hi khatam kar deta hai.
        //    b) SignInAsync() CanSignInAsync() check ko bypass karta hai, to woh
        //       RequireConfirmedEmail = true ke bawajood andar ghus jata.
        var send = await SendConfirmationEmailAsync(user);

        // Development mein SMTP ki asli error aur confirmation link screen par
        // dikha dete hain — warna SMTP theek hone tak flow test hi nahi ho sakta,
        // aur wajah sirf logs mein dabi reh jati hai.
        if (_env.IsDevelopment())
        {
            TempData["DevConfirmUrl"] = send.ConfirmUrl;
            if (send.Error is not null)
                TempData["DevEmailError"] = send.Error;
        }

        return RedirectToAction(nameof(RegisterConfirmation),
            new { email = user.Email, sent = send.Sent });
    }

    /// <summary>
    /// "Check your inbox" page. Email query string mein hai (TempData mein nahi)
    /// taake F5 par page toote nahi — Identity ki apni scaffolding bhi yehi karti hai.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public IActionResult RegisterConfirmation(string? email, bool sent = false)
    {
        // Ye page sirf register ke baad matlab rakhta hai.
        if (string.IsNullOrWhiteSpace(email))
            return RedirectToAction(nameof(Login));

        return View(new RegisterConfirmationViewModel
        {
            Email = email,
            EmailSent = sent,

            // Ye dono sirf Development mein bhare jate hain (Register action mein
            // IsDevelopment() check hai), is liye production mein null rehte hain.
            DevEmailError = TempData["DevEmailError"] as string,
            DevConfirmUrl = TempData["DevConfirmUrl"] as string
        });
    }

    // ══════════════════════════ CONFIRM EMAIL ══════════════════════════

    /// <summary>
    /// Email mein bheje gaye link ka landing point. Yahi woh jagah hai jahan
    /// sabit hota hai ke mailbox asli hai aur usi banday ka hai.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail(string? userId, string? code)
    {
        var vm = new ConfirmEmailViewModel();

        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(code))
        {
            vm.Heading = "This link isn't valid";
            vm.Message = "The confirmation link is missing some information. Request a fresh one below.";
            return View(vm);
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            vm.Heading = "This link isn't valid";
            vm.Message = "We couldn't find an account for this link. Request a fresh one below.";
            return View(vm);
        }

        if (user.EmailConfirmed)
        {
            vm.Succeeded = true;
            vm.AlreadyConfirmed = true;
            vm.Heading = "Already confirmed";
            vm.Message = "This email was confirmed earlier, so there's nothing left to do. Log in whenever you're ready.";
            return View(vm);
        }

        // Token URL mein Base64Url shakal mein tha — wapas asli string banao.
        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        }
        catch (FormatException)
        {
            vm.Heading = "This link is damaged";
            vm.Message = "Some email apps break long links across lines. Request a fresh one below.";
            return View(vm);
        }

        var result = await _userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
        {
            vm.Heading = "This link has expired";
            vm.Message = "Confirmation links last 24 hours and work only once. Request a fresh one below.";
            return View(vm);
        }

        _logger.LogInformation("Email confirm ho gaya: {UserName}", user.UserName);

        // Yahan — confirm hone ke BAAD — welcome email jata hai. Register ke waqt
        // nahi, warna woh fake/typo addresses par bhi jata aur Gmail ki nazar mein
        // humari sender reputation kharab hoti.
        await SendWelcomeEmailAsync(user);

        vm.Succeeded = true;
        vm.Heading = "Email confirmed";
        vm.Message = "Your account is ready. Log in to start posting.";
        return View(vm);
    }

    // ══════════════════════════ RESEND CONFIRMATION ══════════════════════════

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ResendConfirmation()
    {
        // POST ke baad redirect hone par ye flag set hota hai (PRG pattern),
        // is liye F5 dabane par form dobara submit nahi hota.
        ViewData["Sent"] = TempData["ResendDone"] as bool? ?? false;
        ViewData["DevEmailError"] = TempData["DevEmailError"] as string;
        ViewData["DevConfirmUrl"] = TempData["DevConfirmUrl"] as string;
        return View(new ResendConfirmationViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendConfirmation(ResendConfirmationViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);

        // Sirf tab bhejo jab account maujood ho AUR abhi unconfirmed ho.
        if (user is not null && !user.EmailConfirmed)
        {
            var send = await SendConfirmationEmailAsync(user);

            if (_env.IsDevelopment())
            {
                TempData["DevConfirmUrl"] = send.ConfirmUrl;
                if (send.Error is not null)
                    TempData["DevEmailError"] = send.Error;
            }
        }

        // SECURITY: jawab hamesha ek hi hota hai, chahe account mila ho ya na mila ho.
        // Warna koi is form ko email checker ki tarah use kar ke pata kar leta ke
        // kaun kaun SocialApp par registered hai.
        TempData["ResendDone"] = true;
        return RedirectToAction(nameof(ResendConfirmation));
    }

    // ══════════════════════════ LOGIN ══════════════════════════

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (_signInManager.IsSignedIn(User))
            return RedirectToAction("Index", "Home");

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
            return View(model);

        // PasswordSignInAsync ka pehla parameter UserName hota hai, email nahi.
        // Humara form email leta hai, to pehle us email ka user dhoondte hain.
        var user = await _userManager.FindByEmailAsync(model.Email);

        if (user is not null)
        {
            var result = await _signInManager.PasswordSignInAsync(
                user.UserName!,
                model.Password,
                isPersistent: model.RememberMe,
                lockoutOnFailure: true);   // ghalat try count karo (brute-force se bachao)

            if (result.Succeeded)
                return RedirectToLocal(returnUrl);

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty,
                    "Too many failed attempts. Try again in a few minutes.");
                return View(model);
            }

            // IsNotAllowed = RequireConfirmedEmail fail hua, yani email confirm nahi hui.
            //
            // Ek nazuk baat: Identity ye check PASSWORD verify karne se PEHLE karta hai,
            // to yahan seedha "email confirm nahi hui" keh dena ghalat password wale
            // ajnabi ko bhi bata deta ke ye email registered hai. Is liye CheckPasswordAsync
            // se pehle tasdeeq karte hain ke banda waqai isi account ka malik hai.
            if (result.IsNotAllowed && await _userManager.CheckPasswordAsync(user, model.Password))
            {
                ModelState.AddModelError(string.Empty,
                    "Your email isn't confirmed yet. Open the link we sent you, then log in.");
                ViewData["ShowResend"] = true;
                return View(model);
            }
        }

        // SECURITY: jaan-boojh kar ek hi generic message — warna attacker ko
        // pata chal jayega ke kaun sa email registered hai (user enumeration).
        ModelState.AddModelError(string.Empty, "Email or password is incorrect.");
        return View(model);
    }

    // ══════════════════════════ LOGOUT ══════════════════════════

    /// <summary>
    /// POST-only, jaan-boojh kar. Agar logout GET hota to koi bhi
    /// &lt;img src="/Account/Logout"&gt; laga kar tumhein log out kara sakta tha.
    /// </summary>
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    // ══════════════════════════ ACCESS DENIED ══════════════════════════

    /// <summary>Program.cs ke AccessDeniedPath ne is route ka wada kiya tha.</summary>
    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    // ══════════════════════════ HELPERS ══════════════════════════

    /// <summary>
    /// Confirmation token banata hai, usay ek absolute URL mein lapetta hai,
    /// aur email bhej deta hai.
    /// </summary>
    /// <returns>
    /// Sent = email nikli ya nahi. Error = nakami ki asli wajah (Development mein
    /// user ko dikhate hain). ConfirmUrl = woh link jo email mein gaya —
    /// Development mein screen par dikha dete hain taake SMTP theek hone se pehle
    /// bhi poora flow test ho sake.
    /// </returns>
    private async Task<(bool Sent, string? Error, string ConfirmUrl)> SendConfirmationEmailAsync(ApplicationUser user)
    {
        // Ye token cryptographically signed hai aur user ke SecurityStamp se
        // bandha hua hai — is liye guess nahi kiya ja sakta, ek dafa chalta hai,
        // aur 24 ghante baad expire ho jata hai. Database mein kuch store nahi hota.
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);

        // Token Base64 hai, jis mein '+' aur '/' aate hain. URL mein '+' space ban
        // jata hai — link chup-chaap toot jata. Is liye Base64Url mein encode karte
        // hain (yehi Identity ki apni scaffolding bhi karti hai).
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        // protocol dena zaroori hai — warna Url.Action relative "/Account/..." deta
        // hai, jo email mein bekar hai.
        var confirmUrl = Url.Action(
            action: nameof(ConfirmEmail),
            controller: "Account",
            values: new { userId = user.Id, code },
            protocol: Request.Scheme)!;

        if (_env.IsDevelopment())
            _logger.LogInformation("DEV confirmation link {Email} ke liye: {Url}", user.Email, confirmUrl);

        try
        {
            await _emailSender.SendAsync(
                user.Email!,
                user.DisplayName,
                EmailTemplates.Confirmation(user.DisplayName, confirmUrl));

            return (true, null, confirmUrl);
        }
        catch (Exception ex)
        {
            // Email fail hone par account delete nahi karte — woh ban chuka hai,
            // user "Send the link again" se dobara koshish kar sakta hai.
            _logger.LogError(ex, "Confirmation email {Email} ko bhej nahi paye.", user.Email);

            // Sirf message, poora stack trace nahi — ye Development mein screen par jata hai.
            return (false, ex.Message, confirmUrl);
        }
    }

    private async Task SendWelcomeEmailAsync(ApplicationUser user)
    {
        var homeUrl = Url.Action("Index", "Home", values: null, protocol: Request.Scheme)!;

        try
        {
            await _emailSender.SendAsync(
                user.Email!,
                user.DisplayName,
                EmailTemplates.Welcome(user.DisplayName, homeUrl));
        }
        catch (Exception ex)
        {
            // Welcome email "nice to have" hai. Ye fail ho to bhi email confirm
            // ho chuki hai — user ko error dikhana bekar aur confusing hoga.
            _logger.LogError(ex, "Welcome email {Email} ko bhej nahi paye.", user.Email);
        }
    }

    /// <summary>
    /// Open-redirect guard. Url.IsLocalUrl() ke bina koi
    /// /Account/Login?returnUrl=https://phishing-site.com bhej kar
    /// login ke baad user ko bahar redirect kara sakta tha.
    /// </summary>
    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Index", "Home");
    }
}
