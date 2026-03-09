using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MalatePitogoEnrollmentSystem.Models
{
    public class Student
    {
        [Key]
        public int Id { get; set; }

        // Link to ASP.NET Identity user
        public string? UserId { get; set; }

        [Required]
        [StringLength(20)]
        public string? StudentNumber { get; set; }

        // Personal Info
        [Required]
        [StringLength(50)]
        public string? FirstName { get; set; }

        [StringLength(50)]
        public string? MiddleName { get; set; }

        [Required]
        [StringLength(50)]
        public string? LastName { get; set; }

        public DateTime DateOfBirth { get; set; }

        [StringLength(10)]
        public string? Gender { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(100)]
        public string? Email { get; set; }

        [StringLength(20)]
        public string? ContactNumber { get; set; }

        [StringLength(200)]
        public string? Address { get; set; }

        [StringLength(100)]
        public string? GuardianName { get; set; }

        [StringLength(20)]
        public string? GuardianContact { get; set; }

        // Academic Info
        public int? CourseId { get; set; }

        public int YearLevel { get; set; } = 1;

        [StringLength(20)]
        public string Status { get; set; } = "Active"; // Active, Inactive, Pending, Graduated

        [StringLength(20)]
        public string EnrollmentStatus { get; set; } = "Regular"; // Regular, Irregular, New, LOA

        // Audit fields
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        [ForeignKey("CourseId")]
        public Course? Course { get; set; }

        public ICollection<Enrollment>? Enrollments { get; set; }
    }
}