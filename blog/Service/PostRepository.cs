using blog.Data;
using blog.Models;
using Npgsql;
using System.Text.RegularExpressions;

namespace blog.Service
{
    public class PostRepository
    {
        private readonly ApplicationDbContext _context;

        public PostRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Post>> GetAllPostsAsync()
        {
            await using var connection = _context.GetConnection();
            await connection.OpenAsync();

            const string query = @"SELECT id, slug, title, excerpt, content, date, updatedat, ispublished, author 
                                   FROM posts 
                                   WHERE ispublished = TRUE 
                                   ORDER BY date DESC";

            await using var command = new NpgsqlCommand(query, connection);
            await using var reader = await command.ExecuteReaderAsync();

            var posts = new List<Post>();
            while (await reader.ReadAsync())
            {
                posts.Add(new Post
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    Slug = reader.GetString(reader.GetOrdinal("slug")),
                    Title = reader.GetString(reader.GetOrdinal("title")),
                    Excerpt = reader.GetString(reader.GetOrdinal("excerpt")),
                    Content = reader.GetString(reader.GetOrdinal("content")),
                    Date = reader.GetDateTime(reader.GetOrdinal("date")),
                    UpdatedAt = reader.IsDBNull(reader.GetOrdinal("updatedat")) ? null : reader.GetDateTime(reader.GetOrdinal("updatedat")),
                    IsPublished = reader.GetBoolean(reader.GetOrdinal("ispublished")),
                    Author = reader.IsDBNull(reader.GetOrdinal("author")) ? null : reader.GetString(reader.GetOrdinal("author"))
                });
            }

            return posts;
        }

        public async Task<Post?> GetPostByIdAsync(int id)
        {
            await using var connection = _context.GetConnection();
            await connection.OpenAsync();

            const string query = @"SELECT id, slug, title, excerpt, content, date, updatedat, ispublished, author 
                                   FROM posts WHERE id = @id";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@id", id);

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new Post
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    Slug = reader.GetString(reader.GetOrdinal("slug")),
                    Title = reader.GetString(reader.GetOrdinal("title")),
                    Excerpt = reader.GetString(reader.GetOrdinal("excerpt")),
                    Content = reader.GetString(reader.GetOrdinal("content")),
                    Date = reader.GetDateTime(reader.GetOrdinal("date")),
                    UpdatedAt = reader.IsDBNull(reader.GetOrdinal("updatedat")) ? null : reader.GetDateTime(reader.GetOrdinal("updatedat")),
                    IsPublished = reader.GetBoolean(reader.GetOrdinal("ispublished")),
                    Author = reader.IsDBNull(reader.GetOrdinal("author")) ? null : reader.GetString(reader.GetOrdinal("author"))
                };
            }

            return null;
        }

        public async Task<Post?> GetPostBySlugAsync(string slug)
        {
            await using var connection = _context.GetConnection();
            await connection.OpenAsync();

            const string query = @"SELECT id, slug, title, excerpt, content, date, updatedat, ispublished, author 
                                   FROM posts WHERE slug = @slug";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@slug", slug);

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new Post
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    Slug = reader.GetString(reader.GetOrdinal("slug")),
                    Title = reader.GetString(reader.GetOrdinal("title")),
                    Excerpt = reader.GetString(reader.GetOrdinal("excerpt")),
                    Content = reader.GetString(reader.GetOrdinal("content")),
                    Date = reader.GetDateTime(reader.GetOrdinal("date")),
                    UpdatedAt = reader.IsDBNull(reader.GetOrdinal("updatedat")) ? null : reader.GetDateTime(reader.GetOrdinal("updatedat")),
                    IsPublished = reader.GetBoolean(reader.GetOrdinal("ispublished")),
                    Author = reader.IsDBNull(reader.GetOrdinal("author")) ? null : reader.GetString(reader.GetOrdinal("author"))
                };
            }

            return null;
        }

        public async Task<Post> CreatePostAsync(CreatePostRequest request)
        {
            var slug = GenerateSlug(request.Title);
            var date = request.Date ?? DateTime.UtcNow;

            await using var connection = _context.GetConnection();
            await connection.OpenAsync();

            const string query = @"INSERT INTO posts (slug, title, excerpt, content, date, author, createdat, updatedat, ispublished)
                                   VALUES (@slug, @title, @excerpt, @content, @date, @author, NOW(), NOW(), TRUE)
                                   RETURNING id;";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@slug", slug);
            command.Parameters.AddWithValue("@title", request.Title);
            command.Parameters.AddWithValue("@excerpt", (object?)request.Excerpt ?? "");
            command.Parameters.AddWithValue("@content", (object?)request.Content ?? "");
            command.Parameters.AddWithValue("@date", date);
            command.Parameters.AddWithValue("@author", (object?)request.Author ?? DBNull.Value);

            var id = Convert.ToInt32(await command.ExecuteScalarAsync());

            return new Post
            {
                Id = id,
                Slug = slug,
                Title = request.Title,
                Excerpt = request.Excerpt,
                Content = request.Content,
                Date = date,
                Author = request.Author,
                IsPublished = true
            };
        }

        public async Task<bool> UpdatePostAsync(int id, UpdatePostRequest request)
        {
            await using var connection = _context.GetConnection();
            await connection.OpenAsync();

            var updates = new List<string>();
            await using var command = new NpgsqlCommand();
            command.Connection = connection;

            if (request.Title != null)
            {
                updates.Add("title = @title");
                command.Parameters.AddWithValue("@title", request.Title);
                updates.Add("slug = @slug");
                command.Parameters.AddWithValue("@slug", GenerateSlug(request.Title));
            }

            if (request.Excerpt != null)
            {
                updates.Add("excerpt = @excerpt");
                command.Parameters.AddWithValue("@excerpt", request.Excerpt);
            }

            if (request.Content != null)
            {
                updates.Add("content = @content");
                command.Parameters.AddWithValue("@content", request.Content);
            }

            if (request.Date.HasValue)
            {
                updates.Add("date = @date");
                command.Parameters.AddWithValue("@date", request.Date.Value);
            }

            if (request.IsPublished.HasValue)
            {
                updates.Add("ispublished = @ispublished");
                command.Parameters.AddWithValue("@ispublished", request.IsPublished.Value);
            }

            if (request.Author != null)
            {
                updates.Add("author = @author");
                command.Parameters.AddWithValue("@author", request.Author);
            }

            if (updates.Count == 0)
                return false;

            updates.Add("updatedat = NOW()");
            var query = $"UPDATE posts SET {string.Join(", ", updates)} WHERE id = @id";
            command.Parameters.AddWithValue("@id", id);
            command.CommandText = query;

            var rows = await command.ExecuteNonQueryAsync();
            return rows > 0;
        }

        public async Task<bool> DeletePostAsync(int id)
        {
            await using var connection = _context.GetConnection();
            await connection.OpenAsync();

            const string query = "DELETE FROM posts WHERE id = @id";
            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@id", id);

            var rows = await command.ExecuteNonQueryAsync();
            return rows > 0;
        }

        public async Task<bool> TestConnectionAsync()
        {
            await using var connection = _context.GetConnection();
            await connection.OpenAsync();
            return connection.FullState == System.Data.ConnectionState.Open;
        }

        private static string GenerateSlug(string title)
        {
            var slug = title.ToLowerInvariant();
            slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = Regex.Replace(slug, @"\s+", "-");
            slug = Regex.Replace(slug, @"-+", "-");
            return slug.Trim('-');
        }
    }
}
