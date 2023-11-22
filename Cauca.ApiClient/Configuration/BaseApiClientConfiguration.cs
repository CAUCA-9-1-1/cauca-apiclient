namespace Cauca.ApiClient.Configuration
{
    public abstract class BaseApiClientConfiguration
    {
        public virtual string ApiBaseUrl { get; set; }
        public virtual string ApiBaseUrlForAuthentication { get; set; }
        public virtual string UserId { get; set; }
        public virtual string Password { get; set; }
        public virtual bool UseExternalSystemLogin { get; set; }
        public virtual int RequestTimeoutInSeconds { get; set; } = 5;
        public virtual int MaxRequestRetryAttempts { get; set; } = 3;
    }
}
