using System;

namespace blog.Models
{
    public class UpdateProfileRequest
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string CurrentPassword { get; set; }
        public string NewPassword { get; set; }
        public string ConfirmPassword { get; set; }
    }

    public class GetProfileResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public ProfileData Data { get; set; }
    }

    public class UpdateProfileResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public ProfileData Data { get; set; }
    }

    public class ProfileData
    {
        public int UserId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? LastLogin { get; set; }
    }
}