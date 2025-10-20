using Microsoft.AspNetCore.Http;
using blog.Services;
using System.Threading.Tasks;

namespace blog.Middlewares
{
    public class RabbitMqMiddleware
    {
        private readonly RequestDelegate _next;

        public RabbitMqMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, RabbitMqService rabbit)
        {
            // Make RabbitMQ service available to controllers
            context.Items["RabbitMqService"] = rabbit;

            // Capture original response body (so we can inspect it later)
            var originalBody = context.Response.Body;

            try
            {
                await _next(context); // Call next middleware (controllers)

                // ✅ After controller executes:
                if (context.Request.Method is "POST" or "PUT" or "DELETE")
                {
                    var route = context.Request.Path.ToString();
                    var method = context.Request.Method;

                    // Construct message for RabbitMQ
                    var message = $"[{method}] request to {route} completed successfully.";

                    rabbit.Publish(message);
                    Console.WriteLine($"📨 RabbitMQ middleware published: {message}");
                }
            }
            catch (Exception ex)
            {
                // Log and rethrow so API still returns proper error
                Console.WriteLine($"❌ Middleware error: {ex.Message}");
                throw;
            }
            finally
            {
                context.Response.Body = originalBody;
            }
        }
    }
}
