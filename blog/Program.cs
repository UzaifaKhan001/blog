// Program.cs
using blog.Data;
using blog.Helpers;
using blog.Repositories;
using blog.Service;
using blog.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using blog.Middlewares;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// JWT Authentication setup
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"];

if (!string.IsNullOrEmpty(secretKey))
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings["Issuer"],
                ValidAudience = jwtSettings["Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                ClockSkew = TimeSpan.Zero
            };
        });
}
else
{
    Console.WriteLine("⚠️ WARNING: JWT SecretKey is not configured — Authentication will be disabled.");
}

// Dependency Injection
// Dependency Injection
builder.Services.AddScoped<ApplicationDbContext>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<PostRepository>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<JwtTokenHelper>();
builder.Services.AddSingleton<EmailService>();
builder.Services.AddSingleton<RabbitMqService>();



// Add built-in caching
builder.Services.AddMemoryCache();

builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();


var app = builder.Build();

// Configure middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseMiddleware<RabbitMqMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
