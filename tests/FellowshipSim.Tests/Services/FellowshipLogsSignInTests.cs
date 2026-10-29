using FellowshipSim.Logs;

namespace FellowshipSim.Tests.Services;

/// <summary>Signing in to FellowshipLogs with PKCE: the challenge as the standard computes it, and the sign-in page's address.</summary>
public class FellowshipLogsSignInTests
{
    [Fact]
    public void The_Challenge_Is_The_Verifiers_SHA256_In_Base64url()
    {
        // As Python's hashlib computes it: base64url(sha256(verifier)), unpadded.
        Assert.Equal("WkTxhmINhW-bB_0mX6MFD8PVlfUilr6SNfg2-UUnN2M", FellowshipLogsSignIn.Challenge("dBjftJeZ4CVP-mJ92K1ZhOVt3n4GfTvfcatrDaSu_cc7"));
        Assert.Matches("^[A-Za-z0-9_-]{43}$", FellowshipLogsSignIn.NewVerifier());
    }

    [Fact]
    public void The_Sign_In_Page_Has_The_Public_Client_Its_Way_Back_And_The_Challenge()
    {
        string uri = FellowshipLogsSignIn.AuthorizeUri("test-client", FellowshipLogsSignIn.SiteRedirect, "dBjftJeZ4CVP-mJ92K1ZhOVt3n4GfTvfcatrDaSu_cc7", "abc");
        Assert.StartsWith("https://www.fellowshiplogs.com/oauth/authorize?response_type=code&client_id=test-client", uri);
        Assert.Contains("&redirect_uri=https%3A%2F%2Fsimfelldev.github.io%2Fsignin", uri);
        Assert.Contains("&code_challenge=WkTxhmINhW-bB_0mX6MFD8PVlfUilr6SNfg2-UUnN2M&code_challenge_method=S256&state=abc", uri);
    }
}
