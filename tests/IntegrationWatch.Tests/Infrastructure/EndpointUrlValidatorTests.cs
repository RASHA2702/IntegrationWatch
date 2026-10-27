using IntegrationWatch.Infrastructure.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationWatch.Tests.Infrastructure;

[TestClass]
public class EndpointUrlValidatorTests
{
    private readonly EndpointUrlValidator _validator = new();

    [TestMethod]
    [DataRow("https://api.stripe.com/health")]
    [DataRow("https://api.github.com/zen")]
    [DataRow("http://example.com/status")]
    public void ValidateUrl_ValidPublicUrls_ReturnsSuccess(string url)
    {
        var result = _validator.ValidateUrl(url);
        Assert.IsTrue(result.IsValid);
        Assert.IsNull(result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("not-a-valid-uri")]
    [DataRow("ftp://files.example.com")]
    [DataRow("file:///c:/windows/system32/calc.exe")]
    public void ValidateUrl_InvalidOrUnsafeSchemes_FailsValidation(string url)
    {
        var result = _validator.ValidateUrl(url);
        Assert.IsFalse(result.IsValid);
        Assert.IsNotNull(result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("http://127.0.0.1/api/admin")]
    [DataRow("http://127.0.0.2:8080/metrics")]
    [DataRow("http://localhost/internal")]
    [DataRow("http://169.254.169.254/latest/meta-data/")]
    [DataRow("http://10.0.0.1/config")]
    [DataRow("http://192.168.1.1/dashboard")]
    [DataRow("http://172.16.0.1/private")]
    public void ValidateUrl_RestrictedPrivateAndMetadataIps_BlocksSsrf(string url)
    {
        var result = _validator.ValidateUrl(url);
        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.ErrorMessage!.Contains("SSRF prevention") || result.ErrorMessage.Contains("restricted"));
    }

    [TestMethod]
    public void ValidateUrl_WhenAllowPrivateNetworksIsTrue_PermitsPrivateUrl()
    {
        var result = _validator.ValidateUrl("http://127.0.0.1:5000/health", allowPrivateNetworks: true);
        Assert.IsTrue(result.IsValid);
    }
}
