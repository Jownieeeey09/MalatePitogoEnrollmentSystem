using System.ComponentModel.DataAnnotations;

namespace MalatePitogoEnrollmentSystem.Models
{
    public class RegisterModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string Password { get; set; } = string.Empty;

        public string? Role { get; set; }
        public string? StudentNumber { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
    }

    public class StudentRegisterModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string Password { get; set; } = string.Empty;

        [Required]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        public string LastName { get; set; } = string.Empty;

        public DateTime? DateOfBirth { get; set; }
    }

    public class LoginModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class ChangePasswordModel
    {
        [Required]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string NewPassword { get; set; } = string.Empty;
    }

    public class ResetPasswordModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string NewPassword { get; set; } = string.Empty;
    }

    public class AssignRoleModel
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = string.Empty;

        public bool ReplaceExistingRoles { get; set; } = false;
    }

    public class RemoveRoleModel
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = string.Empty;
    }

    public class LockUserModel
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public bool Lock { get; set; }
    }

    // ─── RBAC Models ────────────────────────────────────────────────────────────

    public class CreateRoleModel
    {
        [Required]
        [StringLength(256, MinimumLength = 2)]
        public string Name { get; set; } = string.Empty;

        /// <summary>Optional human-readable description stored as a normalized name note.</summary>
        public string? Description { get; set; }
    }

    public class UpdateRoleModel
    {
        [Required]
        [StringLength(256, MinimumLength = 2)]
        public string NewName { get; set; } = string.Empty;
    }

    public class BulkAssignRoleModel
    {
        [Required]
        public string Role { get; set; } = string.Empty;

        [Required]
        public List<string> UserIds { get; set; } = new();

        /// <summary>When true, removes all existing roles before assigning the new one.</summary>
        public bool ReplaceExistingRoles { get; set; } = false;
    }

    public class UserWithRolesDto
    {
        public string Id { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Username { get; set; }
        public bool EmailConfirmed { get; set; }
        public bool IsLockedOut { get; set; }
        public IList<string> Roles { get; set; } = new List<string>();
    }
}