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
    /// Get all roles (Manager/Admin only)
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Manager,Admin")]
    public IActionResult GetAllRoles()
    {
        var result = _roleService.GetAllRoles();
        return Ok(result);
    }

    /// <summary>
    /// Get role for a specific user (Manager/Admin only)
    /// </summary>
    [HttpGet("user/{userId}")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> GetUserRole(Guid userId)
    {
        var result = await _roleService.GetUserRoleAsync(userId);
        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

}