namespace blog.Models
{
    // Models/Post.cs
    public class Post
    {
        public int Id { get; set; }
        public string Slug { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Excerpt { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public bool IsPublished { get; set; } = true;
        public string? Author { get; set; }
    }

    // Models/CreatePostRequest.cs
    public class CreatePostRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Excerpt { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime? Date { get; set; }
        public string? Author { get; set; }
    }

    // Models/UpdatePostRequest.cs
    public class UpdatePostRequest
    {
        public string? Title { get; set; }
        public string? Excerpt { get; set; }
        public string? Content { get; set; }
        public DateTime? Date { get; set; }
        public bool? IsPublished { get; set; }
        public string? Author { get; set; }
    }
}
