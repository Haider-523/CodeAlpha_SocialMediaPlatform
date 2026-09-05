namespace SocialApp.Services;

/// <summary>
/// Ek image save karne ka nateeja. Exception throw karne ke bajaye result return
/// karte hain kyunki "user ne bari file di" ek expected validation failure hai,
/// exceptional halat nahi — aur exceptions mehngi hoti hain.
///
/// Width/Height process hone ke BAAD ki hain. Inhein DB mein rakhna zaroori hai:
/// browser tab tak image ke liye jagah reserve nahi kar sakta jab tak usse naap
/// na pata ho, aur naap sirf image download hone par pata chalti hai — yehi
/// wajah hai ke Facebook ka feed load hote waqt hilta hai.
/// </summary>
public record ImageSaveResult(bool Succeeded, string? Url, string? Error, int Width = 0, int Height = 0);

/// <summary>Image ko kis shakl mein process karna hai.</summary>
public enum ImageShape
{
    /// <summary>Avatar: center se square crop + 320×320. Har avatar ek jaisa dikhta hai.</summary>
    Avatar,

    /// <summary>Post image: aspect ratio wesa hi, sirf 1280px se bara ho to chhota karo.</summary>
    Post
}

/// <summary>
/// Uploaded images ko save/delete karta hai. Interface is liye hai ke Azure par
/// deploy karte waqt LocalImageStorage ko BlobImageStorage se badalna
/// Program.cs ki ek line ka kaam ho — controllers chhune ki zaroorat na ho
/// (App Service ka local disk deploy par mit jata hai).
/// </summary>
public interface IImageStorage
{
    /// <param name="folder">wwwroot/uploads ke andar sub-folder, jaise "avatars" ya "posts".</param>
    Task<ImageSaveResult> SaveAsync(
        IFormFile file, string folder, ImageShape shape, CancellationToken cancellationToken = default);

    /// <summary>Purani file hataata hai. Na milne par chup-chaap wapas aa jata hai.</summary>
    void Delete(string? relativeUrl);
}
