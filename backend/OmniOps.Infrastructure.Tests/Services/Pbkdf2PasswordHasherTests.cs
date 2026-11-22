using OmniOps.Infrastructure.Services;

namespace OmniOps.Infrastructure.Tests.Services;

public class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new(iterations: 1_000); // cheap for tests

    [Fact]
    public void Verify_AcceptsTheRightPasswordOnly()
    {
        var hash = _hasher.Hash("correct horse battery");

        Assert.True(_hasher.Verify("correct horse battery", hash, out _));
        Assert.False(_hasher.Verify("correct horse batterY", hash, out _));
    }

    [Fact]
    public void Hash_IsSaltedAndNeverContainsThePassword()
    {
        var a = _hasher.Hash("same password");
        var b = _hasher.Hash("same password");

        Assert.NotEqual(a, b);
        Assert.DoesNotContain("same password", a);
        Assert.StartsWith("pbkdf2-sha256$1000$", a);
    }

    [Fact]
    public void Verify_FlagsOldHashesForUpgradeWhenTheCostIsRaised()
    {
        var old = _hasher.Hash("pw-123456789");

        Assert.True(new Pbkdf2PasswordHasher(iterations: 2_000).Verify("pw-123456789", old, out var needsRehash));
        Assert.True(needsRehash);
    }

    [Theory]
    [InlineData("")]
    [InlineData("plain-text")]
    [InlineData("pbkdf2-sha256$x$abc$def")]
    [InlineData("pbkdf2-sha256$1000$not-base64!$also-not")]
    public void Verify_RejectsMalformedHashes(string stored)
    {
        Assert.False(_hasher.Verify("anything", stored, out _));
    }
}
