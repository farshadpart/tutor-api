namespace Tutor.Api.Models.Exceptions
{
    public static class Errors
    {
        public const string USER_ALREADY_HAS_SUBSCRIPTION = "The user already has a subscription with this type!";
        public const string SUBSCRIPTION_START_DATE_INVALID = "The subscription start date is not valid!";
        public const string USER_NOT_EXIST = "The user does not exist!";
        public const string SUBSCRIPTION_TYPE_NOT_EXIST = "The subscription type does not exist!";
        public const string USER_NOT_HAVE_SUBSCRIPTION = "The user does not have the subscription!";
        public const string NO_ACTIVE_CYCLE = "The user subscription does not have any active cycle!";
        public const string ALL_REQUESTS_USED = "All possible requests in this cycle have been used!";
    }
}
