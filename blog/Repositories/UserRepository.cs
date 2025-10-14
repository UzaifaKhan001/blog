using blog.Data;
using blog.Helpers;
using blog.Models;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;
using System;
using System.Threading.Tasks;

namespace blog.Repositories
{
    public class UserRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;

        public UserRepository(ApplicationDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public User GetUserByEmail(string email)
        {
            using var connection = _context.GetConnection();
            connection.Open();

            const string query = @"SELECT user_id, email, password_hash, full_name, 
                     created_at, updated_at, is_active, last_login 
                     FROM users WHERE LOWER(email) = LOWER(@Email) AND is_active = TRUE";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@Email", email.Trim());

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return new User
                {
                    UserId = reader.GetInt32(0),
                    Email = reader.GetString(1),
                    PasswordHash = reader.GetString(2),
                    FullName = reader.IsDBNull(3) ? null : reader.GetString(3),
                    CreatedAt = reader.GetDateTime(4),
                    UpdatedAt = reader.GetDateTime(5),
                    IsActive = reader.GetBoolean(6),
                    LastLogin = reader.IsDBNull(7) ? null : (DateTime?)reader.GetDateTime(7)
                };
            }
            return null;
        }

        public bool CreateUser(RegisterRequest request)
        {
            using var connection = _context.GetConnection();
            connection.Open();

            const string query = @"INSERT INTO users (email, password_hash, full_name) 
                                   VALUES (@Email, @PasswordHash, @FullName)";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@Email", request.Email);
            command.Parameters.AddWithValue("@PasswordHash", PasswordHelper.HashPassword(request.Password));
            command.Parameters.AddWithValue("@FullName", (object?)request.FullName ?? DBNull.Value);

            return command.ExecuteNonQuery() > 0;
        }

        public async Task<bool> UpdateLastLogin(int userId)
        {
            await using var connection = _context.GetConnection();
            await connection.OpenAsync();

            const string query = "UPDATE users SET last_login = @LastLogin WHERE user_id = @UserId";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@LastLogin", DateTime.UtcNow);
            command.Parameters.AddWithValue("@UserId", userId);

            return await command.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> CreateSession(int userId, string token, DateTime expiresAt)
        {
            await using var connection = _context.GetConnection();
            await connection.OpenAsync();

            const string query = @"INSERT INTO user_sessions (user_id, token, expires_at) 
                                   VALUES (@UserId, @Token, @ExpiresAt)";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@Token", token);
            command.Parameters.AddWithValue("@ExpiresAt", expiresAt);

            return await command.ExecuteNonQueryAsync() > 0;
        }

        public bool CreatePasswordResetToken(int userId, string token)
        {
            using var connection = _context.GetConnection();
            connection.Open();

            const string query = @"INSERT INTO password_reset_tokens (user_id, token, expires_at) 
                                   VALUES (@UserId, @Token, @ExpiresAt)";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@Token", token);
            command.Parameters.AddWithValue("@ExpiresAt", DateTime.UtcNow.AddHours(1));

            return command.ExecuteNonQuery() > 0;
        }

        public bool ResetPassword(string email, string token, string newPassword)
        {
            using var connection = _context.GetConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                const string verifyQuery = @"SELECT 1 FROM password_reset_tokens prt
                                             JOIN users u ON prt.user_id = u.user_id
                                             WHERE u.email = @Email AND prt.token = @Token 
                                                   AND prt.expires_at > @CurrentTime";

                using var verifyCommand = new NpgsqlCommand(verifyQuery, connection, transaction);
                verifyCommand.Parameters.AddWithValue("@Email", email);
                verifyCommand.Parameters.AddWithValue("@Token", token);
                verifyCommand.Parameters.AddWithValue("@CurrentTime", DateTime.UtcNow);

                if (verifyCommand.ExecuteScalar() == null)
                    return false;

                const string updateQuery = @"UPDATE users 
                                             SET password_hash = @PasswordHash, updated_at = @UpdatedAt
                                             WHERE email = @Email";

                using var updateCommand = new NpgsqlCommand(updateQuery, connection, transaction);
                updateCommand.Parameters.AddWithValue("@PasswordHash", PasswordHelper.HashPassword(newPassword));
                updateCommand.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow);
                updateCommand.Parameters.AddWithValue("@Email", email);

                if (updateCommand.ExecuteNonQuery() == 0)
                    return false;

                const string deleteQuery = "DELETE FROM password_reset_tokens WHERE token = @Token";
                using var deleteCommand = new NpgsqlCommand(deleteQuery, connection, transaction);
                deleteCommand.Parameters.AddWithValue("@Token", token);
                deleteCommand.ExecuteNonQuery();

                transaction.Commit();
                return true;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public bool VerifyResetToken(string email, string token)
        {
            using var connection = _context.GetConnection();
            connection.Open();

            const string query = @"SELECT 1 FROM password_reset_tokens prt
                                   JOIN users u ON prt.user_id = u.user_id
                                   WHERE u.email = @Email AND prt.token = @Token 
                                         AND prt.expires_at > @CurrentTime";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@Email", email);
            command.Parameters.AddWithValue("@Token", token);
            command.Parameters.AddWithValue("@CurrentTime", DateTime.UtcNow);

            return command.ExecuteScalar() != null;
        }
    }
}
