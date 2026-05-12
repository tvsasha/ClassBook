using Microsoft.EntityFrameworkCore;
using ClassBook.Domain.Interfaces;
using ClassBook.Infrastructure.Data;
using ClassBook.Application.Facades;
using ClassBook.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.OpenApi.Models;
using System.IO;
using System.Security.Claims;
using System.Reflection;
using System.Text;

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

            builder.Services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtectionKeys")))
                .SetApplicationName("ClassBook");

            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("AdminOnly", policy => policy.RequireRole("Администратор"));
                options.AddPolicy("ScheduleManagerOnly", policy => policy.RequireRole("Менеджер расписания", "Администратор"));
                options.AddPolicy("DirectorOnly", policy => policy.RequireRole("Директор", "Администратор"));
                options.AddPolicy("TeacherOrAdmin", policy => policy.RequireRole("Учитель", "Администратор"));
                options.AddPolicy("StudentOrParent", policy => policy.RequireRole("Ученик", "Родитель"));
            });

            builder.Services.AddScoped<IPasswordHasher, AspNetIdentityPasswordHasherAdapter>();

            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "ClassBook API",
                    Version = "v1",
                    Description = "API системы электронного журнала ClassBook для администрирования, учебного процесса, расписания, родительских и ученических кабинетов."
                });

                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                if (File.Exists(xmlPath))
                {
                    options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
                }
            });

            builder.Services.AddScoped<AttendanceFacade>();
            builder.Services.AddScoped<GradeFacade>();
            builder.Services.AddScoped<AuthFacade>();
            builder.Services.AddScoped<UserFacade>();
            builder.Services.AddScoped<ClassFacade>();
            builder.Services.AddScoped<SubjectFacade>();
            builder.Services.AddScoped<LessonFacade>();
            builder.Services.AddScoped<StudentFacade>();
            builder.Services.AddScoped<AuditFacade>();
            builder.Services.AddScoped<ScheduleFacade>();
            builder.Services.AddScoped<ParentFacade>();
            builder.Services.AddScoped<AnalyticsFacade>();
            builder.Services.AddScoped<RoleFacade>();
            builder.Services.AddScoped<ClassTeacherFacade>();

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll",
                    policy => policy
                        .WithOrigins(
                            "http://localhost:5500",
                            "http://localhost:5501",
                            "http://localhost:5502",
                            "http://localhost:5503",
                            "http://localhost:5504",
                            "http://localhost:5505",
                            "http://127.0.0.1:5500",
                            "http://127.0.0.1:5501",
                            "http://127.0.0.1:5502",
                            "http://127.0.0.1:5503",
                            "http://127.0.0.1:5504",
                            "http://127.0.0.1:5505",
                            "http://localhost:5173",
                            "http://127.0.0.1:5173",
                            "https://localhost:7062"
                        )
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials());
            });
            

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.Migrate();
            }

            if (args.Length >= 2 && args[0].Equals("import-roster", StringComparison.OrdinalIgnoreCase))
            {
                using var scope = app.Services.CreateScope();
                var facade = scope.ServiceProvider.GetRequiredService<StudentFacade>();
                using var stream = File.OpenRead(args[1]);
                var result = facade.ImportSchoolRosterDocxAsync(stream).GetAwaiter().GetResult();

                Console.WriteLine($"Импорт завершен. Ученики: {result.Imported}, пропущено: {result.Skipped}, учителей создано: {result.TeachersCreated}, связей руководителей: {result.ClassTeacherLinksCreated}");
                foreach (var teacher in result.Teachers.Where(t => t.Created))
                {
                    Console.WriteLine($"Учитель: {teacher.FullName}; логин: {teacher.Login}; временный пароль: {teacher.TemporaryPassword}");
                }

                return;
            }

            if (args.Length >= 2 && args[0].Equals("import-parents", StringComparison.OrdinalIgnoreCase))
            {
                using var scope = app.Services.CreateScope();
                var facade = scope.ServiceProvider.GetRequiredService<ParentFacade>();
                using var stream = File.OpenRead(args[1]);
                var result = facade.ImportParentRosterDocxAsync(stream).GetAwaiter().GetResult();

                Console.WriteLine($"Импорт родителей завершен. Создано: {result.ParentsCreated}, найдено: {result.ParentsFound}, привязок: {result.LinksCreated}, пропущено: {result.Skipped}, ошибок: {result.Errors.Count}");
                foreach (var parent in result.Parents.Where(parent => parent.Created))
                {
                    Console.WriteLine($"Родитель: {parent.FullName}; логин: {parent.Login}; временный пароль: {parent.TemporaryPassword}; дети: {string.Join(", ", parent.LinkedStudents)}");
                }

                return;
            }

            if (args.Length >= 2 && args[0].Equals("reset-parent-accesses", StringComparison.OrdinalIgnoreCase))
            {
                using var scope = app.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
                var parentRole = db.Roles.FirstOrDefault(role => role.Name == "Родитель");
                if (parentRole == null)
                {
                    Console.WriteLine("Роль 'Родитель' не найдена");
                    return;
                }

                var since = DateTime.Today;
                var parents = db.Users
                    .Include(user => user.StudentParents!)
                    .ThenInclude(link => link.Student)
                    .Where(user => user.RoleId == parentRole.Id && user.CreatedAt >= since)
                    .OrderBy(user => user.FullName)
                    .ToList();

                var csv = new StringBuilder();
                csv.AppendLine("ФИО;Логин;Временный пароль;Дети");
                foreach (var parent in parents)
                {
                    var temporaryPassword = UserFacade.GenerateTemporaryPassword();
                    parent.PasswordHash = hasher.Hash(temporaryPassword);
                    parent.MustChangePassword = true;
                    parent.IsActive = true;
                    var children = string.Join(", ", parent.StudentParents?.Select(link => $"{link.Student.LastName} {link.Student.FirstName}".Trim()) ?? []);
                    csv.Append('"').Append(parent.FullName.Replace("\"", "\"\"")).Append("\";")
                        .Append('"').Append(parent.Login.Replace("\"", "\"\"")).Append("\";")
                        .Append('"').Append(temporaryPassword.Replace("\"", "\"\"")).Append("\";")
                        .Append('"').Append(children.Replace("\"", "\"\"")).AppendLine("\"");
                }

                db.SaveChanges();
                File.WriteAllText(args[1], csv.ToString(), new UTF8Encoding(true));
                Console.WriteLine($"Сброшено доступов родителей: {parents.Count}. Файл: {args[1]}");
                return;
            }

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseExceptionHandler(errorApp =>
            {
                errorApp.Run(async context =>
                {
                    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

                    if (exception != null)
                    {
                        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                        logger.LogError(exception, "Неперехваченное исключение при обработке запроса {Path}", context.Request.Path);
                    }

                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    context.Response.ContentType = "application/json; charset=utf-8";

                    await context.Response.WriteAsJsonAsync(new ClassBook.Controllers.ApiErrorResponse(
                        "Внутренняя ошибка сервера",
                        "server_error"));
                });
            });

            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.UseHttpsRedirection();
            app.UseCors("AllowAll");
            app.UseAuthentication();

            app.Use(async (context, next) =>
            {
                var path = context.Request.Path.Value ?? string.Empty;
                var isApiRequest = path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase);
                var isAllowedApi = path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase)
                    || path.Equals("/api/auth/logout", StringComparison.OrdinalIgnoreCase)
                    || path.Equals("/api/auth/change-password", StringComparison.OrdinalIgnoreCase);

                if (isApiRequest && !isAllowedApi && context.User.Identity?.IsAuthenticated == true)
                {
                    var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    if (int.TryParse(userIdClaim, out var userId))
                    {
                        var db = context.RequestServices.GetRequiredService<AppDbContext>();
                        var mustChangePassword = await db.Users
                            .Where(u => u.Id == userId && u.IsActive)
                            .Select(u => u.MustChangePassword)
                            .FirstOrDefaultAsync();

                        if (mustChangePassword)
                        {
                            context.Response.StatusCode = StatusCodes.Status403Forbidden;
                            await context.Response.WriteAsJsonAsync(new
                            {
                                message = "Необходимо сменить временный пароль перед дальнейшей работой."
                            });
                            return;
                        }
                    }
                }

                await next();
            });

            app.UseAuthorization();
            app.MapControllers();
            app.MapFallbackToFile("/app/{*path:nonfile}", "app/index.html");

            app.Run();
        }
    }
}
