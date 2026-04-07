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
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetAllRoles()
    {
        var result = await _roleService.GetAllRolesAsync();
        return Ok(result);
    }

    /// <summary>
    /// Get role by ID (Admin only)
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetRoleById(Guid id)
    {
        var result = await _roleService.GetRoleByIdAsync(id);
        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    /// <summary>
    /// Get roles for a specific user (Admin only)
    /// </summary>
    [HttpGet("user/{userId}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetUserRoles(Guid userId)
    {
        var result = await _roleService.GetUserRolesAsync(userId);
        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    /// <summary>
    /// Create a new role (Admin only)
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request)
    {
        var result = await _roleService.CreateRoleAsync(request);
        if (!result.Success)
            return BadRequest(result);

        return CreatedAtAction(nameof(GetRoleById), new { id = result.Data?.Id }, result);
    }

    /// <summary>
    /// Update an existing role (Admin only)
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdateRole(Guid id, [FromBody] UpdateRoleRequest request)
    {
        var result = await _roleService.UpdateRoleAsync(id, request);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Delete a role (Admin only)
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeleteRole(Guid id)
    {
        var result = await _roleService.DeleteRoleAsync(id);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Assign a role to a user (Admin only)
    /// </summary>
    [HttpPost("assign")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> AssignRole([FromBody] AssignRoleRequest request)
    {
        var result = await _roleService.AssignRoleToUserAsync(request);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Remove a role from a user (Admin only)
    /// </summary>
    [HttpDelete("user/{userId}/role/{roleId}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> RemoveRoleFromUser(Guid userId, Guid roleId)
    {
        var result = await _roleService.RemoveRoleFromUserAsync(userId, roleId);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
}