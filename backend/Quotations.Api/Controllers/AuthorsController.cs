using Microsoft.AspNetCore.Mvc;
using Quotations.Api.Models;
using Quotations.Api.Repositories;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Quotations.Api.Controllers;

/// <summary>
/// API controller for author operations
/// </summary>
[ApiController]
[Route("api/v1/authors")]
public class AuthorsController : ControllerBase
{
    private readonly IAuthorRepository _authorRepository;

    public AuthorsController(IAuthorRepository authorRepository)
    {
        _authorRepository = authorRepository;
    }

    /// <summary>
    /// Get list of authors
    /// </summary>
    /// <param name="limit">Maximum number of authors to return (optional)</param>
    /// <returns>List of authors</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<Author>>), 200)]
    public async Task<ActionResult<ApiResponse<List<Author>>>> GetAuthors([FromQuery] int? limit = null)
    {
        var authors = await _authorRepository.GetAuthorsAsync(limit);

        return Ok(new ApiResponse<List<Author>>
        {
            Data = authors,
            Success = true
        });
    }

    /// <summary>
    /// Get a single author by ID
    /// </summary>
    /// <param name="id">Author ID</param>
    /// <returns>Author details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<Author>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<ActionResult<ApiResponse<Author>>> GetAuthorById(string id)
    {
        var author = await _authorRepository.GetAuthorByIdAsync(id);

        if (author == null)
        {
            return NotFound(new ApiResponse<object>
            {
                Success = false,
                Errors = new Dictionary<string, string[]>
                {
                    { "id", new[] { $"Author with ID '{id}' not found" } }
                }
            });
        }

        return Ok(new ApiResponse<Author>
        {
            Data = author,
            Success = true
        });
    }
}
