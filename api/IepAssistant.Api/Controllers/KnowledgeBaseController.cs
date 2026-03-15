using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

[ApiController]
[Route("api/knowledge-base")]
public class KnowledgeBaseController : ControllerBase
{
    private readonly IKnowledgeBaseService _knowledgeBaseService;

    public KnowledgeBaseController(IKnowledgeBaseService knowledgeBaseService)
    {
        _knowledgeBaseService = knowledgeBaseService;
    }

    /// <summary>
    /// Search knowledge base entries
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<KnowledgeBaseEntryModel>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] string? query,
        [FromQuery] string? category,
        [FromQuery] string? state,
        CancellationToken cancellationToken)
    {
        var entries = await _knowledgeBaseService.SearchAsync(query, category, state, cancellationToken);
        return Ok(ApiResponse<IEnumerable<KnowledgeBaseEntryModel>>.SuccessResponse(entries));
    }

    /// <summary>
    /// Get a single knowledge base entry by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<KnowledgeBaseEntryModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var entry = await _knowledgeBaseService.GetByIdAsync(id, cancellationToken);

        if (entry == null)
            return NotFound(ApiResponse<object>.Error("Knowledge base entry not found"));

        return Ok(ApiResponse<KnowledgeBaseEntryModel>.SuccessResponse(entry));
    }

    /// <summary>
    /// Get available categories with entry counts
    /// </summary>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<CategoryCount>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories(CancellationToken cancellationToken)
    {
        var categories = await _knowledgeBaseService.GetCategoriesAsync(cancellationToken);
        return Ok(ApiResponse<IEnumerable<CategoryCount>>.SuccessResponse(categories));
    }
}
