using System.ComponentModel.DataAnnotations;

namespace Quotations.Api.Configuration;

public class AppSettings
{
    [Required]
    [Url]
    public string FrontendUrl { get; set; } = "http://localhost:5173";
}
