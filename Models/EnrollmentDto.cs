namespace MalatePitogoEnrollmentSystem.Models
{
    public class EnrollmentDto
    {
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string LastName { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }
        public string Gender { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string? GuardianName { get; set; }
        public string? GuardianContact { get; set; }
        public string Program { get; set; } = string.Empty;
        public int YearLevel { get; set; } = 1;
        public int Semester { get; set; } = 1;
        public string StudentType { get; set; } = "Regular";
    }
}