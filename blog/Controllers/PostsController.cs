using blog.Data;
using blog.Models;
using blog.Service;
using blog.Services;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace blog.Controllers
{
    // Controllers/PostsController.cs
    [ApiController]
    [Route("api/[controller]")]
    public class PostsController : ControllerBase
    {
        private readonly PostRepository _postRepository;
        private readonly IConfiguration _config;
        private readonly AuthService _authService;
        private readonly EmailService _emailService;

        public PostsController(PostRepository postRepository, AuthService authService, EmailService emailService, IConfiguration config)
        {
            _postRepository = postRepository;
            _authService = authService;
            _emailService = emailService;
            _config = config;
        }
        [HttpGet("test-rabbit")]
        public IActionResult TestRabbit()
        {
            if (HttpContext.Items["RabbitMqService"] is RabbitMqService rabbit)
            {
                rabbit.Publish("Hello from Middleware Rabbi httpget test rabbit work i want tMQ!");
                return Ok(new { message = "Message sent successfully" });
            }

            return StatusCode(500, new { message = "RabbitMQ service not available" });
        }


        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] Models.LoginRequest request)
        {
            var response = await _authService.LoginAsync(request);
            return response.Success ? Ok(response) : BadRequest(response);
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] Models.RegisterRequest request)
        {
            var response = await _authService.RegisterAsync(request);
            return response.Success ? Ok(response) : BadRequest(response);
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] Models.ForgotPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new { message = "Email is required" });

            var result = await _authService.ForgotPasswordAsync(request);
            return result.Success ? Ok(new { message = "If the email exists, a password reset link has been sent" })
                                : BadRequest(new { message = result.Message });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] Models.ResetPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.NewPassword))
                return BadRequest(new { message = "Email, token and new password are required" });

            if (request.NewPassword != request.ConfirmPassword)
                return BadRequest(new { message = "Passwords do not match" });

            var result = await _authService.ResetPasswordAsync(request);
            return result.Success ? Ok(new { message = "Password reset successfully" })
                                : BadRequest(new { message = result.Message });
        }

        [HttpPost("verify-reset-token")]
        public async Task<IActionResult> VerifyResetToken([FromBody] VerifyResetTokenRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Token))
                return BadRequest(new { message = "Email and token are required" });

            var isValid = await _authService.VerifyResetTokenAsync(request.Email, request.Token);
            return isValid ? Ok(new { message = "Token is valid" })
                          : BadRequest(new { message = "Invalid or expired token" });
        }

        [HttpPost("subscribe")]
        public async Task<IActionResult> Subscribe([FromBody] SubscriptionRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new { message = "Email is required." });

            try
            {
                string connectionString = _config.GetConnectionString("DefaultConnection");

                using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync();

                string query = "INSERT INTO subscribers (email) VALUES (@email)";
                using var cmd = new NpgsqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@email", request.Email.Trim());

                try
                {
                    await cmd.ExecuteNonQueryAsync();
                    return Ok(new { message = "Subscription successful!" });
                }
                catch (PostgresException ex) when (ex.SqlState == "23505") // unique_violation
                {
                    return Conflict(new { message = "This email is already subscribed." });
                }
            }
            catch (PostgresException ex)
            {
                return StatusCode(500, new { message = $"Database error: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Unexpected error: {ex.Message}" });
            }
        }


        [HttpGet]
        public async Task<ActionResult<List<Post>>> GetPosts()
        {
            try
            {
                var posts = await _postRepository.GetAllPostsAsync();
                return Ok(posts);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving posts", error = ex.Message });
            }
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Post>> GetPostById(int id)
        {
            try
            {
                var post = await _postRepository.GetPostByIdAsync(id);
                if (post == null)
                {
                    return NotFound(new { message = "Post not found" });
                }
                return Ok(post);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving the post", error = ex.Message });
            }
        }

        [HttpGet("slug/{slug}")]
        public async Task<ActionResult<Post>> GetPostBySlug(string slug)
        {
            try
            {
                var post = await _postRepository.GetPostBySlugAsync(slug);
                if (post == null)
                {
                    return NotFound(new { message = "Post not found" });
                }
                return Ok(post);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving the post", error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<ActionResult<Post>> CreatePost(CreatePostRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Title))
                {
                    return BadRequest(new { message = "Title is required" });
                }

                var post = await _postRepository.CreatePostAsync(request);
                return CreatedAtAction(nameof(GetPostById), new { id = post.Id }, post);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while creating the post", error = ex.Message });
            }
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult> UpdatePost(int id, UpdatePostRequest request)
        {
            try
            {
                var existingPost = await _postRepository.GetPostByIdAsync(id);
                if (existingPost == null)
                {
                    return NotFound(new { message = "Post not found" });
                }

                var success = await _postRepository.UpdatePostAsync(id, request);
                if (!success)
                {
                    return BadRequest(new { message = "No fields to update" });
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while updating the post", error = ex.Message });
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult> DeletePost(int id)
        {
            try
            {
                var existingPost = await _postRepository.GetPostByIdAsync(id);
                if (existingPost == null)
                {
                    return NotFound(new { message = "Post not found" });
                }

                var success = await _postRepository.DeletePostAsync(id);
                if (!success)
                {
                    return StatusCode(500, new { message = "Failed to delete post" });
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while deleting the post", error = ex.Message });
            }
        }
    }
}
