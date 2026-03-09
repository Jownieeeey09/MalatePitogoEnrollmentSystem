using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MalatePitogoEnrollmentSystem.Data;
using MalatePitogoEnrollmentSystem.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace MalatePitogoEnrollmentSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EnrollmentController : Controller
    {
        private readonly EnrollmentDbContext _context;
        private readonly IAuthorizationService _authorizationService;

        public EnrollmentController(EnrollmentDbContext context, IAuthorizationService authorizationService)
        {
            _context = context;
            _authorizationService = authorizationService;
        }

        [HttpGet("Index")]
        [AllowAnonymous]
        public IActionResult Index() { return View(); }

        [HttpPost("submit")]
        [AllowAnonymous]
        public async Task<IActionResult> SubmitEnrollment([FromBody] EnrollmentDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(new { message = "Invalid data." });
            var student = new Student
            {
                FirstName = dto.FirstName, MiddleName = dto.MiddleName, LastName = dto.LastName,
                DateOfBirth = dto.DateOfBirth, Gender = dto.Gender, Email = dto.Email,
                ContactNumber = dto.ContactNumber, Address = dto.Address,
                GuardianName = dto.GuardianName, GuardianContact = dto.GuardianContact
            };
            _context.Students.Add(student);
            await _context.SaveChangesAsync();
            var enrollment = new MalatePitogoEnrollmentSystem.Models.Enrollment
            {
                StudentId = student.Id, Program = dto.Program, YearLevel = dto.YearLevel,
                Semester = dto.Semester.ToString(), StudentType = dto.StudentType,
                EnrollmentDate = DateTime.UtcNow, Status = "Pending"
            };
            _context.Enrollments.Add(enrollment);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Enrollment submitted successfully!", studentId = student.Id, enrollmentId = enrollment.EnrollmentId });
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Registrar")]
        public async Task<ActionResult<IEnumerable<MalatePitogoEnrollmentSystem.Models.Enrollment>>> GetEnrollments(
            [FromQuery] string? status = null, [FromQuery] int? studentId = null)
        {
            var query = _context.Enrollments.Include(e => e.Student).Include(e => e.Course).AsQueryable();
            if (!string.IsNullOrEmpty(status)) query = query.Where(e => e.Status == status);
            if (studentId.HasValue) query = query.Where(e => e.StudentId == studentId.Value);
            return await query.ToListAsync();
        }

        [HttpGet("my-enrollments")]
        [Authorize(Roles = "Student")]
        public async Task<ActionResult<IEnumerable<MalatePitogoEnrollmentSystem.Models.Enrollment>>> GetMyEnrollments()
        {
            var studentId = GetCurrentStudentId();
            if (studentId == null) return Unauthorized(new { message = "Student ID not found in token." });
            return await _context.Enrollments.Include(e => e.Student).Include(e => e.Course)
                .Where(e => e.StudentId == studentId.Value).ToListAsync();
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<MalatePitogoEnrollmentSystem.Models.Enrollment>> GetEnrollment(int id)
        {
            var enrollment = await _context.Enrollments.Include(e => e.Student).Include(e => e.Course)
                .FirstOrDefaultAsync(e => e.EnrollmentId == id);
            if (enrollment == null) return NotFound();
            if (User.IsInRole("Student"))
            {
                var studentId = GetCurrentStudentId();
                if (studentId == null || enrollment.StudentId != studentId.Value) return Forbid();
            }
            return enrollment;
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Registrar")]
        public async Task<ActionResult<MalatePitogoEnrollmentSystem.Models.Enrollment>> CreateEnrollment(
            MalatePitogoEnrollmentSystem.Models.Enrollment enrollment)
        {
            if (!await _context.Students.AnyAsync(s => s.Id == enrollment.StudentId))
                return BadRequest(new { message = "Student not found." });
            enrollment.EnrollmentDate = DateTime.UtcNow;
            enrollment.Status = "Pending";
            _context.Enrollments.Add(enrollment);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetEnrollment), new { id = enrollment.EnrollmentId }, enrollment);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Registrar")]
        public async Task<IActionResult> UpdateEnrollment(int id,
            MalatePitogoEnrollmentSystem.Models.Enrollment enrollment)
        {
            if (id != enrollment.EnrollmentId) return BadRequest();
            _context.Entry(enrollment).State = EntityState.Modified;
            try { await _context.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { if (!EnrollmentExists(id)) return NotFound(); throw; }
            return NoContent();
        }

        [HttpPatch("{id}/status")]
        [Authorize(Roles = "Admin,Registrar")]
        public async Task<IActionResult> UpdateEnrollmentStatus(int id, [FromBody] UpdateStatusRequest request)
        {
            var enrollment = await _context.Enrollments.FindAsync(id);
            if (enrollment == null) return NotFound();
            enrollment.Status = request.Status;
            if (!string.IsNullOrEmpty(request.Remarks)) enrollment.Remarks = request.Remarks;
            enrollment.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Status updated successfully.", enrollmentId = enrollment.EnrollmentId, newStatus = enrollment.Status });
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteEnrollment(int id)
        {
            var enrollment = await _context.Enrollments.FindAsync(id);
            if (enrollment == null) return NotFound();
            _context.Enrollments.Remove(enrollment);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private bool EnrollmentExists(int id) => _context.Enrollments.Any(e => e.EnrollmentId == id);
        private int? GetCurrentStudentId()
        {
            var claim = User.FindFirst("student_id") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out var id) ? id : null;
        }
        private string? GetCurrentUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
    }

    public class UpdateStatusRequest
    {
        public string Status { get; set; } = string.Empty;
        public string? Remarks { get; set; }
    }
}


