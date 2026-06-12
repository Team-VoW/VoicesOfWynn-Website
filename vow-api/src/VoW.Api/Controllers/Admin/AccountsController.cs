using Microsoft.AspNetCore.Mvc;
using VoW.Api.Controllers;
using VoW.Api.Contracts.Accounts;
using VoW.Api.Domain.Auth;
using VoW.Api.Services.Accounts;

namespace VoW.Api.Controllers.Admin;

[ApiController]
[RequireCapability(Capability.AccountsManage)]
[Route("admin/accounts")]
public sealed class AccountsController(IAccountAdminService accountService) : ControllerBase
{
    [HttpGet("roles")]
    public async Task<ActionResult<IReadOnlyCollection<AccountRoleResponse>>> Roles(CancellationToken cancellationToken) =>
        Ok(await accountService.GetRolesAsync(cancellationToken));

    [HttpGet("search")]
    public async Task<ActionResult<AccountSearchResponse>> Search(
        [FromQuery] AccountSearchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await accountService.SearchAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddErrors(result.Errors);
            return ValidationProblem(ModelState);
        }

        return Ok(result.Response);
    }

    [HttpGet("{userId:int}")]
    public async Task<ActionResult<AccountDetailsResponse>> Get(int userId, CancellationToken cancellationToken)
    {
        var account = await accountService.GetAsync(userId, cancellationToken);
        return account is null ? NotFound() : Ok(account);
    }

    [HttpPatch("{userId:int}")]
    public async Task<IActionResult> Update(
        int userId,
        [FromBody] UpdateAccountRequest request,
        CancellationToken cancellationToken)
    {
        var result = await accountService.UpdateAsync(userId, request, cancellationToken);
        return result.Succeeded ? NoContent() : this.ProblemFrom(result);
    }

    [HttpPost("")]
    public async Task<ActionResult<CreateAccountResponse>> Create(
        [FromBody] CreateAccountRequest request,
        CancellationToken cancellationToken)
    {
        var result = await accountService.CreateAsync(request, cancellationToken);
        if (result.Succeeded)
        {
            return CreatedAtAction(
                nameof(Get),
                new { userId = result.UserId },
                new CreateAccountResponse(result.UserId!.Value, result.TemporaryPassword!));
        }

        ModelState.AddErrors(result.Errors);
        return ValidationProblem(ModelState);
    }

    [HttpPut("{userId:int}/roles")]
    public async Task<IActionResult> ReplaceRoles(
        int userId,
        [FromBody] UpdateAccountRolesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await accountService.ReplaceRolesAsync(userId, request, cancellationToken);
        return result.Succeeded ? NoContent() : this.ProblemFrom(result);
    }

    [HttpPut("{userId:int}/avatar")]
    [RequestSizeLimit(AvatarUploadLimits.MaxSizeBytes)]
    public async Task<IActionResult> UploadAvatar(
        int userId,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
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
        var result = await accountService.UploadAvatarAsync(userId, stream, cancellationToken);
        return result.Succeeded ? NoContent() : this.ProblemFrom(result);
    }

    [HttpDelete("{userId:int}/avatar")]
    public async Task<IActionResult> ClearAvatar(int userId, CancellationToken cancellationToken)
    {
        var result = await accountService.ClearAvatarAsync(userId, cancellationToken);
        return result.Succeeded ? NoContent() : this.ProblemFrom(result);
    }

    [HttpPost("{userId:int}/reset-password")]
    public async Task<ActionResult<ResetPasswordResponse>> ResetPassword(int userId, CancellationToken cancellationToken)
    {
        var result = await accountService.ResetPasswordAsync(userId, cancellationToken);
        if (result.Succeeded)
        {
            return Ok(new ResetPasswordResponse(result.TemporaryPassword!));
        }

        if (!result.Found)
        {
            return NotFound();
        }

        ModelState.AddErrors(result.Errors);
        return ValidationProblem(ModelState);
    }

    [HttpDelete("{userId:int}")]
    public async Task<IActionResult> Delete(int userId, CancellationToken cancellationToken)
    {
        var callerId = User.GetUserId();
        if (callerId is null)
        {
            return Unauthorized();
        }

        var result = await accountService.DeleteAsync(userId, callerId.Value, cancellationToken);
        return result.Succeeded ? NoContent() : this.ProblemFrom(result);
    }
}
