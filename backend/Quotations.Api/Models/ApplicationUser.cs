using Microsoft.AspNetCore.Identity;

namespace Quotations.Api.Models;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public int SubmissionCount { get; set; } = 0;
}
