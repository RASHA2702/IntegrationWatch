using IntegrationWatch.Infrastructure.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationWatch.Tests.Infrastructure;

[TestClass]
public class HmacSecurityHelperTests
{
    private const string TestSecret = "super-secret-key-42";
    private const string Payload = "{\"event\":\"incident.triggered\",\"endpoint\":\"Stripe\"}";

    [TestMethod]
    public void ComputeHmacSha256_GeneratesConsistentSignature()
    {
        var sig1 = HmacSecurityHelper.ComputeHmacSha256(Payload, TestSecret);
        var sig2 = HmacSecurityHelper.ComputeHmacSha256(Payload, TestSecret);

        Assert.IsNotNull(sig1);
        Assert.AreEqual(64, sig1.Length); // 256 bits = 64 hex characters
        Assert.AreEqual(sig1, sig2);
    }

    [TestMethod]
    public void VerifyHmacSha256_ValidSignature_ReturnsTrue()
    {
        var signature = HmacSecurityHelper.ComputeHmacSha256(Payload, TestSecret);

        // Raw hex
        Assert.IsTrue(HmacSecurityHelper.VerifyHmacSha256(Payload, TestSecret, signature));
        // Prefixed with sha256=
        Assert.IsTrue(HmacSecurityHelper.VerifyHmacSha256(Payload, TestSecret, $"sha256={signature}"));
    }

    [TestMethod]
    public void VerifyHmacSha256_TamperedPayload_ReturnsFalse()
    {
        var signature = HmacSecurityHelper.ComputeHmacSha256(Payload, TestSecret);
        var tamperedPayload = "{\"event\":\"incident.triggered\",\"endpoint\":\"Hacked\"}";

        Assert.IsFalse(HmacSecurityHelper.VerifyHmacSha256(tamperedPayload, TestSecret, signature));
    }

    [TestMethod]
    public void VerifyHmacSha256_InvalidSecret_ReturnsFalse()
    {
        var signature = HmacSecurityHelper.ComputeHmacSha256(Payload, TestSecret);

        Assert.IsFalse(HmacSecurityHelper.VerifyHmacSha256(Payload, "wrong-secret", signature));
    }
}
