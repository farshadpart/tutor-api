namespace Tutor.Api.Services
{
    public interface IEmailView
    {
        string BuildConfirmationLinkBody(string confirmationLink);

        string BuildPasswordResetCodeBody(string resetCode);

        string BuildPasswordResetLinkBody(string resetLink);
    }
}
