using System.ComponentModel.DataAnnotations;

namespace MalatePitogoEnrollmentSystem.Models
{
    public class Enrollment
    {
        [Key]
        public int EnrollmentId { get; set; }

        public int StudentId { get; set; }
        public Student? Student { get; set; }

        public string Program { get; set; } = string.Empty;
        public int YearLevel { get; set; }
        public string Semester { get; set; } = string.Empty;
        public string StudentType { get; set; } = string.Empty;
        public DateTime EnrollmentDate { get; set; }

        public string Status { get; set; } = "Pending";
        public string? Remarks { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public int? CourseId { get; set; }
        public Course? Course { get; set; }
    }
}
