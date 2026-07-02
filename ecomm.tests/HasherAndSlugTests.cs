using ecomm.api.Common;
using ecomm.api.Features.Auth.Services;
using Xunit;

namespace ecomm.tests;

public sealed class PasswordHasherTests
{
    private readonly IPasswordHasher _hasher = new BcryptPasswordHasher();

    [Fact]
    public void Hash_ThenVerify_Succeeds()
    {
        var hash = _hasher.Hash("Admin@123");
        Assert.NotEqual("Admin@123", hash);         // never stored in plaintext
        Assert.True(_hasher.Verify("Admin@123", hash));
    }

    [Fact]
    public void Verify_WrongPassword_Fails()
    {
        var hash = _hasher.Hash("Admin@123");
        Assert.False(_hasher.Verify("wrong", hash));
    }

    [Fact]
    public void Hash_IsSaltedPerCall()
    {
        Assert.NotEqual(_hasher.Hash("same"), _hasher.Hash("same"));  // different salt each time
    }
}

public sealed class SlugTests
{
    [Theory]
    [InlineData("Wall Calendar 2026", "wall-calendar-2026")]
    [InlineData("  Hello World!  ", "hello-world")]
    [InlineData("Multiple   Spaces", "multiple-spaces")]
    [InlineData("Already-Slug", "already-slug")]
    [InlineData("Dell Battery 65Wh", "dell-battery-65wh")]
    public void From_ProducesCleanSlug(string input, string expected)
        => Assert.Equal(expected, Slug.From(input));

    [Theory]
    [InlineData("")]
    [InlineData("###")]
    [InlineData("   ")]
    public void From_EmptyOrSymbols_FallsBackToItem(string input)
        => Assert.Equal("item", Slug.From(input));
}
