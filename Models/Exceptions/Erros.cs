namespace Tutor.Api.Models.Exceptions
{
    public static class Errors
    {
        public const string SOMETHING_WENT_WRONG = "Something went wrong!";
        public const string USER_ALREADY_HAS_SUBSCRIPTION = "The user already has a subscription with this type!";
        public const string SUBSCRIPTION_START_DATE_INVALID = "The subscription start date is not valid!";
        public const string USER_NOT_FOUND = "The user does not exist!";
        public const string SUBSCRIPTION_TYPE_NOT_EXIST = "The subscription type does not exist!";
        public const string USER_NOT_HAVE_SUBSCRIPTION = "The user does not have the subscription!";
        public const string NO_ACTIVE_CYCLE = "The user subscription does not have any active cycle!";
        public const string NO_USABLE_CYCLE = "The user subscription does not have any usable cycle!";
        public const string ALL_REQUESTS_USED = "All possible requests in this cycle have been used!";
        public const string NO_CYCLE_AVAILABLE = "There is no cycle available for the user subscription!";
        public const string TIME_WINDOW_FINISHED = "The time window of the cycle has finished!";
        public const string COULD_NOT_ACQUIRE_LOCK_FOR_USER_UPDATE = "Could not acquire lock for user update";
        public const string FAILED_VALIDATE_QUEUED_CYCLE = "Failed to validate queued cycle for user!";
        public const string DEEP_CLONE_FAILED = "Failed to deep clone the object!";
    }
}
