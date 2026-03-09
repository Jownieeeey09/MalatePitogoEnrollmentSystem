using Microsoft.AspNetCore.Identity;
using MalatePitogoEnrollmentSystem.Data;
using MalatePitogoEnrollmentSystem.Models;

namespace MalatePitogoEnrollmentSystem.Data
{
    /// <summary>
    /// Seeds default roles and test users into the database.
    /// Run with: dotnet run --seed
    /// </summary>
    public static class DataSeeder
    {
        public static async Task SeedAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var context     = scope.ServiceProvider.GetRequiredService<EnrollmentDbContext>();
            var logger      = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            logger.LogInformation("=== DataSeeder: Starting ===");

            // ── 1. ROLES ─────────────────────────────────────────────────────────
            var roles = new[] { "Admin", "Registrar", "Student", "Cashier", "Instructor" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                    logger.LogInformation("  [ROLE]  Created: {Role}", role);
                }
                else
                {
                    logger.LogInformation("  [ROLE]  Already exists: {Role}", role);
                }
            }

            // ── 2. SEED USERS ─────────────────────────────────────────────────────
            var users = new[]
            {
                new SeedUser("admin@dbtc.edu.ph",      "Admin@123!",      "Admin",      null,       null,       null,      Confirmed: true),
                new SeedUser("registrar@dbtc.edu.ph",  "Registrar@123!",  "Registrar",  null,       null,       null,      Confirmed: true),
                new SeedUser("cashier@dbtc.edu.ph",    "Cashier@123!",    "Cashier",    null,       null,       null,      Confirmed: true),
                new SeedUser("instructor1@dbtc.edu.ph","Instructor@123!", "Instructor", null,       null,       null,      Confirmed: true),
                new SeedUser("student1@dbtc.edu.ph",   "Student@123!",    "Student",    "Juan",     "Dela Cruz",null,      Confirmed: true),
                new SeedUser("student2@dbtc.edu.ph",   "Student@123!",    "Student",    "Maria",    "Santos",   null,      Confirmed: true),
                new SeedUser("student3@dbtc.edu.ph",   "Student@123!",    "Student",    "Jose",     "Reyes",    null,      Confirmed: false),
            };

            foreach (var su in users)
            {
                var existing = await userManager.FindByEmailAsync(su.Email);
                if (existing != null)
                {
                    logger.LogInformation("  [USER]  Already exists: {Email}", su.Email);
                    continue;
                }

                var user = new IdentityUser
                {
                    UserName       = su.Email,
                    Email          = su.Email,
                    EmailConfirmed = su.Confirmed
                };

                var result = await userManager.CreateAsync(user, su.Password);
                if (!result.Succeeded)
                {
                    var errs = string.Join(", ", result.Errors.Select(e => e.Description));
                    logger.LogError("  [USER]  Failed to create {Email}: {Errors}", su.Email, errs);
                    continue;
                }

                await userManager.AddToRoleAsync(user, su.Role);
                logger.LogInformation("  [USER]  Created: {Email} [{Role}]", su.Email, su.Role);

                // For students, also create a Student record
                if (su.Role == "Student" && su.FirstName != null)
                {
                    var studentNumber = $"{DateTime.UtcNow.Year}-{new Random().Next(1000, 9999):D4}";
                    context.Students.Add(new Student
                    {
                        UserId           = user.Id,
                        StudentNumber    = studentNumber,
                        FirstName        = su.FirstName,
                        LastName         = su.LastName ?? string.Empty,
                        Email            = su.Email,
                        Status           = su.Confirmed ? "Active" : "Pending",
                        EnrollmentStatus = su.Confirmed ? "Regular" : "New",
                        CreatedAt        = DateTime.UtcNow
                    });
                    logger.LogInformation("  [STUDENT] Created profile: {First} {Last} ({Number})",
                        su.FirstName, su.LastName, studentNumber);
                }
            }

            await context.SaveChangesAsync();
            logger.LogInformation("=== DataSeeder: Complete ===");
            logger.LogInformation("");
            logger.LogInformation("Test Credentials:");
            logger.LogInformation("  Admin      : admin@dbtc.edu.ph      / Admin@123!");
            logger.LogInformation("  Registrar  : registrar@dbtc.edu.ph  / Registrar@123!");
            logger.LogInformation("  Cashier    : cashier@dbtc.edu.ph    / Cashier@123!");
            logger.LogInformation("  Instructor : instructor1@dbtc.edu.ph/ Instructor@123!");
            logger.LogInformation("  Student 1  : student1@dbtc.edu.ph   / Student@123!");
            logger.LogInformation("  Student 2  : student2@dbtc.edu.ph   / Student@123!");
            logger.LogInformation("  Student 3  : student3@dbtc.edu.ph   / Student@123! (Pending/unconfirmed)");
        }

        private record SeedUser(
            string Email,
            string Password,
            string Role,
            string? FirstName,
            string? LastName,
            string? StudentNumber,
            bool Confirmed);
    }
}
