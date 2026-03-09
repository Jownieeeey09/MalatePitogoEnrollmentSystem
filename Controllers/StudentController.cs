using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MalatePitogoEnrollmentSystem.Data;
using MalatePitogoEnrollmentSystem.Models;
using System.Security.Claims;

namespace MalatePitogoEnrollmentSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StudentsController : ControllerBase
    {
        private readonly EnrollmentDbContext _context;

        public StudentsController(EnrollmentDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // PUBLIC ENDPOINTS - No authentication required
        // ============================================================

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> RegisterStudent([FromBody] StudentRegistrationDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { message = "Invalid data.", errors = ModelState });

            if (await _context.Students.AnyAsync(s => s.Email == dto.Email))
                return BadRequest(new { message = "Email already registered." });

            var student = new Student
            {
                FirstName = dto.FirstName,
                MiddleName = dto.MiddleName,
                LastName = dto.LastName,
                DateOfBirth = dto.DateOfBirth,
                Gender = dto.Gender,
                Email = dto.Email,
                ContactNumber = dto.ContactNumber,
                Address = dto.Address,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.Students.Add(student);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Student registered successfully!",
                ID = student.Id,
                email = student.Email
            });
        }

        [HttpGet("check-email/{email}")]
        [AllowAnonymous]
        public async Task<IActionResult> CheckEmailAvailability(string email)
        {
            var exists = await _context.Students.AnyAsync(s => s.Email == email);
            return Ok(new { email, available = !exists });
        }

        // ============================================================
        // STUDENT ENDPOINTS - Students can view/update own data
        // ============================================================

        [HttpGet("my-profile")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetMyProfile()
        {
            var studentId = GetCurrentStudentId();
            if (studentId == null)
                return Unauthorized(new { message = "Student ID not found in token." });

            var student = await _context.Students
                .Include(s => s.Enrollments)
                .FirstOrDefaultAsync(s => s.Id == studentId.Value);

            if (student == null)
                return NotFound(new { message = "Student profile not found." });

            return Ok(student);
        }

        [HttpPut("my-profile")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateStudentProfileDto dto)
        {
            var studentId = GetCurrentStudentId();
            if (studentId == null)
                return Unauthorized(new { message = "Student ID not found in token." });

            var student = await _context.Students.FindAsync(studentId.Value);
            if (student == null)
                return NotFound();

            student.ContactNumber = dto.ContactNumber ?? student.ContactNumber;
            student.Email        = dto.Email        ?? student.Email;
            student.Address      = dto.Address      ?? student.Address;
            student.GuardianContact = dto.GuardianContact ?? student.GuardianContact;
            student.UpdatedAt    = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Profile updated successfully.", student });
        }

        [HttpGet("my-enrollments")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetMyEnrollments()
        {
            var studentId = GetCurrentStudentId();
            if (studentId == null)
                return Unauthorized(new { message = "Student ID not found in token." });

            var enrollments = await _context.Enrollments
                .Where(e => e.StudentId == studentId.Value)
                .Include(e => e.Course)
                .ToListAsync();

            return Ok(enrollments);
        }

        // ============================================================
        // ADMIN/REGISTRAR ENDPOINTS - View all students
        // ============================================================

        [HttpGet]
        [Authorize(Roles = "Admin,Registrar")]
        public async Task<IActionResult> GetAllStudents(
            [FromQuery] string? searchTerm = null,
            [FromQuery] string? program = null,
            [FromQuery] int? yearLevel = null,
            [FromQuery] bool? isActive = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var query = _context.Students
                .Include(s => s.Enrollments)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(s =>
                    s.FirstName.Contains(searchTerm) ||
                    s.LastName.Contains(searchTerm) ||
                    s.Email.Contains(searchTerm));
            }

            if (!string.IsNullOrEmpty(program))
                query = query.Where(s => s.Enrollments!.Any(e => e.Program == program));

            if (yearLevel.HasValue)
                // FIX (was CS0019): YearLevel is now int — both sides are int, comparison is valid
                query = query.Where(s => s.Enrollments!.Any(e => e.YearLevel == yearLevel.Value));

            if (isActive.HasValue)
                query = query.Where(s => s.IsActive == isActive.Value);

            var totalCount = await query.CountAsync();
            var students = await query
                .OrderBy(s => s.LastName)
                .ThenBy(s => s.FirstName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new
            {
                message = "Students retrieved successfully",
                requestedBy = userId,
                pagination = new
                {
                    currentPage = page,
                    pageSize,
                    totalCount,
                    totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                },
                data = students
            });
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetStudentById(int id)
        {
            var student = await _context.Students
                .Include(s => s.Enrollments!)           // ! suppresses CS8604 null warning
                    .ThenInclude(e => e.Course)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (student == null)
                return NotFound(new { message = "Student not found." });

            if (User.IsInRole("Student"))
            {
                var studentId = GetCurrentStudentId();
                if (studentId == null || id != studentId.Value)
                    return Forbid();
            }

            return Ok(student);
        }

        // ============================================================
        // ADMIN/REGISTRAR ENDPOINTS - Create/Update/Delete student
        // ============================================================

        [HttpPost]
        [Authorize(Roles = "Admin,Registrar")]
        public async Task<IActionResult> CreateStudent([FromBody] Student student)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (await _context.Students.AnyAsync(s => s.Email == student.Email))
                return BadRequest(new { message = "Email already exists." });

            student.CreatedAt = DateTime.UtcNow;
            student.IsActive = true;

            _context.Students.Add(student);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetStudentById), new { id = student.Id }, student);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Registrar")]
        public async Task<IActionResult> UpdateStudent(int id, [FromBody] Student student)
        {
            if (id != student.Id)
                return BadRequest(new { message = "ID mismatch." });

            var existingStudent = await _context.Students.FindAsync(id);
            if (existingStudent == null)
                return NotFound();

            if (await _context.Students.AnyAsync(s => s.Email == student.Email && s.Id != id))
                return BadRequest(new { message = "Email already exists." });

            _context.Entry(existingStudent).CurrentValues.SetValues(student);
            existingStudent.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Student updated successfully.", student = existingStudent });
        }

        [HttpPatch("{id}/deactivate")]
        [Authorize(Roles = "Admin,Registrar")]
        public async Task<IActionResult> DeactivateStudent(int id)
        {
            var student = await _context.Students.FindAsync(id);
            if (student == null) return NotFound();

            student.IsActive = false;
            student.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Student deactivated successfully." });
        }

        [HttpPatch("{id}/activate")]
        [Authorize(Roles = "Admin,Registrar")]
        public async Task<IActionResult> ActivateStudent(int id)
        {
            var student = await _context.Students.FindAsync(id);
            if (student == null) return NotFound();

            student.IsActive = true;
            student.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Student activated successfully." });
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteStudent(int id)
        {
            var student = await _context.Students
                .Include(s => s.Enrollments)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (student == null)
                return NotFound();

            // FIX (CS8602 warnings): null-guard before Any()
            if (student.Enrollments != null && student.Enrollments.Any())
            {
                return BadRequest(new
                {
                    message = "Cannot delete student with existing enrollments. Deactivate instead.",
                    enrollmentCount = student.Enrollments.Count
                });
            }

            _context.Students.Remove(student);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Student deleted successfully." });
        }

        // ============================================================
        // FACULTY ENDPOINTS
        // ============================================================

        [HttpGet("my-students")]
        [Authorize(Roles = "Faculty")]
        public async Task<IActionResult> GetMyStudents()
        {
            var instructorId = GetCurrentInstructorId();
            if (instructorId == null)
                return Unauthorized(new { message = "Instructor ID not found in token." });

            var students = await _context.Students
                // FIX (CS8620 + CS8602): use ! operator to assert non-null navigation chains
                .Where(s => s.Enrollments!.Any(e => e.Course!.InstructorId == instructorId.Value))
                .Include(s => s.Enrollments!)
                    .ThenInclude(e => e.Course)
                .Distinct()
                .ToListAsync();

            return Ok(new
            {
                message = "Students retrieved successfully",
                count = students.Count,
                data = students
            });
        }

        // ============================================================
        // STATISTICS ENDPOINTS
        // ============================================================

        [HttpGet("statistics")]
        [Authorize(Roles = "Admin,Registrar")]
        public async Task<IActionResult> GetStudentStatistics()
        {
            var totalStudents  = await _context.Students.CountAsync();
            var activeStudents = await _context.Students.CountAsync(s => s.IsActive);
            var inactiveStudents = totalStudents - activeStudents;

            var studentsByProgram = await _context.Enrollments
                .GroupBy(e => e.Program)
                .Select(g => new { program = g.Key, count = g.Select(e => e.StudentId).Distinct().Count() })
                .ToListAsync();

            var studentsByYearLevel = await _context.Enrollments
                .GroupBy(e => e.YearLevel)
                .Select(g => new { yearLevel = g.Key, count = g.Select(e => e.StudentId).Distinct().Count() })
                .ToListAsync();

            return Ok(new
            {
                totalStudents,
                activeStudents,
                inactiveStudents,
                byProgram = studentsByProgram,
                byYearLevel = studentsByYearLevel
            });
        }

        // ============================================================
        // HELPER METHODS
        // ============================================================

        private int? GetCurrentStudentId()
        {
            var claim = User.FindFirst("student_id")
                     ?? User.FindFirst(ClaimTypes.NameIdentifier);

            return claim != null && int.TryParse(claim.Value, out var id) ? id : null;
        }

        private int? GetCurrentInstructorId()
        {
            var claim = User.FindFirst("instructor_id")
                     ?? User.FindFirst("InstructorId");

            return claim != null && int.TryParse(claim.Value, out var id) ? id : null;
        }

        private string? GetCurrentUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value;
        }
    }

    // ============================================================
    // SUPPORTING DTOs
    // ============================================================

    public class StudentRegistrationDto
    {
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string LastName { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }
        public int Age { get; set; }
        public string Gender { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string? GuardianName { get; set; }
        public string? GuardianContact { get; set; }
    }

    public class UpdateStudentProfileDto
    {
        public string? Email { get; set; }
        public string? ContactNumber { get; set; }
        public string? Address { get; set; }
        public string? GuardianContact { get; set; }
    }
}