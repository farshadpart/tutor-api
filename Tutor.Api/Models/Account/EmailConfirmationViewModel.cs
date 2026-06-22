namespace Tutor.Api.Models.Account
{
    public record EmailConfirmationViewModel(string Title, string Message, string Guidance, bool IsSuccess)
    {
        public string Eyebrow => IsSuccess ? "Confirmation complete" : "Confirmation failed";
    }
}
