using Google.Apis.Auth;

namespace ecomm.api.Features.Auth.Services;

public sealed record GoogleUserInfo(string Subject, string? Email, string? Name, bool EmailVerified);

public interface IGoogleTokenValidator
{
    Task<GoogleUserInfo?> ValidateAsync(string idToken, string clientId, CancellationToken ct = default);
}

/// <summary>Validates a Google ID token (signature + audience) and extracts the profile.</summary>
public sealed class GoogleTokenValidator : IGoogleTokenValidator
{
    public async Task<GoogleUserInfo?> ValidateAsync(string idToken, string clientId, CancellationToken ct = default)
    {
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings { Audience = new[] { clientId } };
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
            return new GoogleUserInfo(payload.Subject, payload.Email, payload.Name, payload.EmailVerified);
        }
        catch
        {
            return null;
        }
    }
}
