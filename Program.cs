using Microsoft.EntityFrameworkCore;
using ClassBook.Domain.Interfaces;
using ClassBook.Infrastructure.Data;
using ClassBook.Application.Facades;
using ClassBook.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace ClassBook
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(
                    builder.Configuration.GetConnectionString("DefaultConnection")
                ));

            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.AccessDeniedPath = "/access-denied";
                    options.ExpireTimeSpan = TimeSpan.FromDays(7);
                    options.SlidingExpiration = true;
                    options.Cookie.SameSite = SameSiteMode.None;  
                    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;  
                });

            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("AdminOnly", policy => policy.RequireRole("Администратор"));
            });

            builder.Services.AddScoped<IPasswordHasher, Sha256PasswordHasherAdapter>();

            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            builder.Services.AddScoped<AttendanceFacade>();
            builder.Services.AddScoped<GradeFacade>();
            builder.Services.AddScoped<AuthFacade>();
            builder.Services.AddScoped<UserFacade>();
            builder.Services.AddScoped<ClassFacade>();
            builder.Services.AddScoped<SubjectFacade>();
            builder.Services.AddScoped<LessonFacade>();
            builder.Services.AddScoped<StudentFacade>();

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll",
                    policy => policy
                        .WithOrigins(
                            "http://localhost:5500",
                            "http://127.0.0.1:5500",
                            "https://localhost:7062"
                        )
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials());
            });
            

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.UseCors("AllowAll");           
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseHttpsRedirection();
            app.MapControllers();

            app.Run();
        }
    }
}