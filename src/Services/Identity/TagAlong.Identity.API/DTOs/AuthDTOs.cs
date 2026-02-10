namespace TagAlong.Identity.API.DTOs;

public record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string PhoneNumber);

public record LoginRequest(
    string Email,
    string Password);

public record GoogleLoginRequest(
    string IdToken);

public record RefreshTokenRequest(
    string AccessToken,
    string RefreshToken);

public record VerifyEmailRequest(
    string Code);

public record VerifyPhoneRequest(
    string Code);

public record ForgotPasswordRequest(
    string Email);

public record ResetPasswordRequest(
    string Email,
    string Code,
    string NewPassword);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword);

public record AuthResponse(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt);

public record UserInfoResponse(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string PhoneNumber,
    bool IsEmailVerified,
    bool IsPhoneVerified,
    string? ProfileImageUrl);
