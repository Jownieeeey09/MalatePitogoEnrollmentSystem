using System.ComponentModel.DataAnnotations;
using MalatePitogoEnrollmentSystem.Data;
using MalatePitogoEnrollmentSystem.Models;

namespace MalatePitogoEnrollmentSystem.Models
{
    public class Instructor
    {
        [Key]
        public int Id { get; set; }

        // Identity link (optional)
        public string? UserId { get; set; }

        // Personal Info
        [Required]
        [StringLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [StringLength(50)]
        public string? MiddleName { get; set; }

        [Required]
        [StringLength(50)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [StringLength(20)]
        public string? Phone { get; set; }

        // Academic Info
        [Required]
        [StringLength(50)]
        public string Department { get; set; } = string.Empty;

        [StringLength(50)]
        public string? Title { get; set; } // e.g., "Professor", "Associate Professor"

        [StringLength(1000)]
        public string? Bio { get; set; }

        // Office Info
        [StringLength(100)]
        public string? OfficeLocation { get; set; }

        [StringLength(200)]
        public string? OfficeHours { get; set; }

        // Status & Audit
        public bool IsActive { get; set; } = true;

        public DateTime? HireDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public ICollection<Course>? Courses { get; set; }
    }
}