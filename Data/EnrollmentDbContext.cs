using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MalatePitogoEnrollmentSystem.Models;

namespace MalatePitogoEnrollmentSystem.Data
{
    public class EnrollmentDbContext : IdentityDbContext<IdentityUser>
    {
        public EnrollmentDbContext(DbContextOptions<EnrollmentDbContext> options)
            : base(options)
        {
        }

        public DbSet<Student> Students { get; set; } = null!;
        public DbSet<Course> Courses { get; set; } = null!;
        public DbSet<MalatePitogoEnrollmentSystem.Models.Enrollment> Enrollments { get; set; } = null!;
        public DbSet<Instructor> Instructors { get; set; } = null!;
    }
}