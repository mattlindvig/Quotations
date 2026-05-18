namespace Quotations.Api.Configuration;

public class ResendSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string FromEmail { get; set; } = "noreply@example.com";
    public string FromName { get; set; } = "Quotations";
}
