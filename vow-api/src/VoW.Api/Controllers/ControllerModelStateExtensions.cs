using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using VoW.Api.Services.Accounts;

namespace VoW.Api.Controllers;

public static class ControllerModelStateExtensions
{
    public static void AddErrors(this ModelStateDictionary modelState, IReadOnlyDictionary<string, string> errors)
    {
        foreach (var (field, message) in errors)
        {
            modelState.AddModelError(field, message);
        }
    }

    public static int? GetUserId(this ClaimsPrincipal user)
    {
        var subject = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return int.TryParse(subject, out var userId) ? userId : null;
    }

    public static IActionResult ProblemFrom(this ControllerBase controller, AccountMutationResult result)
    {
        if (!result.Found)
        {
            return controller.NotFound();
        }

        if (result.IsForbidden)
        {
            return controller.Forbid();
        }

        controller.ModelState.AddErrors(result.Errors);
        return controller.ValidationProblem(controller.ModelState);
    }
}
