using Microsoft.EntityFrameworkCore;
using ClassBook.Domain.Interfaces;
using ClassBook.Infrastructure.Data;
using ClassBook.Application.Facades;
using ClassBook.Infrastructure.Security;
using ClassBook.Domain.Entities;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.OpenApi.Models;
using System.IO;
using System.Security.Claims;
using System.Reflection;
using System.Text;
using ClassBook.Domain.Constants;

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
                    options.Cookie.SameSite = builder.Configuration.GetValue("Cookie:SameSite", SameSiteMode.None);
                    options.Cookie.SecurePolicy = builder.Configuration.GetValue("Cookie:SecurePolicy", CookieSecurePolicy.Always);
                });

            builder.Services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtectionKeys")))
                .SetApplicationName("ClassBook");

            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
            });

            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("AdminOnly", policy => policy.RequireRole("Администратор"));
                options.AddPolicy("ScheduleManagerOnly", policy => policy.RequireRole("Менеджер расписания", "Администратор"));
                options.AddPolicy("ScheduleViewOnly", policy => policy.RequireRole("Менеджер расписания", "Администратор", "Директор", "Учитель"));
                options.AddPolicy("DirectorOnly", policy => policy.RequireRole("Директор", "Администратор"));
                options.AddPolicy("TeacherOrAdmin", policy => policy.RequireRole("Учитель", "Администратор"));
                options.AddPolicy("StudentOrParent", policy => policy.RequireRole("Ученик", "Родитель"));
            });

            builder.Services.AddScoped<IPasswordHasher, AspNetIdentityPasswordHasherAdapter>();
            builder.Services.AddMemoryCache();

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
            builder.Services.AddScoped<FinalGradeFacade>();

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
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                ApplyMigrationsWithRetry(db, logger);
                var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
                EnsureBootstrapAdmin(db, hasher);
            }

            if (args.Length >= 1 && args[0].Equals("normalize-demo-data", StringComparison.OrdinalIgnoreCase))
            {
                using var scope = app.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                NormalizeDemoData(db);
                return;
            }

            if (args.Length >= 2 && args[0].Equals("import-roster", StringComparison.OrdinalIgnoreCase))
            {
                using var scope = app.Services.CreateScope();
                var facade = scope.ServiceProvider.GetRequiredService<StudentFacade>();
                using var stream = File.OpenRead(args[1]);
                var result = facade.ImportSchoolRosterDocxAsync(stream).GetAwaiter().GetResult();

                Console.WriteLine($"Импорт завершен. Ученики: {result.Imported}, пропущено: {result.Skipped}, учителей создано: {result.TeachersCreated}, связей руководителей: {result.ClassTeacherLinksCreated}");
                foreach (var error in result.Errors)
                {
                    Console.WriteLine($"Ошибка: {error}");
                }

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
                foreach (var error in result.Errors)
                {
                    Console.WriteLine($"Ошибка: {error}");
                }

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

            if (args.Length >= 2 && args[0].Equals("reset-teacher-accesses", StringComparison.OrdinalIgnoreCase))
            {
                using var scope = app.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
                var teacherRole = db.Roles.FirstOrDefault(role => role.Name == "Учитель");
                if (teacherRole == null)
                {
                    Console.WriteLine("Роль 'Учитель' не найдена");
                    return;
                }

                var teachers = db.Users
                    .Where(user => user.RoleId == teacherRole.Id)
                    .OrderBy(user => user.FullName)
                    .ToList();

                var csv = new StringBuilder();
                csv.AppendLine("ФИО;Логин;Временный пароль");
                foreach (var teacher in teachers)
                {
                    var temporaryPassword = UserFacade.GenerateTemporaryPassword();
                    teacher.PasswordHash = hasher.Hash(temporaryPassword);
                    teacher.MustChangePassword = true;
                    teacher.IsActive = true;
                    csv.Append('"').Append(teacher.FullName.Replace("\"", "\"\"")).Append("\";")
                        .Append('"').Append(teacher.Login.Replace("\"", "\"\"")).Append("\";")
                        .Append('"').Append(temporaryPassword.Replace("\"", "\"\"")).AppendLine("\"");
                }

                db.SaveChanges();
                File.WriteAllText(args[1], csv.ToString(), new UTF8Encoding(true));
                Console.WriteLine($"Сброшено доступов учителей: {teachers.Count}. Файл: {args[1]}");
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

            app.Use(async (context, next) =>
            {
                context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
                context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
                context.Response.Headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
                context.Response.Headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
                await next();
            });

            app.Use(async (context, next) =>
            {
                var path = context.Request.Path.Value ?? string.Empty;
                var legacyPublicFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "/",
                    "/index.html",
                    "/login.html",
                    "/change-password.html",
                    "/director-dashboard.html",
                    "/parent-portal.html",
                    "/raspisanie.html",
                    "/student-portal.html",
                    "/teacher.html",
                    "/main.js",
                    "/style.css",
                    "/12.py"
                };

                if (legacyPublicFiles.Contains(path))
                {
                    context.Response.Redirect("/app/", permanent: false);
                    return;
                }

                await next();
            });

            app.UseDefaultFiles();
            app.UseStaticFiles(new StaticFileOptions
            {
                OnPrepareResponse = context =>
                {
                    var path = context.Context.Request.Path.Value ?? string.Empty;
                    if (path.StartsWith("/app/", StringComparison.OrdinalIgnoreCase))
                    {
                        context.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
                        context.Context.Response.Headers.Pragma = "no-cache";
                        context.Context.Response.Headers.Expires = "0";
                    }
                }
            });
            app.UseForwardedHeaders();
            app.UseHttpsRedirection();
            app.UseCors("AllowAll");
            app.UseAuthentication();

            app.Use(async (context, next) =>
            {
                var path = context.Request.Path.Value ?? string.Empty;
                var method = context.Request.Method;
                var isUnsafeApi = path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
                    && !HttpMethods.IsGet(method)
                    && !HttpMethods.IsHead(method)
                    && !HttpMethods.IsOptions(method)
                    && !path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase)
                    && !path.Equals("/api/auth/csrf", StringComparison.OrdinalIgnoreCase)
                    && !path.Equals("/api/auth/heartbeat", StringComparison.OrdinalIgnoreCase)
                    && !path.Equals("/api/auth/offline", StringComparison.OrdinalIgnoreCase);

                if (isUnsafeApi && context.User.Identity?.IsAuthenticated == true && !IsValidCsrfToken(context))
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsJsonAsync(new ClassBook.Controllers.ApiErrorResponse(
                        "Не удалось подтвердить безопасность запроса. Обновите страницу и попробуйте еще раз.",
                        "csrf_failed"));
                    return;
                }

                await next();
            });

            app.Use(async (context, next) =>
            {
                var path = context.Request.Path.Value ?? string.Empty;
                var isApiRequest = path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase);
                var isAllowedApi = path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase)
                    || path.Equals("/api/auth/logout", StringComparison.OrdinalIgnoreCase)
                    || path.Equals("/api/auth/change-password", StringComparison.OrdinalIgnoreCase)
                    || path.Equals("/api/auth/heartbeat", StringComparison.OrdinalIgnoreCase)
                    || path.Equals("/api/auth/offline", StringComparison.OrdinalIgnoreCase);

                var isQrLogin = string.Equals(context.User.FindFirst("auth_method")?.Value, "qr", StringComparison.Ordinal);
                if (isApiRequest && !isAllowedApi && !isQrLogin && context.User.Identity?.IsAuthenticated == true)
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

        private static void EnsureBootstrapAdmin(AppDbContext db, IPasswordHasher hasher)
        {
            const string legacyLogin = "1";
            const string legacyPassword = "1";
            const string fullName = "Администратор";

            var adminRole = db.Roles.First(role => role.Name == "Администратор");
            var legacyAdmin = db.Users.FirstOrDefault(user => user.Login == legacyLogin);
            var bootstrapPassword = Environment.GetEnvironmentVariable("CLASSBOOK_BOOTSTRAP_ADMIN_PASSWORD");
            var hasOtherActiveAdmin = db.Users.Any(user =>
                user.Login != legacyLogin &&
                user.RoleId == adminRole.Id &&
                user.IsActive);

            if (legacyAdmin == null)
            {
                if (string.IsNullOrWhiteSpace(bootstrapPassword))
                    return;

                db.Users.Add(new User
                {
                    Login = legacyLogin,
                    FullName = fullName,
                    PasswordHash = hasher.Hash(bootstrapPassword),
                    RoleId = adminRole.Id,
                    IsActive = true,
                    MustChangePassword = true,
                    CreatedAt = DateTime.UtcNow
                });
                db.SaveChanges();
                return;
            }

            var changed = false;
            if (hasher.Verify(legacyPassword, legacyAdmin.PasswordHash))
            {
                if (hasOtherActiveAdmin)
                {
                    legacyAdmin.IsActive = false;
                    legacyAdmin.MustChangePassword = true;
                    changed = true;
                }
                else if (!string.IsNullOrWhiteSpace(bootstrapPassword))
                {
                    legacyAdmin.PasswordHash = hasher.Hash(bootstrapPassword);
                    legacyAdmin.MustChangePassword = true;
                    changed = true;
                }
            }

            if (changed)
                db.SaveChanges();
        }

        private static bool IsValidCsrfToken(HttpContext context)
        {
            const string cookieName = "ClassBook.Csrf";
            const string headerName = "X-CSRF-TOKEN";

            if (!context.Request.Cookies.TryGetValue(cookieName, out var cookieToken))
                return false;

            var headerToken = context.Request.Headers[headerName].FirstOrDefault();
            return !string.IsNullOrWhiteSpace(cookieToken)
                && !string.IsNullOrWhiteSpace(headerToken)
                && string.Equals(cookieToken, headerToken, StringComparison.Ordinal);
        }

        private static void NormalizeDemoData(AppDbContext db)
        {
            using var transaction = db.Database.BeginTransaction();

            NormalizeDemoUsers(db);
            NormalizeDemoSubjects(db);
            ConsolidateDemoSubjects(db);
            DeduplicateSubjectClassAssignments(db);
            db.SaveChanges();

            NormalizeDemoLessons(db);
            EnsureOvzLessons(db);
            EnsureLessonSubjectAssignments(db);
            db.SaveChanges();

            SeedDemoAttendanceAndGrades(db);
            db.SaveChanges();

            transaction.Commit();

            var questionUsers = db.Users.Count(user => user.FullName.Contains("?"));
            var ordinaryGrades = db.Grades
                .Include(grade => grade.Student)
                .ThenInclude(student => student.Class)
                .AsEnumerable()
                .Count(grade => grade.Student.Class != null && !IsOvzClass(grade.Student.Class.Name));
            var ovzGrades = db.Grades
                .Include(grade => grade.Student)
                .ThenInclude(student => student.Class)
                .AsEnumerable()
                .Count(grade => grade.Student.Class != null && IsOvzClass(grade.Student.Class.Name));
            var attendance = db.Attendances.Count();

            Console.WriteLine($"Нормализация демо-данных завершена. Пользователей с ????: {questionUsers}. Оценок: {ordinaryGrades}. Оценок ОВЗ: {ovzGrades}. Посещаемость: {attendance}.");
        }

        private static void NormalizeDemoUsers(AppDbContext db)
        {
            var teacherNamesById = new Dictionary<int, string>
            {
                [2] = "Тарзиева Нина Адилхоновна",
                [3] = "Кудабаева Дана Нурлановна",
                [4] = "Милош Алевтина Валерьевна",
                [5] = "Абдрахимова Асимгуль Асембековна",
                [6] = "Мироненко Надежда Васильевна",
                [7] = "Соколова Маргарита Юрьевна",
                [8] = "Усачева Марина Николаевна",
                [9] = "Агишева Ольга Ивановна",
                [11] = "Даниленко Анна Александровна",
                [12] = "Бушуева Наталья Михайловна",
                [13] = "Пелеева Татьяна Геннадьевна",
                [73] = "Смирнов Алексей Викторович",
                [78] = "Иванченко Александра Анатольевна",
                [80] = "Кузьмина Анастасия Владимировна",
                [83] = "Иванникова Татьяна Сергеевна",
                [88] = "Ковалев Артем Сергеевич",
                [89] = "Соловьева Алина Вячеславовна",
                [90] = "Давлетшина Румия Минигалимовна",
                [91] = "Егорова Дарья Константиновна",
                [92] = "Гаврилова Ольга Петровна",
                [93] = "Тихонова Валерия Сергеевна",
                [94] = "Михайлова Ксения Андреевна",
                [95] = "Литвинюк Марина Александровна",
                [96] = "Фролова Надежда Ильинична",
                [97] = "Семенова Екатерина Денисовна",
                [98] = "Павлова Марина Витальевна",
                [99] = "Орлова Екатерина Павловна",
                [100] = "Калинина Вера Николаевна",
                [101] = "Новикова Татьяна Алексеевна",
                [102] = "Васильева Людмила Юрьевна",
                [103] = "Андреева Елена Борисовна",
                [104] = "Беляева Оксана Сергеевна",
                [105] = "Виноградова Ирина Павловна",
                [106] = "Гордеева Мария Викторовна",
                [107] = "Демидова Анна Игоревна",
                [108] = "Жукова Полина Александровна",
                [109] = "Петрова Елена Сергеевна",
                [110] = "Зайцева Светлана Дмитриевна",
                [111] = "Карпова Любовь Николаевна",
                [112] = "Ларионова Юлия Олеговна",
                [113] = "Медведева Анастасия Романовна",
                [114] = "Назарова Валентина Петровна",
                [115] = "Федоров Илья Андреевич",
                [116] = "Осипова Галина Андреевна",
                [117] = "Прохорова Екатерина Михайловна",
                [118] = "Морозова Ирина Викторовна",
                [119] = "Рябова Дарья Евгеньевна",
                [126] = "Сафонова Ольга Владимировна",
                [127] = "Третьякова Наталья Сергеевна"
            };
            var fallbackTeacherNames = new[]
            {
                "Савельева Мария Андреевна",
                "Никитина Анна Сергеевна",
                "Громова Виктория Павловна",
                "Белова Елена Игоревна",
                "Захарова Наталья Викторовна",
                "Корнилова Оксана Дмитриевна",
                "Алексеева Ирина Олеговна",
                "Волкова Светлана Романовна",
                "Романова Юлия Михайловна",
                "Макарова Полина Евгеньевна",
                "Соловьева Алина Вячеславовна",
                "Егорова Дарья Константиновна",
                "Гаврилова Ольга Петровна",
                "Тихонова Валерия Сергеевна",
                "Михайлова Ксения Андреевна",
                "Фролова Надежда Ильинична",
                "Семенова Екатерина Денисовна",
                "Павлова Марина Витальевна",
                "Калинина Вера Николаевна",
                "Новикова Татьяна Алексеевна",
                "Васильева Людмила Юрьевна",
                "Андреева Елена Борисовна"
            };
            var usedTeacherNames = new HashSet<string>(teacherNamesById.Values, StringComparer.OrdinalIgnoreCase);
            var usedLogins = new HashSet<string>(
                db.Users
                    .Where(user => user.RoleId != SystemRoleIds.Teacher)
                    .Select(user => user.Login),
                StringComparer.OrdinalIgnoreCase);
            var fallbackIndex = 0;

            foreach (var user in db.Users.OrderBy(user => user.Id))
            {
                if (user.RoleId == SystemRoleIds.Teacher)
                {
                    if (teacherNamesById.TryGetValue(user.Id, out var teacherName))
                    {
                        user.FullName = teacherName;
                    }
                    else if (ShouldNormalizeTeacherName(user.FullName, user.Login))
                    {
                        while (fallbackIndex < fallbackTeacherNames.Length &&
                               usedTeacherNames.Contains(fallbackTeacherNames[fallbackIndex]))
                        {
                            fallbackIndex++;
                        }

                        user.FullName = fallbackIndex < fallbackTeacherNames.Length
                            ? fallbackTeacherNames[fallbackIndex++]
                            : $"Педагог школы {user.Id}";
                        usedTeacherNames.Add(user.FullName);
                    }

                    usedLogins.Remove(user.Login);
                    user.Login = BuildUniqueTeacherLogin(user.FullName, usedLogins, user.Id);
                    user.IsActive = true;
                    continue;
                }

                if (!user.FullName.Contains('?'))
                    continue;

                var source = $"{user.FullName} {user.Login}".ToLowerInvariant();
                user.FullName = source switch
                {
                    var text when text.Contains("мат") || text.Contains("алгеб") || text.Contains("геомет") => "Учитель математики",
                    var text when text.Contains("рус") => "Учитель русского языка",
                    var text when text.Contains("физ-ра") || text.Contains("физк") => "Педагог физической культуры",
                    var text when text.Contains("физик") => "Смирнов Алексей Викторович",
                    var text when text.Contains("информ") => "Ковалев Артем Сергеевич",
                    var text when text.Contains("биолог") => "Учитель биологии",
                    var text when text.Contains("хим") => "Учитель химии",
                    var text when text.Contains("об-") || text.Contains("общест") => "Учитель обществознания",
                    var text when text.Contains("анг") => "Учитель английского языка",
                    var text when text.Contains("лит") || text.Contains("чтен") => "Учитель литературы",
                    var text when text.Contains("музык") => "Учитель музыки",
                    _ => "Учитель начальных классов"
                };
                user.IsActive = true;
            }
        }

        private static bool ShouldNormalizeTeacherName(string fullName, string login)
        {
            var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return fullName.Contains('?') ||
                   fullName.Contains(':') ||
                   fullName.Contains('.') ||
                   fullName.StartsWith("Учитель", StringComparison.OrdinalIgnoreCase) ||
                   fullName.StartsWith("Педагог", StringComparison.OrdinalIgnoreCase) ||
                   fullName.StartsWith("Преподаватель", StringComparison.OrdinalIgnoreCase) ||
                   login.StartsWith("schedule.", StringComparison.OrdinalIgnoreCase) ||
                   parts.Length < 3;
        }

        private static string BuildUniqueTeacherLogin(string fullName, HashSet<string> usedLogins, int userId)
        {
            var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var baseLogin = parts.Length >= 2
                ? $"{Transliterate(parts[0])}.{Transliterate(parts[1])}"
                : $"teacher.{userId}";
            baseLogin = baseLogin.ToLowerInvariant().Trim('.');
            if (string.IsNullOrWhiteSpace(baseLogin))
                baseLogin = $"teacher.{userId}";

            var login = baseLogin;
            var suffix = 2;
            while (usedLogins.Contains(login))
            {
                login = $"{baseLogin}.{suffix++}";
            }

            usedLogins.Add(login);
            return login;
        }

        private static string Transliterate(string value)
        {
            var map = new Dictionary<char, string>
            {
                ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d", ['е'] = "e", ['ё'] = "e",
                ['ж'] = "zh", ['з'] = "z", ['и'] = "i", ['й'] = "y", ['к'] = "k", ['л'] = "l", ['м'] = "m",
                ['н'] = "n", ['о'] = "o", ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t", ['у'] = "u",
                ['ф'] = "f", ['х'] = "h", ['ц'] = "ts", ['ч'] = "ch", ['ш'] = "sh", ['щ'] = "sch",
                ['ы'] = "y", ['э'] = "e", ['ю'] = "yu", ['я'] = "ya"
            };
            var builder = new StringBuilder();
            foreach (var character in value.ToLowerInvariant())
            {
                if (map.TryGetValue(character, out var replacement))
                {
                    builder.Append(replacement);
                }
                else if (character is >= 'a' and <= 'z' or >= '0' and <= '9')
                {
                    builder.Append(character);
                }
            }
            return builder.ToString();
        }

        private static void NormalizeDemoSubjects(AppDbContext db)
        {
            var users = db.Users.ToDictionary(user => user.Id);
            var classesBySubject = db.SubjectClassAssignments
                .Include(assignment => assignment.Class)
                .GroupBy(assignment => assignment.SubjectId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(assignment => assignment.Class.Name).Distinct().ToList());

            foreach (var subject in db.Subjects)
            {
                subject.Name = NormalizeSubjectName(subject.Name);
                var classNames = classesBySubject.TryGetValue(subject.SubjectId, out var names) ? names : new List<string>();
                subject.TeacherId = PickTeacherIdForSubject(db, subject.Name, classNames, subject.TeacherId);
                if (users.TryGetValue(subject.TeacherId, out var teacher))
                    teacher.IsActive = true;
            }

            foreach (var assignment in db.SubjectClassAssignments.Include(assignment => assignment.Subject).Include(assignment => assignment.Class))
            {
                assignment.TeacherId = PickTeacherIdForSubject(
                    db,
                    assignment.Subject.Name,
                    new[] { assignment.Class.Name },
                    assignment.TeacherId);
            }
        }

        private static void DeduplicateSubjectClassAssignments(AppDbContext db)
        {
            var duplicates = db.SubjectClassAssignments
                .AsEnumerable()
                .GroupBy(assignment => new { assignment.SubjectId, assignment.ClassId, assignment.TeacherId })
                .SelectMany(group => group.OrderBy(assignment => assignment.SubjectClassAssignmentId).Skip(1))
                .ToList();

            if (duplicates.Count > 0)
                db.SubjectClassAssignments.RemoveRange(duplicates);
        }

        private static void ConsolidateDemoSubjects(AppDbContext db)
        {
            var subjects = db.Subjects.ToList();
            foreach (var duplicateGroup in subjects
                         .GroupBy(subject => new { subject.Name, subject.TeacherId })
                         .Where(group => group.Count() > 1))
            {
                var keeper = duplicateGroup.OrderBy(subject => subject.SubjectId).First();
                foreach (var duplicate in duplicateGroup.Where(subject => subject.SubjectId != keeper.SubjectId))
                {
                    foreach (var lesson in db.Lessons.Where(lesson => lesson.SubjectId == duplicate.SubjectId))
                        lesson.SubjectId = keeper.SubjectId;

                    foreach (var assignment in db.SubjectClassAssignments.Where(assignment => assignment.SubjectId == duplicate.SubjectId))
                        assignment.SubjectId = keeper.SubjectId;

                    db.Subjects.Remove(duplicate);
                }
            }
        }

        private static void NormalizeDemoLessons(AppDbContext db)
        {
            var subjects = db.Subjects.ToDictionary(subject => subject.SubjectId);
            var classes = db.Classes.ToDictionary(classEntity => classEntity.ClassId);

            foreach (var lesson in db.Lessons)
            {
                if (!subjects.TryGetValue(lesson.SubjectId, out var subject))
                    continue;

                subject.Name = NormalizeSubjectName(subject.Name);
                var className = classes.TryGetValue(lesson.ClassId, out var classEntity) ? classEntity.Name : string.Empty;
                lesson.TeacherId = PickTeacherIdForSubject(db, subject.Name, new[] { className }, lesson.TeacherId);

                var sample = GetLessonSample(subject.Name, lesson.Date, lesson.LessonId);
                lesson.Topic = sample.Topic;
                lesson.Homework = IsOvzClass(className) ? null : sample.Homework;
            }
        }

        private static void EnsureOvzLessons(AppDbContext db)
        {
            var ovzSubjects = new[] { "Русский язык", "Математика", "Литературное чтение", "Окружающий мир", "Самоподготовка" };
            var start = new DateTime(2026, 5, 11);
            var dayOffsets = new[] { 0, 1, 2, 3, 4, 7, 8, 9, 10, 11, 14, 15, 16, 17, 18 };
            EnsureDemoScheduleSlots(db);
            var slots = db.Schedules
                .AsEnumerable()
                .GroupBy(slot => new { slot.DayOfWeek, slot.LessonNumber })
                .ToDictionary(group => group.Key, group => group.OrderBy(slot => slot.ScheduleId).First());

            foreach (var classEntity in db.Classes.AsEnumerable().Where(classEntity => IsOvzClass(classEntity.Name)).ToList())
            {
                var teacherId = db.ClassTeachers
                    .Where(link => link.ClassId == classEntity.ClassId)
                    .Select(link => link.TeacherId)
                    .FirstOrDefault();
                if (teacherId == 0)
                    teacherId = classEntity.Name == "1А" ? 9 : classEntity.Name == "1Б" ? 11 : 12;

                for (var subjectIndex = 0; subjectIndex < ovzSubjects.Length; subjectIndex++)
                {
                    var subjectName = ovzSubjects[subjectIndex];
                    var subject = db.Subjects.FirstOrDefault(item => item.Name == subjectName && item.TeacherId == teacherId);
                    if (subject == null)
                    {
                        subject = new Subject { Name = subjectName, TeacherId = teacherId };
                        db.Subjects.Add(subject);
                        db.SaveChanges();
                    }

                    if (!db.SubjectClassAssignments.Any(link =>
                            link.SubjectId == subject.SubjectId &&
                            link.ClassId == classEntity.ClassId &&
                            link.TeacherId == teacherId))
                    {
                        db.SubjectClassAssignments.Add(new SubjectClassAssignment
                        {
                            SubjectId = subject.SubjectId,
                            ClassId = classEntity.ClassId,
                            TeacherId = teacherId,
                            CreatedAt = DateTime.UtcNow
                        });
                    }

                    foreach (var offset in dayOffsets)
                    {
                        var date = start.AddDays(offset);
                        var dayOfWeek = GetSchoolDayOfWeek(date);
                        if (dayOfWeek < 0 || !slots.TryGetValue(new { DayOfWeek = dayOfWeek, LessonNumber = subjectIndex + 1 }, out var slot))
                        {
                            continue;
                        }

                        var sample = GetLessonSample(subjectName, date, offset);
                        var existingLesson = db.Lessons.FirstOrDefault(lesson =>
                            lesson.ClassId == classEntity.ClassId &&
                            lesson.SubjectId == subject.SubjectId &&
                            lesson.Date == date);

                        if (existingLesson != null)
                        {
                            existingLesson.TeacherId = teacherId;
                            existingLesson.ScheduleId = slot.ScheduleId;
                            existingLesson.Topic = sample.Topic;
                            existingLesson.Homework = null;
                            continue;
                        }

                        db.Lessons.Add(new Lesson
                        {
                            SubjectId = subject.SubjectId,
                            ClassId = classEntity.ClassId,
                            TeacherId = teacherId,
                            ScheduleId = slot.ScheduleId,
                            Date = date,
                            Topic = sample.Topic,
                            Homework = null
                        });
                    }
                }
            }
        }

        private static void EnsureDemoScheduleSlots(AppDbContext db)
        {
            if (db.Schedules.Any())
                return;

            var defaultTimes = new (string Start, string End)[]
            {
                ("09:15", "09:55"),
                ("10:10", "10:50"),
                ("10:55", "11:35"),
                ("11:45", "12:25"),
                ("12:30", "13:10"),
                ("13:30", "14:10"),
                ("14:15", "14:55")
            };

            for (var day = 0; day < 5; day++)
            {
                for (var index = 0; index < defaultTimes.Length; index++)
                {
                    db.Schedules.Add(new Schedule
                    {
                        DayOfWeek = day,
                        LessonNumber = index + 1,
                        StartTime = TimeSpan.Parse(defaultTimes[index].Start),
                        EndTime = TimeSpan.Parse(defaultTimes[index].End),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }

            db.SaveChanges();
        }

        private static void SeedDemoAttendanceAndGrades(AppDbContext db)
        {
            var lessons = db.Lessons.Include(lesson => lesson.Class).Include(lesson => lesson.Subject).ToList();
            var studentsByClass = db.Students
                .Where(student => student.ClassId != null)
                .GroupBy(student => student.ClassId!.Value)
                .ToDictionary(group => group.Key, group => group.ToList());
            var attendanceKeys = db.Attendances
                .Select(attendance => new { attendance.StudentId, attendance.LessonId })
                .AsEnumerable()
                .ToHashSet();
            var gradeKeys = db.Grades
                .Select(grade => new { grade.StudentId, grade.LessonId })
                .AsEnumerable()
                .ToHashSet();
            var ovzStudentIds = db.Students
                .Include(student => student.Class)
                .AsEnumerable()
                .Where(student => student.Class != null && IsOvzClass(student.Class.Name))
                .Select(student => student.StudentId)
                .ToHashSet();
            var ovzGrades = db.Grades.Where(grade => ovzStudentIds.Contains(grade.StudentId)).ToList();
            if (ovzGrades.Count > 0)
                db.Grades.RemoveRange(ovzGrades);

            foreach (var grade in db.Grades.Include(grade => grade.Student).ThenInclude(student => student.Class))
            {
                if (grade.Student.Class != null && !IsOvzClass(grade.Student.Class.Name))
                    grade.Value = GetGradeValue(grade.StudentId, grade.LessonId);
            }

            foreach (var lesson in lessons)
            {
                if (!studentsByClass.TryGetValue(lesson.ClassId, out var students))
                    continue;

                foreach (var student in students)
                {
                    var attendanceKey = new { student.StudentId, lesson.LessonId };
                    if (!attendanceKeys.Contains(attendanceKey))
                    {
                        db.Attendances.Add(new Attendance
                        {
                            StudentId = student.StudentId,
                            LessonId = lesson.LessonId,
                            Status = GetAttendanceStatus(student.StudentId, lesson.LessonId)
                        });
                        attendanceKeys.Add(attendanceKey);
                    }

                    if (IsOvzClass(lesson.Class.Name))
                        continue;

                    var gradeKey = new { student.StudentId, lesson.LessonId };
                    if (!gradeKeys.Contains(gradeKey) && ShouldCreateGrade(student.StudentId, lesson.LessonId))
                    {
                        db.Grades.Add(new Grade
                        {
                            StudentId = student.StudentId,
                            LessonId = lesson.LessonId,
                            Value = GetGradeValue(student.StudentId, lesson.LessonId)
                        });
                        gradeKeys.Add(gradeKey);
                    }
                }
            }

            db.SaveChanges();

            var ordinaryStudents = db.Students
                .Include(student => student.Class)
                .AsEnumerable()
                .Where(student => student.Class != null && !IsOvzClass(student.Class.Name))
                .ToList();

            foreach (var student in ordinaryStudents)
            {
                var currentCount = db.Grades.Count(grade => grade.StudentId == student.StudentId);
                if (currentCount >= 6 || student.ClassId == null)
                    continue;

                var candidateLessons = lessons
                    .Where(lesson => lesson.ClassId == student.ClassId.Value)
                    .OrderBy(lesson => lesson.Date)
                    .ThenBy(lesson => lesson.LessonId)
                    .ToList();

                foreach (var lesson in candidateLessons)
                {
                    if (currentCount >= 6)
                        break;

                    if (db.Grades.Any(grade => grade.StudentId == student.StudentId && grade.LessonId == lesson.LessonId))
                        continue;

                    db.Grades.Add(new Grade
                    {
                        StudentId = student.StudentId,
                        LessonId = lesson.LessonId,
                        Value = GetGradeValue(student.StudentId + currentCount, lesson.LessonId)
                    });
                    currentCount++;
                }
            }
        }

        private static void EnsureLessonSubjectAssignments(AppDbContext db)
        {
            var existingKeys = db.SubjectClassAssignments
                .Select(assignment => new { assignment.SubjectId, assignment.ClassId, assignment.TeacherId })
                .AsEnumerable()
                .ToHashSet();

            var lessonKeys = db.Lessons
                .Select(lesson => new { lesson.SubjectId, lesson.ClassId, lesson.TeacherId })
                .AsEnumerable()
                .Distinct()
                .ToList();

            foreach (var key in lessonKeys)
            {
                if (existingKeys.Contains(key))
                    continue;

                db.SubjectClassAssignments.Add(new SubjectClassAssignment
                {
                    SubjectId = key.SubjectId,
                    ClassId = key.ClassId,
                    TeacherId = key.TeacherId,
                    CreatedAt = DateTime.UtcNow
                });
                existingKeys.Add(key);
            }
        }

        private static string NormalizeSubjectName(string value)
        {
            var name = (value ?? string.Empty).Trim().ToLowerInvariant().Replace('ё', 'е');
            name = name.Replace(".", string.Empty).Replace(",", string.Empty);

            if (name.Contains("об-во") || name.Contains("общество") || name.Contains("обществ")) return "Обществознание";
            if (name.Contains("рус")) return "Русский язык";
            if (name.Contains("мат-ка") || name == "мат" || name.Contains("матем")) return "Математика";
            if (name.Contains("алгеб")) return "Алгебра";
            if (name.Contains("геом")) return "Геометрия";
            if (name.Contains("анг")) return "Английский язык";
            if (name.Contains("биолог")) return "Биология";
            if (name.Contains("географ")) return "География";
            if (name.Contains("истор")) return "История";
            if (name.Contains("информ")) return "Информатика";
            if (name.Contains("лит чт") || name.Contains("чтение")) return "Литературное чтение";
            if (name.Contains("лит")) return "Литература";
            if (name.Contains("физ-ра") || name.Contains("физк")) return "Физическая культура";
            if (name.Contains("физик")) return "Физика";
            if (name.Contains("хим")) return "Химия";
            if (name.Contains("окр") || name.Contains("мир")) return "Окружающий мир";
            if (name.Contains("изо") || name.Contains("рис")) return "ИЗО";
            if (name.Contains("музык")) return "Музыка";
            if (name.Contains("труд") || name.Contains("технолог")) return "Технология";
            if (name.Contains("ров") || name.Contains("разговор") || name.Contains("россия")) return "Разговоры о важном";
            if (name == "ип" || name.Contains("проект")) return "Индивидуальный проект";
            if (name == "с/п" || name.Contains("самопод") || name.Contains("соц")) return "Самоподготовка";
            if (name.Contains("впр")) return "Подготовка к ВПР";
            if (name.Contains("огэ")) return "Подготовка к ОГЭ";
            if (name.Contains("однк")) return "ОДНКНР";

            return string.IsNullOrWhiteSpace(value) || value.Contains('?') ? "Предмет по расписанию" : value.Trim();
        }

        private static int PickTeacherIdForSubject(AppDbContext db, string subjectName, IEnumerable<string> classNames, int fallbackTeacherId)
        {
            var classList = classNames.ToList();
            if (classList.Any(IsOvzClass))
            {
                var className = classList.First(IsOvzClass);
                return className == "1А" ? 9 : className == "1Б" ? 11 : 12;
            }

            var normalized = subjectName.ToLowerInvariant();
            var preferred = normalized switch
            {
                var text when text.Contains("англий") => 78,
                var text when text.Contains("биолог") => 80,
                var text when text.Contains("географ") => 83,
                var text when text.Contains("истор") || text.Contains("общество") => 90,
                var text when text.Contains("литерат") => 95,
                var text when text.Contains("физика") => 73,
                var text when text.Contains("хим") => 118,
                var text when text.Contains("информ") => 88,
                var text when text.Contains("музык") => 99,
                var text when text.Contains("культура") => 115,
                var text when text.Contains("русский") => 109,
                var text when text.Contains("алгеб") || text.Contains("геометр") || text.Contains("математ") => 8,
                _ => fallbackTeacherId
            };

            if (db.Users.Any(user => user.Id == preferred))
                return preferred;

            return db.Users.Any(user => user.Id == fallbackTeacherId) ? fallbackTeacherId : db.Users.Select(user => user.Id).First();
        }

        private static (string Topic, string Homework) GetLessonSample(string subjectName, DateTime date, int seed)
        {
            var samples = new Dictionary<string, (string Topic, string Homework)[]>
            {
                ["Математика"] = new[] { ("Решение задач на порядок действий", "Стр. 42, N 5-8"), ("Умножение и деление", "Стр. 47, N 3-6"), ("Текстовые задачи", "Повторить алгоритм решения") },
                ["Алгебра"] = new[] { ("Линейные уравнения", "Параграф 12, N 84-88"), ("Функции и графики", "Построить графики в тетради"), ("Системы уравнений", "Параграф 14, N 101-104") },
                ["Геометрия"] = new[] { ("Признаки равенства треугольников", "Параграф 8, задачи 54-56"), ("Параллельные прямые", "Выучить теорему, N 72"), ("Площадь многоугольника", "Решить задачи 91-93") },
                ["Русский язык"] = new[] { ("Правописание безударных гласных", "Упр. 118, правило"), ("Сложное предложение", "Упр. 132"), ("Синтаксический разбор", "Разобрать 4 предложения") },
                ["Литература"] = new[] { ("Анализ художественного текста", "Прочитать главу и ответить на вопросы"), ("Образ героя в произведении", "Подготовить цитатный план"), ("Средства выразительности", "Найти 5 примеров в тексте") },
                ["Литературное чтение"] = new[] { ("Выразительное чтение рассказа", "Читать стр. 34-37"), ("Главная мысль текста", "Ответить на вопросы после текста"), ("План пересказа", "Подготовить пересказ") },
                ["Окружающий мир"] = new[] { ("Природные зоны России", "Заполнить таблицу в тетради"), ("Органы чувств человека", "Стр. 28-30, вопросы"), ("Правила безопасности", "Составить памятку") },
                ["Английский язык"] = new[] { ("Past Simple: правильные глаголы", "Workbook p. 24, ex. 2-4"), ("Topic: My school day", "Выучить слова"), ("Reading practice", "Прочитать текст и перевести") },
                ["Китайский язык"] = new[] { ("Базовые фразы приветствия", "Выучить 8 фраз"), ("Иероглифы по теме семья", "Прописать иероглифы"), ("Диалог в школе", "Подготовить короткий диалог") },
                ["Биология"] = new[] { ("Строение клетки", "Параграф 9, схема клетки"), ("Органы растений", "Параграф 11, вопросы 1-4"), ("Экосистемы", "Подготовить 3 примера") },
                ["География"] = new[] { ("Климатические пояса", "Параграф 18, контурная карта"), ("Рельеф России", "Отметить формы рельефа"), ("Внутренние воды", "Параграф 21") },
                ["История"] = new[] { ("Культура Древней Руси", "Параграф 15, даты"), ("Реформы Петра I", "Таблица реформ"), ("Россия в XIX веке", "Ответить на вопросы") },
                ["Обществознание"] = new[] { ("Права и обязанности гражданина", "Параграф 10, вопросы"), ("Семья и общество", "Мини-сочинение"), ("Государство и закон", "Повторить термины") },
                ["Физика"] = new[] { ("Сила и масса", "Параграф 19, задачи 3-5"), ("Давление твердых тел", "Параграф 21"), ("Работа и мощность", "Решить задачи") },
                ["Химия"] = new[] { ("Химические элементы", "Выучить символы 1-20"), ("Кислоты и основания", "Параграф 17"), ("Уравнения реакций", "Расставить коэффициенты") },
                ["Информатика"] = new[] { ("Алгоритмы и исполнители", "Составить блок-схему"), ("Табличные данные", "Практическая работа N 4"), ("Безопасность в интернете", "Подготовить памятку") },
                ["Физическая культура"] = new[] { ("Развитие выносливости", "Комплекс ОРУ"), ("Легкая атлетика", "Техника бега"), ("Командные игры", "Правила игры") },
                ["Музыка"] = new[] { ("Музыкальные жанры", "Выучить определения"), ("Народная песня", "Подготовить сообщение"), ("Ритм и мелодия", "Повторить термины") },
                ["ИЗО"] = new[] { ("Композиция в пейзаже", "Закончить эскиз"), ("Цветовой круг", "Подобрать палитру"), ("Натюрморт", "Принести простой предмет") },
                ["Технология"] = new[] { ("Проектирование изделия", "Подготовить эскиз"), ("Материалы и инструменты", "Повторить технику безопасности"), ("Практическая работа", "Завершить работу") },
                ["Разговоры о важном"] = new[] { ("Ценности школьного сообщества", "Подумать над примерами"), ("Командная работа", "Подготовить один вопрос"), ("Моя малая родина", "Мини-рассказ") },
                ["Индивидуальный проект"] = new[] { ("Выбор темы проекта", "Сформулировать цель"), ("План исследования", "Составить план"), ("Источники информации", "Найти 3 источника") },
                ["Самоподготовка"] = new[] { ("Закрепление материала дня", "Завершить классные задания"), ("Индивидуальная работа", "Повторить сложные задания"), ("Работа с учебными материалами", "Подготовить вопросы к урокам") },
                ["Социальная практика"] = new[] { ("Планирование полезного дела", "Описать цель и шаги"), ("Командное взаимодействие", "Подготовить итоги работы"), ("Рефлексия проекта", "Заполнить дневник практики") },
                ["ОРКСЭ"] = new[] { ("Культура общения", "Подготовить пример ситуации"), ("Семейные традиции", "Короткий рассказ"), ("Добро и ответственность", "Ответить на вопросы") },
                ["Подготовка к ВПР"] = new[] { ("Разбор типовых заданий", "Выполнить вариант 2"), ("Работа с ошибками", "Повторить сложные темы"), ("Тренировочная работа", "Закончить задания") },
                ["Подготовка к ОГЭ"] = new[] { ("Решение экзаменационных заданий", "Вариант 5"), ("Критерии оценивания", "Повторить памятку"), ("Практикум по части 2", "Дописать ответы") }
            };

            var set = samples.TryGetValue(subjectName, out var exact) ? exact : new[] { ("Работа по теме урока", "Повторить материал урока"), ("Закрепление изученного", "Выполнить задания в тетради"), ("Практическая работа", "Завершить начатое") };
            var item = set[StableHash(StableTextHash(subjectName), date.Year, date.Month, date.Day, seed) % set.Length];
            return item;
        }

        private static bool IsOvzClass(string className) =>
            string.Equals(className, "1А", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(className, "1Б", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(className, "2О", StringComparison.OrdinalIgnoreCase);

        private static int GetSchoolDayOfWeek(DateTime date)
        {
            var day = ((int)date.DayOfWeek + 6) % 7;
            return day is >= 0 and <= 4 ? day : -1;
        }

        private static bool ShouldCreateGrade(int studentId, int lessonId) =>
            StableHash(studentId, lessonId, 41) % 100 < 54;

        private static int GetGradeValue(int studentId, int lessonId)
        {
            var value = StableHash(studentId, lessonId, 79) % 100;
            return value < 14 ? 2 : value < 32 ? 3 : value < 72 ? 4 : 5;
        }

        private static byte GetAttendanceStatus(int studentId, int lessonId)
        {
            var value = StableHash(studentId, lessonId, 131) % 100;
            if (value < 86) return 1;
            if (value < 94) return 0;
            return 2;
        }

        private static int StableTextHash(string value)
        {
            unchecked
            {
                var hash = 17;
                foreach (var character in value)
                    hash = hash * 31 + character;
                return hash & int.MaxValue;
            }
        }

        private static int StableHash(params int[] values)
        {
            unchecked
            {
                var hash = 17;
                foreach (var value in values)
                    hash = hash * 31 + value;
                return hash & int.MaxValue;
            }
        }

        private static void ApplyMigrationsWithRetry(AppDbContext db, ILogger logger)
        {
            const int maxAttempts = 12;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    db.Database.Migrate();
                    return;
                }
                catch (Exception exception) when (attempt < maxAttempts)
                {
                    logger.LogWarning(exception, "База данных пока недоступна, повтор миграций {Attempt}/{MaxAttempts}", attempt, maxAttempts);
                    Thread.Sleep(TimeSpan.FromSeconds(5));
                }
            }

            db.Database.Migrate();
        }
    }
}
