namespace SocialApp.Models.ViewModels;

/// <summary>
/// Search page ka master view model.
/// Dono tabs (People aur Posts) ka data aur active tab state sambhalta hai.
/// </summary>
public class SearchViewModel
{
    public string? Query { get; set; }

    /// <summary>Active tab: "people" ya "posts"</summary>
    public string Tab { get; set; } = "people";

    /// <summary>People tab ke results</summary>
    public IReadOnlyList<SearchUserViewModel> Users { get; set; } = [];

    /// <summary>Posts tab ke results — FeedViewModel reuse karte hain taake paging aur _Post.cshtml partial chal sake</summary>
    public FeedViewModel? PostsFeed { get; set; }

    public int PeopleCount { get; set; }
    public int PostsCount { get; set; }

    /// <summary>User ne koi query type ki hai ya abhi search page par naya aya hai</summary>
    public bool HasSearched => !string.IsNullOrWhiteSpace(Query);

    public bool IsPeopleTab => string.Equals(Tab, "people", StringComparison.OrdinalIgnoreCase);
    public bool IsPostsTab => string.Equals(Tab, "posts", StringComparison.OrdinalIgnoreCase);
}
