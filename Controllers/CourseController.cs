using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using MalatePitogoEnrollmentSystem.Data;
using MalatePitogoEnrollmentSystem.Models;

namespace MalatePitogoEnrollmentSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CoursesController : ControllerBase
    {
        private readonly EnrollmentDbContext _context;

        public CoursesController(EnrollmentDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Get all courses (public - no authentication required)
        /// Students can browse available courses before enrollment
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<Course>>> GetAllCourses(
            [FromQuery] string? department = null,
            [FromQuery] int? yearLevel = null,
            [FromQuery] bool activeOnly = true)
        {
            var query = _context.Courses.AsQueryable();

            // Filter by department if provided
            if (!string.IsNullOrEmpty(department))
            {
                query = query.Where(c => c.Department == department);
            }

            // Filter by year level if provided
            if (yearLevel.HasValue)
            {
                query = query.Where(c => c.YearLevel == yearLevel.Value);
            }

            // Show only active courses by default
            if (activeOnly)
            {
                query = query.Where(c => c.IsActive);
            }

            var courses = await query
                .OrderBy(c => c.Department)
                .ThenBy(c => c.YearLevel)
                .ThenBy(c => c.CourseCode)
                .ToListAsync();

            return Ok(courses);
        }

        /// <summary>
        /// Get course by ID (public)
        /// Anyone can view course details
        /// </summary>
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<Course>> GetCourseById(int id)
        {
            var course = await _context.Courses
                
                .FirstOrDefaultAsync(c => c.CourseId == id);

            if (course == null)
            {
                return NotFound(new { message = "Course not found" });
            }

            return Ok(course);
        }

        /// <summary>
        /// Get course by code (public)
        /// Useful for course catalog lookups
        /// </summary>
        [HttpGet("code/{code}")]
        [AllowAnonymous]
        public async Task<ActionResult<Course>> GetCourseByCode(string code)
        {
            var course = await _context.Courses
                
                .FirstOrDefaultAsync(c => c.CourseCode == code);

            if (course == null)
            {
                return NotFound(new { message = $"Course with code '{code}' not found" });
            }

            return Ok(course);
        }

        /// <summary>
        /// Get all departments (public)
        /// Useful for filtering courses
        /// </summary>
        [HttpGet("departments")]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<string>>> GetDepartments()
        {
            var departments = await _context.Courses
                .Where(c => !string.IsNullOrEmpty(c.Department))
                .Select(c => c.Department)
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync();

            return Ok(departments);
        }

        /// <summary>
        /// Get course statistics (authenticated users only)
        /// </summary>
        [HttpGet("{id}/statistics")]
        [Authorize(Roles = "Admin,Registrar")]
        public async Task<ActionResult<object>> GetCourseStatistics(int id)
        {
            var course = await _context.Courses.FindAsync(id);
            
            if (course == null)
            {
                return NotFound(new { message = "Course not found" });
            }

            var totalStudents = await _context.Students
                .CountAsync(s => s.CourseId == id && s.Status == "Active");

            var enrollmentsByYear = await _context.Students
                .Where(s => s.CourseId == id && s.Status == "Active")
                .GroupBy(s => s.YearLevel)
                .Select(g => new
                {
                    YearLevel = g.Key,
                    Count = g.Count()
                })
                .OrderBy(x => x.YearLevel)
                .ToListAsync();

            return Ok(new
            {
                CourseId = course.CourseId,
                CourseName = course.CourseName,
                TotalStudents = totalStudents,
                EnrollmentsByYear = enrollmentsByYear
            });
        }

        /// <summary>
        /// Create new course (Admin and Registrar only)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin,Registrar")]
        public async Task<ActionResult<Course>> CreateCourse([FromBody] CourseCreateDto courseDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Check if course code already exists
            var existingCourse = await _context.Courses
                .FirstOrDefaultAsync(c => c.CourseCode == courseDto.CourseCode);

            if (existingCourse != null)
            {
                return BadRequest(new { message = $"Course with code '{courseDto.CourseCode}' already exists" });
            }

            var course = new Course
            {
                CourseCode = courseDto.CourseCode,
                CourseName = courseDto.CourseName,
                Description = courseDto.Description,
                Department = courseDto.Department,
                YearLevel = courseDto.YearLevel,
                IsActive = courseDto.IsActive ?? true,
                CreatedAt = DateTime.Now,
                CreatedBy = GetCurrentUserId()
            };

            _context.Courses.Add(course);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetCourseById), 
                new { id = course.CourseId }, course);
        }

        /// <summary>
        /// Update course (Admin and Registrar only)
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Registrar")]
        public async Task<IActionResult> UpdateCourse(int id, [FromBody] CourseUpdateDto courseDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var course = await _context.Courses.FindAsync(id);

            if (course == null)
            {
                return NotFound(new { message = "Course not found" });
            }

            // Check if new course code conflicts with existing course (excluding current)
            if (courseDto.CourseCode != course.CourseCode)
            {
                var codeExists = await _context.Courses
                    .AnyAsync(c => c.CourseCode == courseDto.CourseCode && c.CourseId != id);

                if (codeExists)
                {
                    return BadRequest(new { message = $"Course code '{courseDto.CourseCode}' is already in use" });
                }
            }

            // Update fields
            course.CourseCode = courseDto.CourseCode;
            course.CourseName = courseDto.CourseName;
            course.Description = courseDto.Description;
            course.Department = courseDto.Department;
            course.YearLevel = courseDto.YearLevel;
            course.IsActive = courseDto.IsActive;
            course.UpdatedAt = DateTime.Now;
            course.UpdatedBy = GetCurrentUserId();

            await _context.SaveChangesAsync();

            return Ok(new 
            { 
                message = "Course updated successfully", 
                course 
            });
        }

        /// <summary>
        /// Soft delete course (Admin only)
        /// Deactivates the course rather than permanently deleting it
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteCourse(int id)
        {
            var course = await _context.Courses
                .Include(c => c.Students)
                .FirstOrDefaultAsync(c => c.CourseId == id);

            if (course == null)
            {
                return NotFound(new { message = "Course not found" });
            }

            // Check if there are active students enrolled in this course
            var activeStudents = course.Students?.Count(s => s.Status == "Active") ?? 0;
            
            if (activeStudents > 0)
            {
                return BadRequest(new 
                { 
                    message = $"Cannot delete course with {activeStudents} active student(s). Please deactivate the course instead.",
                    activeStudents 
                });
            }

            // Soft delete - mark as inactive
            course.IsActive = false;
            course.UpdatedAt = DateTime.Now;
            course.UpdatedBy = GetCurrentUserId();

            await _context.SaveChangesAsync();

            return Ok(new 
            { 
                message = "Course deactivated successfully", 
                courseId = id 
            });
        }

        /// <summary>
        /// Permanently delete course (Admin only)
        /// WARNING: This is irreversible
        /// </summary>
        [HttpDelete("{id}/permanent")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> PermanentlyDeleteCourse(int id)
        {
            var course = await _context.Courses
                .Include(c => c.Students)
                
                .FirstOrDefaultAsync(c => c.CourseId == id);

            if (course == null)
            {
                return NotFound(new { message = "Course not found" });
            }

            // Safety check - prevent deletion if there are any students
            if (course.Students?.Any() == true)
            {
                return BadRequest(new 
                { 
                    message = "Cannot permanently delete course with student records",
                    studentCount = course.Students.Count 
                });
            }

            // Safety check - prevent deletion if there are subjects
            if (course.Subjects?.Any() == true)
            {
                return BadRequest(new 
                { 
                    message = "Cannot permanently delete course with associated subjects",
                    subjectCount = course.Subjects.Count 
                });
            }

            _context.Courses.Remove(course);
            await _context.SaveChangesAsync();

            return Ok(new 
            { 
                message = "Course permanently deleted", 
                courseId = id 
            });
        }

        /// <summary>
        /// Activate/Deactivate course (Admin and Registrar only)
        /// </summary>
        [HttpPatch("{id}/status")]
        [Authorize(Roles = "Admin,Registrar")]
        public async Task<IActionResult> UpdateCourseStatus(int id, [FromBody] bool isActive)
        {
            var course = await _context.Courses.FindAsync(id);

            if (course == null)
            {
                return NotFound(new { message = "Course not found" });
            }

            course.IsActive = isActive;
            course.UpdatedAt = DateTime.Now;
            course.UpdatedBy = GetCurrentUserId();

            await _context.SaveChangesAsync();

            return Ok(new 
            { 
                message = $"Course {(isActive ? "activated" : "deactivated")} successfully",
                courseId = id,
                isActive 
            });
        }

        /// <summary>
        /// Get students enrolled in a course (Admin and Registrar only)
        /// </summary>
        [HttpGet("{id}/students")]
        [Authorize(Roles = "Admin,Registrar,Instructor")]
        public async Task<ActionResult<IEnumerable<object>>> GetCourseStudents(
            int id,
            [FromQuery] int? yearLevel = null,
            [FromQuery] string? status = null)
        {
            var course = await _context.Courses.FindAsync(id);
            
            if (course == null)
            {
                return NotFound(new { message = "Course not found" });
            }

            var query = _context.Students
                .Where(s => s.CourseId == id);

            // Filter by year level if provided
            if (yearLevel.HasValue)
            {
                query = query.Where(s => s.YearLevel == yearLevel.Value);
            }

            // Filter by status if provided
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(s => s.Status == status);
            }

            var students = await query
                .Select(s => new
                {
                    s.Id,
                    s.StudentNumber,
                    s.FirstName,
                    s.LastName,
                    s.Email,
                    s.YearLevel,
                    s.Status,
                    s.EnrollmentStatus
                })
                .OrderBy(s => s.YearLevel)
                .ThenBy(s => s.LastName)
                .ToListAsync();

            return Ok(new
            {
                CourseId = course.CourseId,
                CourseName = course.CourseName,
                TotalStudents = students.Count,
                Students = students
            });
        }

        /// <summary>
        /// Get subjects for a course (public)
        /// </summary>
        [HttpGet("{id}/subjects")]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<object>>> GetCourseSubjects(int id)
        {
            var course = await _context.Courses.FindAsync(id);
            
            if (course == null)
            {
                return NotFound(new { message = "Course not found" });
            }

            var subjects = await _context.Courses
                .Where(s => s.CourseId == id && s.IsActive)
                .OrderBy(s => s.YearLevel)
                
                .ToListAsync();

            return Ok(subjects);
        }

        /// <summary>
        /// Bulk update course status (Admin only)
        /// </summary>
        [HttpPost("bulk-status")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> BulkUpdateStatus([FromBody] BulkStatusUpdateDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var courses = await _context.Courses
                .Where(c => dto.CourseIds.Contains(c.CourseId))
                .ToListAsync();

            if (courses.Count == 0)
            {
                return NotFound(new { message = "No courses found with the provided IDs" });
            }

            var userId = GetCurrentUserId();
            var updated = 0;

            foreach (var course in courses)
            {
                course.IsActive = dto.IsActive;
                course.UpdatedAt = DateTime.Now;
                course.UpdatedBy = userId;
                updated++;
            }

            await _context.SaveChangesAsync();

            return Ok(new 
            { 
                message = $"{updated} course(s) {(dto.IsActive ? "activated" : "deactivated")} successfully",
                updatedCount = updated,
                requestedCount = dto.CourseIds.Count
            });
        }

        /// <summary>
        /// Get current user ID from JWT claims
        /// </summary>
        private string GetCurrentUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? User.FindFirst("sub")?.Value 
                ?? "system";
        }
    }

    #region DTOs (Data Transfer Objects)

    /// <summary>
    /// DTO for creating a new course
    /// </summary>
    public class CourseCreateDto
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(20)]
        public string CourseCode { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(100)]
        public string CourseName { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.StringLength(500)]
        public string? Description { get; set; }

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(50)]
        public string Department { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Range(1, 6)]
        public int YearLevel { get; set; }

        public bool? IsActive { get; set; }
    }

    /// <summary>
    /// DTO for updating an existing course
    /// </summary>
    public class CourseUpdateDto
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(20)]
        public string CourseCode { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(100)]
        public string CourseName { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.StringLength(500)]
        public string? Description { get; set; }

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(50)]
        public string Department { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Range(1, 6)]
        public int YearLevel { get; set; }

        public bool IsActive { get; set; }
    }

    /// <summary>
    /// DTO for bulk status updates
    /// </summary>
    public class BulkStatusUpdateDto
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.MinLength(1)]
        public List<int> CourseIds { get; set; } = new();

        [System.ComponentModel.DataAnnotations.Required]
        public bool IsActive { get; set; }
    }

    #endregion
}
