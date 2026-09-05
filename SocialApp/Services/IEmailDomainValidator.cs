namespace SocialApp.Services;

/// <summary>Email domain check ka nateeja.</summary>
public enum EmailDomainCheck
{
    /// <summary>Domain ka mail server mil gaya — is par mail ja sakti hai.</summary>
    Deliverable,

    /// <summary>Domain ka koi mail server nahi. Aksar typo hota hai (jaise .comm).</summary>
    NoMailServer,

    /// <summary>DNS se jawab nahi mila (offline/timeout). Faisla nahi kar sakte.</summary>
    CouldNotCheck
}

/// <param name="Status">Check ka nateeja.</param>
/// <param name="Suggestion">
/// Agar domain kisi mashhoor provider ke bohot qareeb tha to uska sahi naam,
/// take user ko "Did you mean gmail.com?" dikha saken. Warna null.
/// </param>
public record EmailDomainResult(EmailDomainCheck Status, string? Suggestion);

/// <summary>
/// Ye check karta hai ke email ke domain par mail waqai pohnch sakti hai ya nahi.
///
/// Zaroorat kyun pari: [EmailAddress] sirf SHAKAL dekhta hai. "you@smal.comm"
/// bilkul valid shakal ka email hai — .comm ek valid domain label hai. Format
/// validation ke paas TLD ki list nahi hoti, is liye woh isay rok nahi sakta.
/// Rokne ka ek hi tareeqa hai: DNS se poochho ke is domain ka mail server hai ya nahi.
/// </summary>
public interface IEmailDomainValidator
{
    Task<EmailDomainResult> CheckAsync(string email, CancellationToken cancellationToken = default);
}
