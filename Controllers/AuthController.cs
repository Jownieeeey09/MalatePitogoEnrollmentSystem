using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MalatePitogoEnrollmentSystem.Data;
using MalatePitogoEnrollmentSystem.Models;

namespace MalatePitogoEnrollmentSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IConfiguration _configuration;
        private readonly EnrollmentDbContext _context;

        public AuthController(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager, RoleManager<IdentityRole> roleManager, IConfiguration configuration, EnrollmentDbContext context)
        {
            _userManager = userManager; _signInManager = signInManager; _roleManager = roleManager; _configuration = configuration; _context = context;
        }

        [HttpPost("register")] [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Register([FromBody] RegisterModel model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (!string.IsNullOrEmpty(model.Role)) { if (!await _roleManager.RoleExistsAsync(model.Role)) return BadRequest(new { message = $"Role '{model.Role}' does not exist" }); }
            var user = new IdentityUser { UserName = model.Email, Email = model.Email, EmailConfirmed = true };
            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded) return BadRequest(new { errors = result.Errors });
            await _userManager.AddToRoleAsync(user, !string.IsNullOrEmpty(model.Role) ? model.Role : "Student");
            if (model.Role == "Student" || string.IsNullOrEmpty(model.Role))
            {
                _context.Students.Add(new Student { UserId = user.Id, StudentNumber = model.StudentNumber ?? GenerateStudentNumber(), FirstName = model.FirstName ?? string.Empty, LastName = model.LastName ?? string.Empty, Email = model.Email, Status = "Active", EnrollmentStatus = "Regular", CreatedAt = DateTime.UtcNow });
                await _context.SaveChangesAsync();
            }
            return Ok(new { message = "User registered successfully", userId = user.Id, email = user.Email, roles = await _userManager.GetRolesAsync(user) });
        }

        [HttpPost("register/student")] [AllowAnonymous]
        public async Task<IActionResult> RegisterStudent([FromBody] StudentRegisterModel model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (await _userManager.FindByEmailAsync(model.Email) != null) return BadRequest(new { message = "Email already registered" });
            var user = new IdentityUser { UserName = model.Email, Email = model.Email, EmailConfirmed = true };
            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded) return BadRequest(new { errors = result.Errors });
            await _userManager.AddToRoleAsync(user, "Student");
            var dobStr = model.DateOfBirth;
            var dob = dobStr.HasValue ? DateTime.SpecifyKind(dobStr.Value, DateTimeKind.Utc) : DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
            
            var student = new Student { UserId = user.Id, StudentNumber = GenerateStudentNumber(), FirstName = model.FirstName, LastName = model.LastName, Email = model.Email, DateOfBirth = dob, Status = "Active", EnrollmentStatus = "New", CreatedAt = DateTime.UtcNow };
            _context.Students.Add(student);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Registration successful.", studentNumber = student.StudentNumber });
        }

        [HttpPost("login")] [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginModel model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null) return Unauthorized(new { message = "Invalid email or password" });
            var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: true);
            if (result.Succeeded)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var token = await GenerateJwtToken(user);
                object? additionalInfo = null;
                if (roles.Contains("Student"))
                {
                    var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == user.Id);
                    if (student != null)
                    {
                        if (student.Status != "Active") return Unauthorized(new { message = "Student account is not active." });
                        additionalInfo = new { studentId = student.Id, studentNumber = student.StudentNumber, fullName = $"{student.FirstName} {student.LastName}", status = student.Status };
                    }
                }
                return Ok(new { token, userId = user.Id, email = user.Email, roles, expiresIn = 7200, userInfo = additionalInfo });
            }
            if (result.IsLockedOut) return Unauthorized(new { message = "Account is locked." });
            if (result.IsNotAllowed) return Unauthorized(new { message = "Please confirm your email." });
            return Unauthorized(new { message = "Invalid email or password" });
        }

        [HttpGet("me")] [Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Unauthorized();
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();
            var roles = await _userManager.GetRolesAsync(user);
            object? roleSpecificInfo = null;
            if (roles.Contains("Student"))
            {
                var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == user.Id);
                if (student != null) roleSpecificInfo = new { studentId = student.Id, studentNumber = student.StudentNumber, firstName = student.FirstName, lastName = student.LastName, status = student.Status, enrollmentStatus = student.EnrollmentStatus };
            }
            return Ok(new { id = user.Id, email = user.Email, username = user.UserName, roles, profile = roleSpecificInfo });
        }

        [HttpPost("change-password")] [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordModel model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var user = await _userManager.FindByIdAsync(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            if (user == null) return NotFound(new { message = "User not found" });
            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            return result.Succeeded ? Ok(new { message = "Password changed successfully" }) : BadRequest(new { errors = result.Errors });
        }

        [HttpPost("reset-password")] [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordModel model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null) return NotFound(new { message = "User not found" });
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
            return result.Succeeded ? Ok(new { message = "Password reset successfully" }) : BadRequest(new { errors = result.Errors });
        }

        [HttpPost("assign-role")] [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AssignRole([FromBody] AssignRoleModel model)
        {
            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null) return NotFound(new { message = "User not found" });
            if (!await _roleManager.RoleExistsAsync(model.Role)) return BadRequest(new { message = $"Role '{model.Role}' does not exist" });
            if (model.ReplaceExistingRoles) await _userManager.RemoveFromRolesAsync(user, await _userManager.GetRolesAsync(user));
            var result = await _userManager.AddToRoleAsync(user, model.Role);
            return result.Succeeded ? Ok(new { message = "Role assigned", userId = user.Id, roles = await _userManager.GetRolesAsync(user) }) : BadRequest(new { errors = result.Errors });
        }

        [HttpPost("remove-role")] [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RemoveRole([FromBody] RemoveRoleModel model)
        {
            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null) return NotFound(new { message = "User not found" });
            var result = await _userManager.RemoveFromRoleAsync(user, model.Role);
            return result.Succeeded ? Ok(new { message = "Role removed", userId = user.Id, roles = await _userManager.GetRolesAsync(user) }) : BadRequest(new { errors = result.Errors });
        }

        [HttpGet("users")] [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _userManager.Users.ToListAsync();
            var userList = new List<object>();
            foreach (var user in users) { var roles = await _userManager.GetRolesAsync(user); userList.Add(new { id = user.Id, email = user.Email, username = user.UserName, emailConfirmed = user.EmailConfirmed, lockoutEnabled = user.LockoutEnabled, lockoutEnd = user.LockoutEnd, roles }); }
            return Ok(userList);
        }

        [HttpPost("lock-user")] [Authorize(Roles = "Admin")]
        public async Task<IActionResult> LockUser([FromBody] LockUserModel model)
        {
            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null) return NotFound(new { message = "User not found" });
            if (model.Lock) { var r = await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue); return r.Succeeded ? Ok(new { message = "User locked" }) : BadRequest(new { errors = r.Errors }); }
            else { var r = await _userManager.SetLockoutEndDateAsync(user, null); if (r.Succeeded) { await _userManager.ResetAccessFailedCountAsync(user); return Ok(new { message = "User unlocked" }); } return BadRequest(new { errors = r.Errors }); }
        }

        [HttpPost("initialize-roles")] [AllowAnonymous]
        public async Task<IActionResult> InitializeRoles()
        {
            var roles = new[] { "Admin", "Registrar", "Student", "Cashier", "Instructor" };
            var created = new List<string>();
            foreach (var role in roles) { if (!await _roleManager.RoleExistsAsync(role)) { var r = await _roleManager.CreateAsync(new IdentityRole(role)); if (r.Succeeded) created.Add(role); } }
            return Ok(new { message = "Roles initialized", createdRoles = created });
        }

        [HttpPost("refresh-token")] [Authorize]
        public async Task<IActionResult> RefreshToken()
        {
            var user = await _userManager.FindByIdAsync(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            if (user == null) return Unauthorized();
            return Ok(new { token = await GenerateJwtToken(user), roles = await _userManager.GetRolesAsync(user), expiresIn = 7200 });
        }

        private async Task<string> GenerateJwtToken(IdentityUser user)
        {
            var jwtSettings = _configuration.GetSection("Jwt");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var roles = await _userManager.GetRolesAsync(user);
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email!),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName!)
            };
            foreach (var role in roles) claims.Add(new Claim(ClaimTypes.Role, role));
            if (roles.Contains("Student"))
            {
                var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == user.Id);
                if (student != null)
                {
                    claims.Add(new Claim("studentId", student.Id.ToString()));
                    claims.Add(new Claim("studentNumber", student.StudentNumber));
                    claims.Add(new Claim("fullName", $"{student.FirstName} {student.LastName}"));
                }
            }
            var token = new JwtSecurityToken(issuer: jwtSettings["Issuer"], audience: jwtSettings["Audience"], claims: claims, expires: DateTime.UtcNow.AddHours(2), signingCredentials: credentials);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private string GenerateStudentNumber()
        {
            return $"{DateTime.Now.Year}-{new Random().Next(1000, 9999)}";
        }
    }
}
