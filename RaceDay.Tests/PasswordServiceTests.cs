using RaceDay.Api.Services;

namespace RaceDay.Tests;

/// <summary>Pure unit tests (no HTTP, no database) for the PBKDF2 password hashing service.</summary>
public class PasswordServiceTests
{
    private readonly PasswordService _service = new();

    [Fact]
    public void Hash_DoesNotContainThePlainPassword()
    {
        var hash = _service.Hash("Password123!");

        Assert.DoesNotContain("Password123!", hash);
        Assert.StartsWith("PBKDF2-SHA256$", hash);
    }

    [Fact]
    public void Hash_UsesARandomSalt_SoTheSamePasswordHashesDifferently()
    {
        Assert.NotEqual(_service.Hash("Password123!"), _service.Hash("Password123!"));
    }

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        var hash = _service.Hash("Password123!");
        Assert.True(_service.Verify("Password123!", hash));
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var hash = _service.Hash("Password123!");
        Assert.False(_service.Verify("password123!", hash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("plain-text-password")]
    [InlineData("PBKDF2-SHA256$abc$not-base64$not-base64")]
    [InlineData("PBKDF2-SHA256$100000$!!!$!!!")]
    public void Verify_MalformedStoredHash_ReturnsFalseInsteadOfThrowing(string storedHash)
    {
        Assert.False(_service.Verify("Password123!", storedHash));
    }
}
