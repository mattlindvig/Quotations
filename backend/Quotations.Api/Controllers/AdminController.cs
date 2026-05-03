using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quotations.Api.Models;
using Quotations.Api.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Quotations.Api.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private static readonly List<string> AllowedRoles = new() { "User", "Reviewer", "Admin" };

    private readonly IUserRepository _users;

    public AdminController(IUserRepository users)
    {
        _users = users;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _users.GetAllAsync();

        var items = users.Select(u => new
        {
            id = u.Id,
            username = u.Username,
            displayName = u.DisplayName,
            email = u.Email,
            roles = u.Roles,
            isActive = u.IsActive,
            createdAt = u.CreatedAt
        });

        return Ok(new { success = true, data = items });
    }

    [HttpPut("users/{userId}/roles")]
    public async Task<IActionResult> UpdateRoles(string userId, [FromBody] UpdateRolesRequest request)
    {
        var invalid = request.Roles.Except(AllowedRoles).ToList();
        if (invalid.Count > 0)
            return BadRequest(new { success = false, message = $"Unknown roles: {string.Join(", ", invalid)}" });

        // Always keep the base User role
        var roles = request.Roles.Contains("User")
            ? request.Roles.Distinct().ToList()
            : request.Roles.Prepend("User").Distinct().ToList();

        var updated = await _users.UpdateRolesAsync(userId, roles);
        if (!updated)
            return NotFound(new { success = false, message = "User not found" });

        return Ok(new { success = true, data = new { userId, roles } });
    }
}

public record UpdateRolesRequest(List<string> Roles);
