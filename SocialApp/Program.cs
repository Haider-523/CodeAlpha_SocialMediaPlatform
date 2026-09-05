using DnsClient;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SocialApp.Data;
using SocialApp.Models.Entities;
using SocialApp.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// 1) EF Core + SQL Server LocalDB
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// 2) ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireDigit = true;
    options.User.RequireUniqueEmail = true;

    // Email confirm hone tak login band. Ye do settings ek hi cheez check karti
    // hain (SignInManager.CanSignInAsync mein) — dono set kar rahe hain taake
    // intent code parhne wale ko saaf nazar aaye.
    options.SignIn.RequireConfirmedEmail = true;
    options.SignIn.RequireConfirmedAccount = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
// Ye line email confirmation ke tokens banane wala provider register karti hai.
// Iske bina GenerateEmailConfirmationTokenAsync() runtime par phat jata hai.
.AddDefaultTokenProviders();

// 3) Email bhejna (SMTP)
//
//    Configure<T> "Smtp" section ko SmtpSettings par bind kar deta hai. Values do
//    jagah se aati hain aur IConfiguration unhein merge kar deta hai:
//      Host / Port / UseStartTls / FromName → appsettings.json  (secret nahi)
//      UserName / Password / FromEmail      → dotnet user-secrets (secret — git se bahar)
builder.Services.Configure<SmtpSettings>(
    builder.Configuration.GetSection(SmtpSettings.SectionName));

// Scoped kaafi hai: MailKit ka client har SendAsync mein naya banta hai.
builder.Services.AddScoped<IAppEmailSender, SmtpEmailSender>();

// 4) Email domain validation (DNS).
//    LookupClient SINGLETON hai — ye thread-safe hai aur jawabon ko cache karta
//    hai, to har request par naya banana sirf zaya hoga. Timeout 3 second aur
//    1 retry: registration DNS ke intezaar mein latka nahi rehna chahiye.
builder.Services.AddSingleton<ILookupClient>(_ => new LookupClient(new LookupClientOptions
{
    Timeout = TimeSpan.FromSeconds(3),
    Retries = 1,
    UseCache = true
}));

builder.Services.AddScoped<IEmailDomainValidator, DnsEmailDomainValidator>();

// 4b) Uploaded images (avatars + post images).
//     Interface ke peeche is liye hai ke Azure App Service ka local disk deploy
//     par mit jata hai — wahan sirf ye ek line badal kar Blob Storage par jayenge.
builder.Services.AddScoped<IImageStorage, LocalImageStorage>();

// 4c) Feed queries. Scoped kyunki ye DbContext par depend karta hai, aur
//     DbContext hamesha per-request hota hai.
builder.Services.AddScoped<IFeedService, FeedService>();

// 5) Cookie behaviour: where to send users who are not logged in
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});

var app = builder.Build();

// Startup par SMTP ki halat saaf batao. Ye check pehle nahi tha, is liye missing
// credentials ka pata sirf register karte waqt chalta tha — aur woh bhi ek dabi
// hui log line se.
var smtpSettings = app.Services.GetRequiredService<IOptions<SmtpSettings>>().Value;

if (smtpSettings.IsConfigured)
{
    app.Logger.LogInformation("SMTP ready — {Host}:{Port}, from {From}",
        smtpSettings.Host, smtpSettings.Port, smtpSettings.FromEmail);

    // Development-only: secret ki SHAKL batata hai, secret nahi. 535 BadCredentials
    // ki wajah 90% dafa isi line se pakri jati hai — password mein spaces reh gaye,
    // ya UserName aur FromEmail alag hain.
    if (app.Environment.IsDevelopment())
        app.Logger.LogInformation("SMTP check → {Shape}", smtpSettings.DescribeSecretShape());
}
else
{
    app.Logger.LogWarning(
        "SMTP CONFIGURED NAHI HAI — confirmation aur welcome emails nahi jayengi. " +
        "Ye teen commands SocialApp folder mein chalao: " +
        "dotnet user-secrets set \"Smtp:UserName\" \"you@gmail.com\" | " +
        "dotnet user-secrets set \"Smtp:Password\" \"<16-char app password>\" | " +
        "dotnet user-secrets set \"Smtp:FromEmail\" \"you@gmail.com\". " +
        "Filhaal Development mein confirmation link screen par nazar aa jayega.");
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// 404 / 403 status code hone par branded error page render karein
app.UseStatusCodePagesWithReExecute("/Home/Error/{0}");

app.UseHttpsRedirection();

// Security Headers: Clickjacking defense, MIME sniff block, strict referrer policy
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Frame-Options", "SAMEORIGIN");
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    await next();
});

app.UseRouting();

app.UseAuthentication();   // Tum kaun ho?
app.UseAuthorization();    // Tumhein ijazat hai?

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();