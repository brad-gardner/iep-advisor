using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Controllers;

[ApiController]
public class ConfigController : ControllerBase
{
    private readonly IFeatureFlags _featureFlags;

    public ConfigController(IFeatureFlags featureFlags)
    {
        _featureFlags = featureFlags;
    }

    [HttpGet("api/config")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<Dictionary<string, bool>>), StatusCodes.Status200OK)]
    public IActionResult GetConfig()
    {
        var features = new Dictionary<string, bool>
        {
            [FeatureFlags.AnalysisRun] = _featureFlags.IsEnabled(FeatureFlags.AnalysisRun),
            [FeatureFlags.MeetingPrepStandalone] = _featureFlags.IsEnabled(FeatureFlags.MeetingPrepStandalone),
            [FeatureFlags.SchoolSide] = _featureFlags.IsEnabled(FeatureFlags.SchoolSide),
            [FeatureFlags.StudentWorkspace] = _featureFlags.IsEnabled(FeatureFlags.StudentWorkspace),
        };

        return Ok(ApiResponse<Dictionary<string, bool>>.SuccessResponse(features));
    }
}
