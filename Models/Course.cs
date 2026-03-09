using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MalatePitogoEnrollmentSystem.Models
{
    public class Course
    {
        [Key]
        public int CourseId { get; set; }

        [Required]
        [StringLength(20)]
        public string CourseCode { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string CourseName { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        // Academic Info
        [Required]
        [StringLength(50)]
        public string Department { get; set; } = string.Empty;

        [Range(1, 6)]
        public int YearLevel { get; set; } = 1;

        [Range(1, 12)]
        public int Credits { get; set; }

        public int? InstructorId { get; set; }

        // Status
        public bool IsActive { get; set; } = true;

        // Audit fields
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string? CreatedBy { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public string? UpdatedBy { get; set; }

        // Navigation properties
        [ForeignKey("InstructorId")]
        public Instructor? Instructor { get; set; }

        public ICollection<Student>? Students { get; set; }

        public ICollection<Enrollment>? Enrollments { get; set; }

        public ICollection<Course>? Subjects { get; set; }
    }
}