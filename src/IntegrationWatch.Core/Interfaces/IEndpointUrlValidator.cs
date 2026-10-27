namespace IntegrationWatch.Core.Interfaces;

public record UrlValidationResult(bool IsValid, string? ErrorMessage)
{
    public static UrlValidationResult Success() => new(true, null);
    public static UrlValidationResult Failure(string errorMessage) => new(false, errorMessage);
}

public interface IEndpointUrlValidator
{
    UrlValidationResult ValidateUrl(string url, bool allowPrivateNetworks = false);
}
