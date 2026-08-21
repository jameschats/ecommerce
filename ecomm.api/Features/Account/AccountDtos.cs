namespace ecomm.api.Features.Account;

public sealed record ProfileDto(long UserId, string? Email, string? FullName, string? PhoneNumber, IReadOnlyList<string> Roles, bool IsEmailVerified);
public sealed record UpdateProfileRequest(string? FullName, string? PhoneNumber);

public sealed record AddressDto(
    long CustomerAddressId, string? Label, string? RecipientName, string? Phone,
    string Line1, string? Line2, string City, string State, string Pincode, string Country,
    string AddressType, bool IsDefault);

public sealed record SaveAddressRequest(
    string? Label, string? RecipientName, string? Phone,
    string Line1, string? Line2, string City, string State, string Pincode, string? Country,
    string? AddressType, bool IsDefault);

public sealed record NotificationPreferenceDto(string Channel, string Category, bool IsOptedIn, DateTime? OptedInAt);
public sealed record SetNotificationPreferenceRequest(string Channel, string Category, bool IsOptedIn);
