using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MalatePitogoEnrollmentSystem.Controllers
{
    [ApiController]
    [Route("api/test")]
    public class TestController : ControllerBase
    {
        // ============================================================
        // PUBLIC ENDPOINTS - No authentication required
        // ============================================================

        /// <summary>
        /// Public endpoint - anyone can access
        /// </summary>
        [HttpGet("public")]
        [AllowAnonymous]
        public IActionResult Public()
        {
            return Ok(new
            {
                message = "This is a public endpoint. No authentication required.",
                timestamp = DateTime.UtcNow,
                authenticated = User.Identity?.IsAuthenticated ?? false
            });
        }

        /// <summary>
        /// Health check endpoint - public
        /// </summary>
        [HttpGet("health")]
        [AllowAnonymous]
        public IActionResult Health()
        {
            return Ok(new
            {
                status = "healthy",
                service = "EnrollmentSystem API",
                timestamp = DateTime.UtcNow
            });
        }

        // ============================================================
        // AUTHENTICATED ENDPOINTS - Any authenticated user
        // ============================================================

        /// <summary>
        /// Basic authentication test - any authenticated user
        /// </summary>
        [HttpGet("secure")]
        [Authorize]
        public IActionResult Secure()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            return Ok(new
            {
                message = "You are authenticated!",
                userId,
                email,
                role,
                authenticated = true
            });
        }

        /// <summary>
        /// Get current user's claims
        /// </summary>
        [HttpGet("whoami")]
        [Authorize]
        public IActionResult WhoAmI()
        {
            var claims = User.Claims.Select(c => new
            {
                type = c.Type,
                value = c.Value
            }).ToList();

            return Ok(new
            {
                authenticated = User.Identity?.IsAuthenticated ?? false,
                name = User.Identity?.Name,
                authenticationType = User.Identity?.AuthenticationType,
                claims
            });
        }

        // ============================================================
        // ROLE-BASED ENDPOINTS - Test each role
        // ============================================================

        /// <summary>
        /// Student-only endpoint
        /// </summary>
        [HttpGet("student-only")]
        [Authorize(Roles = "Student")]
        public IActionResult StudentOnly()
        {
            var studentId = User.FindFirst("student_id")?.Value;

            return Ok(new
            {
                message = "Welcome, Student!",
                role = "Student",
                studentId,
                accessLevel = "Student",
                canDo = new[]
                {
                    "View own profile",
                    "View own enrollments",
                    "Update contact information"
                }
            });
        }

        /// <summary>
        /// Faculty-only endpoint
        /// </summary>
        [HttpGet("faculty-only")]
        [Authorize(Roles = "Faculty")]
        public IActionResult FacultyOnly()
        {
            var instructorId = User.FindFirst("instructor_id")?.Value;

            return Ok(new
            {
                message = "Welcome, Faculty!",
                role = "Faculty",
                instructorId,
                accessLevel = "Faculty",
                canDo = new[]
                {
                    "View own profile",
                    "View own courses",
                    "View students in your courses"
                }
            });
        }

        /// <summary>
        /// Registrar-only endpoint
        /// </summary>
        [HttpGet("registrar-only")]
        [Authorize(Roles = "Registrar")]
        public IActionResult RegistrarOnly()
        {
            return Ok(new
            {
                message = "Welcome, Registrar!",
                role = "Registrar",
                accessLevel = "Registrar",
                canDo = new[]
                {
                    "View all students",
                    "Approve/reject enrollments",
                    "Assign instructors to courses",
                    "Manage student records"
                }
            });
        }

        /// <summary>
        /// Finance-only endpoint
        /// </summary>
        [HttpGet("finance-only")]
        [Authorize(Roles = "Finance")]
        public IActionResult FinanceOnly()
        {
            return Ok(new
            {
                message = "Welcome, Finance Staff!",
                role = "Finance",
                accessLevel = "Finance",
                canDo = new[]
                {
                    "View payment records",
                    "Process payments",
                    "Generate financial reports"
                }
            });
        }

        /// <summary>
        /// Admin-only endpoint
        /// </summary>
        [HttpGet("admin-only")]
        [Authorize(Roles = "Admin")]
        public IActionResult AdminOnly()
        {
            return Ok(new
            {
                message = "Welcome, Administrator!",
                role = "Admin",
                accessLevel = "Admin",
                canDo = new[]
                {
                    "Full system access",
                    "Manage all users",
                    "Configure system settings",
                    "View all data"
                }
            });
        }

        // ============================================================
        // MULTIPLE ROLES ENDPOINTS
        // ============================================================

        /// <summary>
        /// Admin OR Registrar endpoint
        /// </summary>
        [HttpGet("admin-or-registrar")]
        [Authorize(Roles = "Admin,Registrar")]
        public IActionResult AdminOrRegistrar()
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            return Ok(new
            {
                message = "Access granted for Admin or Registrar",
                yourRole = role,
                accessLevel = "Admin or Registrar",
                canDo = new[]
                {
                    "Manage students",
                    "View all enrollments",
                    "Generate reports"
                }
            });
        }

        /// <summary>
        /// Faculty OR Admin endpoint
        /// </summary>
        [HttpGet("faculty-or-admin")]
        [Authorize(Roles = "Faculty,Admin")]
        public IActionResult FacultyOrAdmin()
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            return Ok(new
            {
                message = "Access granted for Faculty or Admin",
                yourRole = role,
                accessLevel = "Faculty or Admin"
            });
        }

        /// <summary>
        /// Staff-only endpoint (Registrar, Faculty, Finance, Admin)
        /// </summary>
        [HttpGet("staff-only")]
        [Authorize(Roles = "Registrar,Faculty,Finance,Admin")]
        public IActionResult StaffOnly()
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            return Ok(new
            {
                message = "Welcome, Staff Member!",
                yourRole = role,
                accessLevel = "Staff",
                note = "This endpoint is for all staff members (not students)"
            });
        }

        // ============================================================
        // PERMISSION-BASED ENDPOINTS
        // ============================================================

        /// <summary>
        /// Test custom policy-based authorization
        /// </summary>
        [HttpGet("can-approve-enrollments")]
        [Authorize(Policy = "CanApproveEnrollment")]
        public IActionResult CanApproveEnrollments()
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            return Ok(new
            {
                message = "You have permission to approve enrollments",
                yourRole = role,
                permission = "enrollment:approve"
            });
        }

        /// <summary>
        /// Test admin-only policy
        /// </summary>
        [HttpGet("admin-policy")]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult AdminPolicy()
        {
            return Ok(new
            {
                message = "Admin policy check passed",
                policy = "AdminOnly"
            });
        }

        // ============================================================
        // AUTHORIZATION TESTING ENDPOINTS
        // ============================================================

        /// <summary>
        /// Test 401 Unauthorized response
        /// </summary>
        [HttpGet("test-unauthorized")]
        [Authorize]
        public IActionResult TestUnauthorized()
        {
            // This will return 401 if no valid token is provided
            return Ok(new { message = "If you see this, you are authenticated" });
        }

        /// <summary>
        /// Test 403 Forbidden response (Admin only)
        /// </summary>
        [HttpGet("test-forbidden")]
        [Authorize(Roles = "Admin")]
        public IActionResult TestForbidden()
        {
            // This will return 403 if user is authenticated but not an Admin
            return Ok(new { message = "If you see this, you are an Admin" });
        }

        /// <summary>
        /// Test authorization with resource-based logic
        /// </summary>
        [HttpGet("test-resource/{resourceId}")]
        [Authorize]
        public IActionResult TestResourceBased(int resourceId)
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var studentId = User.FindFirst("student_id")?.Value;

            // Simulate resource ownership check
            bool isOwner = studentId != null && int.Parse(studentId) == resourceId;
            bool isAdmin = role == "Admin" || role == "Registrar";

            if (!isOwner && !isAdmin)
            {
                return Forbid(); // 403 Forbidden
            }

            return Ok(new
            {
                message = "Access granted to resource",
                resourceId,
                accessReason = isAdmin ? "Admin/Registrar access" : "Resource owner",
                yourRole = role
            });
        }

        // ============================================================
        // TOKEN TESTING ENDPOINTS
        // ============================================================

        /// <summary>
        /// Validate JWT token structure
        /// </summary>
        [HttpGet("validate-token")]
        [Authorize]
        public IActionResult ValidateToken()
        {
            var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

            var claims = User.Claims.ToDictionary(
                c => c.Type.Split('/').Last(), // Get claim name without namespace
                c => c.Value
            );

            return Ok(new
            {
                message = "Token is valid",
                tokenLength = token.Length,
                claims,
                expiresAt = User.FindFirst("exp")?.Value,
                issuedAt = User.FindFirst("iat")?.Value
            });
        }

        /// <summary>
        /// Test token expiration behavior
        /// </summary>
        [HttpGet("token-info")]
        [Authorize]
        public IActionResult TokenInfo()
        {
            var exp = User.FindFirst("exp")?.Value;
            var iat = User.FindFirst("iat")?.Value;

            DateTime? expirationDate = null;
            DateTime? issuedDate = null;

            if (long.TryParse(exp, out var expSeconds))
            {
                expirationDate = DateTimeOffset.FromUnixTimeSeconds(expSeconds).UtcDateTime;
            }

            if (long.TryParse(iat, out var iatSeconds))
            {
                issuedDate = DateTimeOffset.FromUnixTimeSeconds(iatSeconds).UtcDateTime;
            }

            return Ok(new
            {
                issuedAt = issuedDate,
                expiresAt = expirationDate,
                isExpired = expirationDate.HasValue && expirationDate.Value < DateTime.UtcNow,
                timeUntilExpiration = expirationDate.HasValue
                    ? (expirationDate.Value - DateTime.UtcNow).ToString()
                    : null
            });
        }

        // ============================================================
        // COMPREHENSIVE ROLE TEST
        // ============================================================

        /// <summary>
        /// Get comprehensive authorization test results
        /// </summary>
        [HttpGet("auth-test-suite")]
        [Authorize]
        public IActionResult AuthTestSuite()
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var studentId = User.FindFirst("student_id")?.Value;
            var instructorId = User.FindFirst("instructor_id")?.Value;

            var isStudent = User.IsInRole("Student");
            var isFaculty = User.IsInRole("Faculty");
            var isRegistrar = User.IsInRole("Registrar");
            var isFinance = User.IsInRole("Finance");
            var isAdmin = User.IsInRole("Admin");

            return Ok(new
            {
                authentication = new
                {
                    isAuthenticated = User.Identity?.IsAuthenticated ?? false,
                    authenticationType = User.Identity?.AuthenticationType,
                    name = User.Identity?.Name
                },
                user = new
                {
                    userId,
                    email,
                    role,
                    studentId,
                    instructorId
                },
                roles = new
                {
                    isStudent,
                    isFaculty,
                    isRegistrar,
                    isFinance,
                    isAdmin
                },
                permissions = new
                {
                    canViewOwnProfile = true,
                    canViewAllStudents = isAdmin || isRegistrar,
                    canViewAllEnrollments = isAdmin || isRegistrar,
                    canApproveEnrollments = isAdmin || isRegistrar,
                    canManageInstructors = isAdmin,
                    canDeleteRecords = isAdmin,
                    canViewStudentsInCourses = isFaculty || isAdmin,
                    canManagePayments = isFinance || isAdmin
                },
                endpoints = new
                {
                    canAccess = new
                    {
                        publicEndpoints = true,
                        studentEndpoints = isStudent,
                        facultyEndpoints = isFaculty,
                        registrarEndpoints = isRegistrar,
                        financeEndpoints = isFinance,
                        adminEndpoints = isAdmin
                    }
                },
                allClaims = User.Claims.Select(c => new
                {
                    type = c.Type.Split('/').Last(),
                    value = c.Value
                }).ToList()
            });
        }

        // ============================================================
        // ERROR SIMULATION ENDPOINTS
        // ============================================================

        /// <summary>
        /// Simulate 401 Unauthorized
        /// </summary>
        [HttpGet("simulate-401")]
        public IActionResult Simulate401()
        {
            return Unauthorized(new { error = "Unauthorized", message = "Authentication required" });
        }

        /// <summary>
        /// Simulate 403 Forbidden
        /// </summary>
        [HttpGet("simulate-403")]
        [Authorize]
        public IActionResult Simulate403()
        {
            return Forbid(); // Returns 403
        }

        /// <summary>
        /// Test different HTTP status codes
        /// </summary>
        [HttpGet("status/{code}")]
        [AllowAnonymous]
        public IActionResult TestStatus(int code)
        {
            return code switch
            {
                200 => Ok(new { status = 200, message = "OK" }),
                201 => Created("", new { status = 201, message = "Created" }),
                204 => NoContent(),
                400 => BadRequest(new { status = 400, message = "Bad Request" }),
                401 => Unauthorized(new { status = 401, message = "Unauthorized" }),
                403 => Forbid(),
                404 => NotFound(new { status = 404, message = "Not Found" }),
                500 => StatusCode(500, new { status = 500, message = "Internal Server Error" }),
                _ => BadRequest(new { message = "Unsupported status code" })
            };
        }

        // ============================================================
        // DEBUGGING ENDPOINTS
        // ============================================================

        /// <summary>
        /// Debug endpoint - shows all request headers
        /// </summary>
        [HttpGet("debug/headers")]
        [AllowAnonymous]
        public IActionResult DebugHeaders()
        {
            var headers = Request.Headers
                .ToDictionary(h => h.Key, h => h.Value.ToString());

            var authHeader = Request.Headers["Authorization"].ToString();
            var hasBearer = authHeader.StartsWith("Bearer ");

            return Ok(new
            {
                headers,
                authorization = new
                {
                    present = !string.IsNullOrEmpty(authHeader),
                    hasBearer,
                    tokenLength = hasBearer ? authHeader.Replace("Bearer ", "").Length : 0
                }
            });
        }

        /// <summary>
        /// Debug endpoint - shows request information
        /// </summary>
        [HttpGet("debug/request")]
        [AllowAnonymous]
        public IActionResult DebugRequest()
        {
            return Ok(new
            {
                method = Request.Method,
                path = Request.Path.Value,
                query = Request.QueryString.Value,
                scheme = Request.Scheme,
                host = Request.Host.Value,
                isHttps = Request.IsHttps,
                protocol = Request.Protocol,
                contentType = Request.ContentType,
                hasFormContentType = Request.HasFormContentType
            });
        }
    }
}