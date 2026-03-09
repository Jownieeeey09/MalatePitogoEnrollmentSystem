using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MalatePitogoEnrollmentSystem.Models;

namespace MalatePitogoEnrollmentSystem.Controllers
{
    /// <summary>
    /// Handles all Role CRUD and User↔Role assignment for the RBAC system.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")] // Default: every endpoint requires Admin unless overridden
    public class RoleController : ControllerBase
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<IdentityUser> _userManager;

        public RoleController(RoleManager<IdentityRole> roleManager, UserManager<IdentityUser> userManager)
        {
            _roleManager = roleManager;
            _userManager = userManager;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // ROLE CRUD
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// GET api/role
        /// Returns a list of all roles in the system.
        /// Accessible by any authenticated user so front-ends can populate dropdowns.
        /// </summary>
        [HttpGet]
        [Authorize] // override: any authenticated user
        public IActionResult GetAllRoles()
        {
            var roles = _roleManager.Roles
                .Select(r => new { r.Id, r.Name })
                .OrderBy(r => r.Name)
                .ToList();

            return Ok(new { count = roles.Count, roles });
        }

        /// <summary>
        /// POST api/role
        /// Creates a new role. Admin only.
        /// Body: { "name": "Finance", "description": "Optional note" }
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateRole([FromBody] CreateRoleModel model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            if (await _roleManager.RoleExistsAsync(model.Name))
                return Conflict(new { message = $"Role '{model.Name}' already exists." });

            var role = new IdentityRole(model.Name);
            var result = await _roleManager.CreateAsync(role);

            if (!result.Succeeded)
                return BadRequest(new { errors = result.Errors });

            return Ok(new
            {
                message = $"Role '{model.Name}' created successfully.",
                role = new { role.Id, role.Name, description = model.Description }
            });
        }

        /// <summary>
        /// PUT api/role/{roleName}
        /// Renames an existing role. Admin only.
        /// Body: { "newName": "NewRoleName" }
        /// </summary>
        [HttpPut("{roleName}")]
        public async Task<IActionResult> UpdateRole(string roleName, [FromBody] UpdateRoleModel model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var role = await _roleManager.FindByNameAsync(roleName);
            if (role == null)
                return NotFound(new { message = $"Role '{roleName}' not found." });

            if (await _roleManager.RoleExistsAsync(model.NewName))
                return Conflict(new { message = $"Role '{model.NewName}' already exists." });

            var oldName = role.Name;
            role.Name = model.NewName;
            var result = await _roleManager.UpdateAsync(role);

            if (!result.Succeeded)
                return BadRequest(new { errors = result.Errors });

            return Ok(new { message = $"Role renamed from '{oldName}' to '{model.NewName}'.", role = new { role.Id, role.Name } });
        }

        /// <summary>
        /// DELETE api/role/{roleName}
        /// Deletes a role. Will fail if any users are still assigned to it. Admin only.
        /// </summary>
        [HttpDelete("{roleName}")]
        public async Task<IActionResult> DeleteRole(string roleName)
        {
            var role = await _roleManager.FindByNameAsync(roleName);
            if (role == null)
                return NotFound(new { message = $"Role '{roleName}' not found." });

            // Prevent deleting a role that still has members to avoid orphaned access
            var usersInRole = await _userManager.GetUsersInRoleAsync(roleName);
            if (usersInRole.Count > 0)
            {
                return BadRequest(new
                {
                    message = $"Cannot delete role '{roleName}' while {usersInRole.Count} user(s) are still assigned to it. Remove them first.",
                    affectedUsers = usersInRole.Count
                });
            }

            var result = await _roleManager.DeleteAsync(role);
            if (!result.Succeeded)
                return BadRequest(new { errors = result.Errors });

            return Ok(new { message = $"Role '{roleName}' deleted successfully." });
        }

        // ─────────────────────────────────────────────────────────────────────────
        // USER ↔ ROLE ASSIGNMENT
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// POST api/role/assign
        /// Assigns a single role to a user. Admin only.
        /// Body: { "userId": "...", "role": "Admin", "replaceExistingRoles": false }
        /// </summary>
        [HttpPost("assign")]
        public async Task<IActionResult> AssignRole([FromBody] AssignRoleModel model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null) return NotFound(new { message = "User not found." });

            if (!await _roleManager.RoleExistsAsync(model.Role))
                return BadRequest(new { message = $"Role '{model.Role}' does not exist." });

            if (model.ReplaceExistingRoles)
            {
                var currentRoles = await _userManager.GetRolesAsync(user);
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
            }

            if (await _userManager.IsInRoleAsync(user, model.Role))
                return Conflict(new { message = $"User already has the role '{model.Role}'." });

            var result = await _userManager.AddToRoleAsync(user, model.Role);
            if (!result.Succeeded) return BadRequest(new { errors = result.Errors });

            return Ok(new
            {
                message = $"Role '{model.Role}' assigned to user '{user.Email}'.",
                userId = user.Id,
                email = user.Email,
                roles = await _userManager.GetRolesAsync(user)
            });
        }

        /// <summary>
        /// POST api/role/remove
        /// Removes a single role from a user. Admin only.
        /// Body: { "userId": "...", "role": "Registrar" }
        /// </summary>
        [HttpPost("remove")]
        public async Task<IActionResult> RemoveRole([FromBody] RemoveRoleModel model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null) return NotFound(new { message = "User not found." });

            if (!await _userManager.IsInRoleAsync(user, model.Role))
                return BadRequest(new { message = $"User does not have the role '{model.Role}'." });

            var result = await _userManager.RemoveFromRoleAsync(user, model.Role);
            if (!result.Succeeded) return BadRequest(new { errors = result.Errors });

            return Ok(new
            {
                message = $"Role '{model.Role}' removed from user '{user.Email}'.",
                userId = user.Id,
                email = user.Email,
                roles = await _userManager.GetRolesAsync(user)
            });
        }

        /// <summary>
        /// POST api/role/bulk-assign
        /// Assigns a role to multiple users in one call. Admin only.
        /// Body: { "role": "Instructor", "userIds": ["id1","id2"], "replaceExistingRoles": false }
        /// </summary>
        [HttpPost("bulk-assign")]
        public async Task<IActionResult> BulkAssignRole([FromBody] BulkAssignRoleModel model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            if (!await _roleManager.RoleExistsAsync(model.Role))
                return BadRequest(new { message = $"Role '{model.Role}' does not exist." });

            var succeeded = new List<string>();
            var failed = new List<object>();

            foreach (var userId in model.UserIds)
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    failed.Add(new { userId, reason = "User not found." });
                    continue;
                }

                if (model.ReplaceExistingRoles)
                {
                    var currentRoles = await _userManager.GetRolesAsync(user);
                    await _userManager.RemoveFromRolesAsync(user, currentRoles);
                }

                if (await _userManager.IsInRoleAsync(user, model.Role))
                {
                    failed.Add(new { userId, email = user.Email, reason = $"Already has role '{model.Role}'." });
                    continue;
                }

                var result = await _userManager.AddToRoleAsync(user, model.Role);
                if (result.Succeeded)
                    succeeded.Add(userId);
                else
                    failed.Add(new { userId, email = user.Email, reason = result.Errors.Select(e => e.Description) });
            }

            return Ok(new
            {
                message = $"Bulk assignment of role '{model.Role}' complete.",
                successCount = succeeded.Count,
                failCount = failed.Count,
                succeeded,
                failed
            });
        }

        // ─────────────────────────────────────────────────────────────────────────
        // QUERIES
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// GET api/role/{roleName}/users
        /// Returns all users who currently hold the given role. Admin only.
        /// </summary>
        [HttpGet("{roleName}/users")]
        public async Task<IActionResult> GetUsersInRole(string roleName)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
                return NotFound(new { message = $"Role '{roleName}' not found." });

            var usersInRole = await _userManager.GetUsersInRoleAsync(roleName);
            var result = new List<UserWithRolesDto>();

            foreach (var user in usersInRole)
            {
                result.Add(new UserWithRolesDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    Username = user.UserName,
                    EmailConfirmed = user.EmailConfirmed,
                    IsLockedOut = user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow,
                    Roles = await _userManager.GetRolesAsync(user)
                });
            }

            return Ok(new { role = roleName, count = result.Count, users = result });
        }

        /// <summary>
        /// GET api/role/users
        /// Returns ALL users in the system alongside their full role list. Admin only.
        /// Useful for building the user-role management UI table.
        /// </summary>
        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsersWithRoles()
        {
            var users = await _userManager.Users.OrderBy(u => u.Email).ToListAsync();
            var result = new List<UserWithRolesDto>();

            foreach (var user in users)
            {
                result.Add(new UserWithRolesDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    Username = user.UserName,
                    EmailConfirmed = user.EmailConfirmed,
                    IsLockedOut = user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow,
                    Roles = await _userManager.GetRolesAsync(user)
                });
            }

            return Ok(new { count = result.Count, users = result });
        }
    }
}
