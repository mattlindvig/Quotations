namespace Quotations.Api.Configuration;

public class MeilisearchSettings
{
    public bool Enabled { get; set; } = false;
    public string Url { get; set; } = "http://localhost:7700";
    public string ApiKey { get; set; } = "";
}
