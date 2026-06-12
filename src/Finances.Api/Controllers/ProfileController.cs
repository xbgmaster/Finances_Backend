using Finances.Application.Dtos;
using Finances.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finances.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly IProfileService _profile;

    public ProfileController(IProfileService profile) => _profile = profile;

    [HttpGet]
    public async Task<ActionResult<UserProfileDto>> Get(CancellationToken ct) =>
        Ok(await _profile.GetAsync(ct));

    [HttpPut]
    public async Task<ActionResult<UserProfileDto>> Update(UpdateProfileDto dto, CancellationToken ct) =>
        Ok(await _profile.UpdateAsync(dto, ct));
}
