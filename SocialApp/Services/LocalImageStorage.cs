using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace SocialApp.Services;

/// <summary>
/// Images ko wwwroot/uploads/&lt;folder&gt;/ mein save karta hai — raw file copy
/// nahi karta, usse dobara encode karta hai.
///
/// SECURITY:
///   • 4 MB request limit (controller) + 4 MB yahan.
///   • Extension whitelist — blacklist ("exe hatao") kabhi kaam nahi karti.
///   • Image.IdentifyAsync — asli check. Sirf headers parhta hai; agar file
///     image nahi hai to yahin fail ho jati hai. Koi "shell.aspx" ko "photo.jpg"
///     naam de kar bhej sakta hai, extension jhoot bol sakti hai, decoder nahi.
///   • Pixel limit — 40 megapixel. Iske bina ek 200 KB ki "decompression bomb"
///     PNG server ki poori RAM kha sakti hai (50000×50000 = 10 GB bitmap).
///   • Filename hamesha naya GUID — user ka diya hua naam KABHI use nahi hota,
///     is liye path traversal aur overwrite dono namumkin hain.
///
/// PERFORMANCE aur PRIVACY (dobara encode karne ke faide):
///   • Phone ki 4 MB JPEG feed mein ~150 KB WebP ban jati hai — mobile data aur
///     load time dono bachte hain.
///   • AutoOrient() EXIF rotation apply kar deta hai, warna phone se li hui
///     tasveerein terhi dikhti hain.
///   • ExifProfile null — phone ki tasveer mein GPS coordinates hote hain.
///     Unhein bina soche serve karna asli privacy leak hai.
/// </summary>
public class LocalImageStorage : IImageStorage
{
    private const long MaxBytes = 4 * 1024 * 1024;      // 4 MB
    private const long MaxPixels = 40_000_000;          // 40 MP
    private const int AvatarSize = 320;
    private const int PostMaxWidth = 1280;
    private const string UploadsRoot = "uploads";

    private static readonly string[] AllowedExtensions =
        [".jpg", ".jpeg", ".png", ".webp"];

    private readonly IWebHostEnvironment _env;
    private readonly ILogger<LocalImageStorage> _logger;

    public LocalImageStorage(IWebHostEnvironment env, ILogger<LocalImageStorage> logger)
    {
        _env = env;
        _logger = logger;
    }

    public async Task<ImageSaveResult> SaveAsync(
        IFormFile file, string folder, ImageShape shape, CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
            return Fail("No file was selected.");

        if (file.Length > MaxBytes)
            return Fail($"Image must be smaller than {MaxBytes / (1024 * 1024)} MB.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            return Fail("Only JPG, PNG or WebP images are allowed.");

        // Folder name hum khud dete hain, magar guard phir bhi — kal koi route se
        // aayi value pass kar de to path traversal na ho.
        if (folder.Length == 0 || folder.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            return Fail("Storage folder is not valid.");

        // ── 1. Headers se pehchano (poori file memory mein load kiye bina) ──
        try
        {
            await using var probe = file.OpenReadStream();
            var info = await Image.IdentifyAsync(probe, cancellationToken);

            if ((long)info.Width * info.Height > MaxPixels)
                return Fail("That image is too large. Please pick a smaller resolution.");
        }
        catch (UnknownImageFormatException)
        {
            return Fail("That file isn't an image. Please pick a real photo.");
        }
        catch (InvalidImageContentException)
        {
            return Fail("That image looks corrupted. Please try another file.");
        }

        var directory = Path.Combine(_env.WebRootPath, UploadsRoot, folder);
        Directory.CreateDirectory(directory);

        // Output hamesha WebP — is liye extension input se nahi, hum se aati hai.
        var fileName = $"{Guid.NewGuid():N}.webp";
        var fullPath = Path.Combine(directory, fileName);

        try
        {
            await using var source = file.OpenReadStream();
            using var image = await Image.LoadAsync(source, cancellationToken);

            // Pehle orientation. Phone ki portrait tasveer pixels ko landscape
            // mein rakh kar EXIF mein "isse 90° ghuma kar dikhao" likh deta hai.
            // AutoOrient wo rotation asli pixels par apply kar deta hai — is ke
            // bina tasveer feed mein terhi dikhti hai, aur agla step (resize) bhi
            // ghalat rukh par lag jata hai.
            image.Mutate(c => c.AutoOrient());

            if (shape == ImageShape.Avatar)
            {
                // ResizeMode.Crop = beech se square kaat kar 320×320. Yahi wajah
                // hai ke alag-alag shakl ki tasveerein feed mein ek jaisi lagti hain,
                // aur user ko khud crop karne ki zaroorat nahi parti.
                image.Mutate(c => c.Resize(new ResizeOptions
                {
                    Size = new Size(AvatarSize, AvatarSize),
                    Mode = ResizeMode.Crop,
                    Position = AnchorPositionMode.Center
                }));
            }
            else if (image.Width > PostMaxWidth)
            {
                // Post image crop NAHI hoti — composition user ki hai. Sirf bari
                // tasveer chhoti hoti hai; choti ko upscale karne ka faida nahi
                // (file bari, quality utni hi).
                image.Mutate(c => c.Resize(new ResizeOptions
                {
                    Size = new Size(PostMaxWidth, 0),
                    Mode = ResizeMode.Max
                }));
            }

            // GPS aur camera info hata do.
            image.Metadata.ExifProfile = null;
            image.Metadata.IptcProfile = null;
            image.Metadata.XmpProfile = null;

            await image.SaveAsync(fullPath, new WebpEncoder { Quality = 80 }, cancellationToken);

            // URL mein hamesha forward slash — Windows ka backslash browser mein nahi chalta.
            // Naap resize ke BAAD li ja rahi hai — wahi DB mein jayegi.
            return new ImageSaveResult(
                true, $"/{UploadsRoot}/{folder}/{fileName}", null, image.Width, image.Height);
        }
        catch (Exception ex) when (ex is IOException or ImageFormatException)
        {
            _logger.LogError(ex, "Image process/save fail hui: {Path}", fullPath);
            TryDeletePartial(fullPath);
            return Fail("Couldn't save that image. Please try again.");
        }
    }

    public void Delete(string? relativeUrl)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl)) return;

        // Sirf apne uploads folder ke andar se delete karte hain. Ye check na ho to
        // DB mein "/../../appsettings.json" likhwa kar koi file udwa sakta hai.
        var prefix = $"/{UploadsRoot}/";
        if (!relativeUrl.StartsWith(prefix, StringComparison.Ordinal)) return;

        var uploadsRoot = Path.GetFullPath(Path.Combine(_env.WebRootPath, UploadsRoot));
        var candidate = Path.GetFullPath(Path.Combine(
            _env.WebRootPath,
            relativeUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));

        // Doosri layer: resolve hone ke BAAD bhi path uploads ke andar hi ho.
        if (!candidate.StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase)) return;

        try
        {
            if (File.Exists(candidate)) File.Delete(candidate);
        }
        catch (IOException ex)
        {
            // Delete fail hona user ka masla nahi — sirf ek orphan file reh jayegi.
            _logger.LogWarning(ex, "Purani image delete nahi hui: {Path}", candidate);
        }
    }

    private static ImageSaveResult Fail(string message) => new(false, null, message);

    /// <summary>Save beech mein fail ho to adhoori file peeche na rahe.</summary>
    private static void TryDeletePartial(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { /* ignore */ }
    }
}
