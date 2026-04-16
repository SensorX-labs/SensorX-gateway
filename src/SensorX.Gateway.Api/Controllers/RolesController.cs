using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SensorX.Gateway.Application.DTOs;
using SensorX.Gateway.Application.Interfaces;

namespace SensorX.Gateway.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly IRoleService _roleService;

    public RolesController(IRoleService roleService)
    {
        _roleService = roleService;
    }

    /// <summary>
    /// Get all roles (Admin only)
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Administrator")]
    public IActionResult GetAllRoles()
    {
        var result = _roleService.GetAllRoles();
        return Ok(result);
    }

    /// <summary>
    /// Get role for a specific user (Admin only)
    /// </summary>
    [HttpGet("user/{userId}")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> GetUserRole(Guid userId)
    {
        var result = await _roleService.GetUserRoleAsync(userId);
        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    /// <summary>
    /// Assign a role to a user (Admin only)
    /// </summary>
    [HttpPost("assign")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> AssignRole([FromBody] AssignRoleRequest request)
    {
        var result = await _roleService.AssignRoleToUserAsync(request);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
}