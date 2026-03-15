using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;

namespace IepAssistant.Api.Filters;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class AuthorizeChildAccessAttribute : Attribute, IAsyncActionFilter
{
    public AccessRole MinimumRole { get; }

    public AuthorizeChildAccessAttribute(AccessRole minimumRole = AccessRole.Viewer)
    {
        MinimumRole = minimumRole;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.ActionArguments.TryGetValue("childId", out var childIdObj) || childIdObj is not int childId)
        {
            await next();
            return;
        }

        var userIdClaim = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var accessService = context.HttpContext.RequestServices.GetRequiredService<IAccessService>();
        var role = await accessService.GetRoleAsync(childId, userId);

        if (role == null || role < MinimumRole)
        {
            context.Result = new ForbidResult();
            return;
        }

        await next();
    }
}
