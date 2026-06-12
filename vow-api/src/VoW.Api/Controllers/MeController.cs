using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VoW.Api.Contracts.Profile;
using VoW.Api.Services.Accounts;

namespace VoW.Api.Controllers;

[ApiController]
[Authorize]
[Route("me")]
public sealed class MeController(ISelfProfileService profileService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SelfProfileResponse>> Get(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var profile = await profileService.GetAsync(userId.Value, cancellationToken);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPut]
    public async Task<IActionResult> Update(
        [FromBody] UpdateSelfProfileRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await profileService.UpdateAsync(userId.Value, request, cancellationToken);
        return result.Succeeded ? NoContent() : this.ProblemFrom(result);
    }

    [HttpPut("password")]
    public async Task<IActionResult> SetPassword(
        [FromBody] SetSelfPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await profileService.SetPasswordAsync(userId.Value, request, cancellationToken);
        return result.Succeeded ? NoContent() : this.ProblemFrom(result);
    }

    [HttpPut("avatar")]
    [RequestSizeLimit(AvatarUploadLimits.MaxSizeBytes)]
    public async Task<IActionResult> UploadAvatar(IFormFile? file, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError(nameof(file), "An avatar image is required.");
            return ValidationProblem(ModelState);
        }

        if (file.Length > AvatarUploadLimits.MaxSizeBytes)
        {
            ModelState.AddModelError(nameof(file), $"Avatar image must not exceed {AvatarUploadLimits.MaxSizeBytes} bytes.");
            return ValidationProblem(ModelState);
        }

        await using var stream = file.OpenReadStream();
        var result = await profileService.UploadAvatarAsync(userId.Value, stream, cancellationToken);
        return result.Succeeded ? NoContent() : this.ProblemFrom(result);
    }

    [HttpDelete("avatar")]
    public async Task<IActionResult> ClearAvatar(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await profileService.ClearAvatarAsync(userId.Value, cancellationToken);
        return result.Succeeded ? NoContent() : this.ProblemFrom(result);
    }

    private int? GetUserId()
        => User.GetUserId();
}
