using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MalatePitogoEnrollmentSystem.Data;
using MalatePitogoEnrollmentSystem.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

[ApiController]
[Route("api/[controller]")]
public class InstructorsController : ControllerBase
{
    private readonly EnrollmentDbContext _context;

    public InstructorsController(EnrollmentDbContext context)
    {
        _context = context;
    }

    // ============================================================
    // PUBLIC ENDPOINTS - View instructors (read-only)
    // ============================================================

    /// <summary>
    /// Get all instructors - Public access for browsing
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<Instructor>>> GetInstructors(
        [FromQuery] string? department = null,
        [FromQuery] bool? isActive = null)
    {
        var query = _context.Instructors
            .Include(i => i.Courses)
            .AsQueryable();

        // Filter by department if provided
        if (!string.IsNullOrEmpty(department))
        {
            query = query.Where(i => i.Department == department);
        }

        // Filter by active status if provided
        if (isActive.HasValue)
        {
            query = query.Where(i => i.IsActive == isActive.Value);
        }

        var instructors = await query.ToListAsync();

        // For non-authenticated users, return limited info
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return Ok(instructors.Select(i => new
            {
                i.Id,
                i.FirstName,
                i.LastName,
                i.Department,
                i.Title,
                CourseCount = i.Courses?.Count ?? 0
            }));
        }

        return instructors;
    }

    /// <summary>
    /// Get specific instructor - Public access
    /// </summary>
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<Instructor>> GetInstructor(int id)
    {
        var instructor = await _context.Instructors
            .Include(i => i.Courses)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (instructor == null)
        {
            return NotFound();
        }

        // For non-authenticated users, return limited info
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return Ok(new
            {
                instructor.Id,
                instructor.FirstName,
                instructor.LastName,
                instructor.Department,
                instructor.Title,
                instructor.Bio,
                Courses = instructor.Courses?.Select(c => new { c.CourseId, c.CourseName, c.CourseCode })
            });
        }

        // Admin and Faculty get full details including contact info
        return instructor;
    }

    // ============================================================
    // FACULTY ENDPOINTS - Instructors can view/update their own profile
    // ============================================================

    /// <summary>
    /// Get current instructor's own profile
    /// </summary>
    [HttpGet("my-profile")]
    [Authorize(Roles = "Faculty")]
    public async Task<ActionResult<Instructor>> GetMyProfile()
    {
        var instructorId = GetCurrentInstructorId();
        if (instructorId == null)
        {
            return Unauthorized(new { message = "Instructor ID not found in token." });
        }

        var instructor = await _context.Instructors
            .Include(i => i.Courses)
            .FirstOrDefaultAsync(i => i.Id == instructorId.Value);

        if (instructor == null)
        {
            return NotFound(new { message = "Instructor profile not found." });
        }

        return instructor;
    }

    /// <summary>
    /// Update own profile - Faculty can update their own information
    /// </summary>
    [HttpPut("my-profile")]
    [Authorize(Roles = "Faculty")]
    public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateInstructorProfileDto dto)
    {
        var instructorId = GetCurrentInstructorId();
        if (instructorId == null)
        {
            return Unauthorized(new { message = "Instructor ID not found in token." });
        }

        var instructor = await _context.Instructors.FindAsync(instructorId.Value);
        if (instructor == null)
        {
            return NotFound();
        }

        // Faculty can only update specific fields
        instructor.Bio = dto.Bio ?? instructor.Bio;
        instructor.Email = dto.Email ?? instructor.Email;
        instructor.Phone = dto.Phone ?? instructor.Phone;
        instructor.OfficeLocation = dto.OfficeLocation ?? instructor.OfficeLocation;
        instructor.OfficeHours = dto.OfficeHours ?? instructor.OfficeHours;

        await _context.SaveChangesAsync();

        return Ok(new { message = "Profile updated successfully.", instructor });
    }

    /// <summary>
    /// Get courses taught by current instructor
    /// </summary>
    [HttpGet("my-courses")]
    [Authorize(Roles = "Faculty")]
    public async Task<ActionResult<IEnumerable<Course>>> GetMyCourses()
    {
        var instructorId = GetCurrentInstructorId();
        if (instructorId == null)
        {
            return Unauthorized(new { message = "Instructor ID not found in token." });
        }

        var courses = await _context.Courses
            .Where(c => c.InstructorId == instructorId.Value)
            .Include(c => c.Enrollments)
            .ToListAsync();

        return courses;
    }

    // ============================================================
    // ADMIN ENDPOINTS - Full CRUD operations
    // ============================================================

    /// <summary>
    /// Create new instructor - Admin only
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Instructor>> CreateInstructor([FromBody] Instructor instructor)
    {
        // Validate email uniqueness
        if (await _context.Instructors.AnyAsync(i => i.Email == instructor.Email))
        {
            return BadRequest(new { message = "Email already exists." });
        }

        // Set default values
        instructor.IsActive = true;
        instructor.HireDate = instructor.HireDate == default ? DateTime.UtcNow : instructor.HireDate;

        _context.Instructors.Add(instructor);
        await _context.SaveChangesAsync();

        // Optional: Create a user account for the instructor
        // await CreateInstructorUserAccount(instructor);

        return CreatedAtAction(nameof(GetInstructor), new { id = instructor.Id }, instructor);
    }

    /// <summary>
    /// Update instructor - Admin only (full update)
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateInstructor(int id, [FromBody] Instructor instructor)
    {
        if (id != instructor.Id)
        {
            return BadRequest(new { message = "ID mismatch." });
        }

        var existingInstructor = await _context.Instructors.FindAsync(id);
        if (existingInstructor == null)
        {
            return NotFound();
        }

        // Check email uniqueness (excluding current instructor)
        if (await _context.Instructors.AnyAsync(i => i.Email == instructor.Email && i.Id != id))
        {
            return BadRequest(new { message = "Email already exists." });
        }

        _context.Entry(existingInstructor).CurrentValues.SetValues(instructor);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!InstructorExists(id))
            {
                return NotFound();
            }
            throw;
        }

        return Ok(new { message = "Instructor updated successfully.", instructor = existingInstructor });
    }

    /// <summary>
    /// Deactivate instructor - Admin only (soft delete)
    /// </summary>
    [HttpPatch("{id}/deactivate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeactivateInstructor(int id)
    {
        var instructor = await _context.Instructors.FindAsync(id);
        if (instructor == null)
        {
            return NotFound();
        }

        instructor.IsActive = false;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Instructor deactivated successfully." });
    }

    /// <summary>
    /// Reactivate instructor - Admin only
    /// </summary>
    [HttpPatch("{id}/activate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ActivateInstructor(int id)
    {
        var instructor = await _context.Instructors.FindAsync(id);
        if (instructor == null)
        {
            return NotFound();
        }

        instructor.IsActive = true;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Instructor activated successfully." });
    }

    /// <summary>
    /// Delete instructor permanently - Admin only (hard delete)
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteInstructor(int id)
    {
        var instructor = await _context.Instructors
            .Include(i => i.Courses)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (instructor == null)
        {
            return NotFound();
        }

        // Check if instructor has active courses
        if (instructor.Courses != null && instructor.Courses.Any())
        {
            return BadRequest(new 
            { 
                message = "Cannot delete instructor with assigned courses. Reassign or remove courses first.",
                courseCount = instructor.Courses.Count
            });
        }

        _context.Instructors.Remove(instructor);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Instructor deleted successfully." });
    }

    // ============================================================
    // REGISTRAR ENDPOINTS - View and assign instructors
    // ============================================================

    /// <summary>
    /// Assign instructor to course - Admin or Registrar
    /// </summary>
    [HttpPost("{instructorId}/assign-course/{courseId}")]
    [Authorize(Roles = "Admin,Registrar")]
    public async Task<IActionResult> AssignCourse(int instructorId, int courseId)
    {
        var instructor = await _context.Instructors.FindAsync(instructorId);
        if (instructor == null)
        {
            return NotFound(new { message = "Instructor not found." });
        }

        if (!instructor.IsActive)
        {
            return BadRequest(new { message = "Cannot assign courses to inactive instructor." });
        }

        var course = await _context.Courses.FindAsync(courseId);
        if (course == null)
        {
            return NotFound(new { message = "Course not found." });
        }

        course.InstructorId = instructorId;
        await _context.SaveChangesAsync();

        return Ok(new 
        { 
            message = "Course assigned successfully.",
            instructor = new { instructor.Id, instructor.FirstName, instructor.LastName },
            course = new { course.CourseId, course.CourseName, course.CourseCode }
        });
    }

    /// <summary>
    /// Get instructors by department - Admin or Registrar
    /// </summary>
    [HttpGet("department/{department}")]
    [Authorize(Roles = "Admin,Registrar,Faculty")]
    public async Task<ActionResult<IEnumerable<Instructor>>> GetInstructorsByDepartment(string department)
    {
        var instructors = await _context.Instructors
            .Where(i => i.Department == department && i.IsActive)
            .Include(i => i.Courses)
            .ToListAsync();

        return instructors;
    }

    /// <summary>
    /// Get instructor statistics - Admin or Registrar
    /// </summary>
    [HttpGet("{id}/statistics")]
    [Authorize(Roles = "Admin,Registrar")]
    public async Task<ActionResult> GetInstructorStatistics(int id)
    {
        var instructor = await _context.Instructors
            .Include(i => i.Courses)
                .ThenInclude(c => c.Enrollments)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (instructor == null)
        {
            return NotFound();
        }

        var stats = new
        {
            instructorId = instructor.Id,
            instructorName = $"{instructor.FirstName} {instructor.LastName}",
            department = instructor.Department,
            totalCourses = instructor.Courses?.Count ?? 0,
            activeCourses = instructor.Courses?.Count(c => c.IsActive) ?? 0,
            totalStudents = instructor.Courses?.Sum(c => c.Enrollments?.Count ?? 0) ?? 0,
            courseDetails = instructor.Courses?.Select(c => new
            {
                c.CourseCode,
                c.CourseName,
                studentCount = c.Enrollments?.Count ?? 0,
                c.IsActive
            })
        };

        return Ok(stats);
    }

    // ============================================================
    // HELPER METHODS
    // ============================================================

    private bool InstructorExists(int id)
    {
        return _context.Instructors.Any(e => e.Id == id);
    }

    /// <summary>
    /// Get the current instructor's ID from JWT token
    /// </summary>
    private int? GetCurrentInstructorId()
    {
        var instructorIdClaim = User.FindFirst("instructor_id") 
            ?? User.FindFirst("InstructorId");
        
        if (instructorIdClaim != null && int.TryParse(instructorIdClaim.Value, out var instructorId))
        {
            return instructorId;
        }

        return null;
    }

    /// <summary>
    /// Get the current user ID from JWT token
    /// </summary>
    private string? GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? User.FindFirst("sub")?.Value;
    }
}

// ============================================================
// SUPPORTING DTOs
// ============================================================

/// <summary>
/// DTO for instructor profile updates by faculty
/// </summary>
public class UpdateInstructorProfileDto
{
    public string? Bio { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? OfficeLocation { get; set; }
    public string? OfficeHours { get; set; }
}