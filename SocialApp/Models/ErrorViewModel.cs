namespace SocialApp.Models;

/// <summary>
/// Error page ka view model. Status code (404, 500 etc) aur request ID handle karta hai.
/// </summary>
public class ErrorViewModel
{
    public string? RequestId { get; set; }
    public int? StatusCode { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    public bool Is404 => StatusCode == 404;

    public string Title => Is404
        ? "Page Not Found"
        : StatusCode.HasValue
            ? $"Error {StatusCode.Value}"
            : "Something Went Wrong";

    public string Message => Is404
        ? "The page or resource you are looking for doesn't exist, was deleted, or the URL might be mistyped."
        : "An unexpected error occurred while processing your request. Please try again or return home.";
}
