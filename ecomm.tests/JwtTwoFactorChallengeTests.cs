using ecomm.api.Features.Auth.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace ecomm.tests;

/// <summary>Regression coverage for a real bug caught live during the v4 Phase 1 2FA rollout: every
/// challenge token failed validation with a misleading "expired" message, even brand new ones,
/// because JwtSecurityTokenHandler remaps "sub" to a ClaimTypes URI by default and the validation
/// code was looking up the raw short name. Every 2FA login was broken until this was fixed.</summary>
public class JwtTwoFactorChallengeTests
{
    private static JwtTokenService NewSvc() => new(Options.Create(new JwtSettings
    {
        Issuer = "ecomm.api", Audience = "ecomm.web", Key = "test-signing-key-at-least-32-characters-long",
    }));

    [Fact]
    public void A_freshly_issued_challenge_token_validates_and_round_trips_the_user_id()
    {
        var svc = NewSvc();
        var (token, _) = svc.CreateTwoFactorChallengeToken(userId: 42);

        var result = svc.ValidateTwoFactorChallengeToken(token);

        Assert.Equal(42, result);
    }

    [Fact]
    public void An_access_token_is_not_accepted_as_a_two_factor_challenge_token()
    {
        var svc = NewSvc();
        var user = new ecomm.api.Data.Entities.User { UserId = 1, TenantId = 1 };
        var (accessToken, _) = svc.CreateAccessToken(user, ["Admin"], []);

        Assert.Null(svc.ValidateTwoFactorChallengeToken(accessToken));
    }

    [Fact]
    public void A_tampered_token_is_rejected()
    {
        var svc = NewSvc();
        var (token, _) = svc.CreateTwoFactorChallengeToken(userId: 1);

        Assert.Null(svc.ValidateTwoFactorChallengeToken(token[..^2] + "xx"));
    }

    [Fact]
    public void Garbage_input_is_rejected_without_throwing()
    {
        var svc = NewSvc();
        Assert.Null(svc.ValidateTwoFactorChallengeToken("not-a-jwt"));
        Assert.Null(svc.ValidateTwoFactorChallengeToken(""));
    }
}
