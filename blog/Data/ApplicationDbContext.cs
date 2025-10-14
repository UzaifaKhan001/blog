using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Threading.Tasks;

namespace blog.Data
{
    public class ApplicationDbContext
    {
        private readonly string _connectionString;

        public ApplicationDbContext(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public NpgsqlConnection GetConnection()
        {
            return new NpgsqlConnection(_connectionString);
        }
    }
}
