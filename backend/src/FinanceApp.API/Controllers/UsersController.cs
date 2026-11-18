using FinanceApp.API.Security;
using FinanceApp.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApp.API.Controllers;

/// <summary>Dados do usuário autenticado. A identidade vem sempre do access token.</summary>
[ApiController]
[Authorize]
[Route("api/v1/users/me")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class UsersController(UserService userService, RefreshTokenCookie refreshCookie) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserDto>> GetMe(CancellationToken cancellationToken) =>
        Ok(await userService.GetCurrentAsync(cancellationToken));

    [HttpPut]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<UserDto>> UpdateMe([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken) =>
        Ok(await userService.UpdateProfileAsync(request, cancellationToken));

    [HttpPut("password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        await userService.ChangePasswordAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> DeleteMe([FromBody] DeleteAccountRequest request, CancellationToken cancellationToken)
    {
        await userService.DeleteAccountAsync(request, cancellationToken);
        refreshCookie.Delete(Response);
        return NoContent();
    }
}
