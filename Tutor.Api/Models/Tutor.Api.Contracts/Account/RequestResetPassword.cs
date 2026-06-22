namespace Tutor.Api.Models.Tutor.Api.Contracts.Account
{
    public record RequestResetPassword(string Email, string Token, string NewPassword);
}
