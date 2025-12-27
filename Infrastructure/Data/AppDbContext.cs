using ClassBook.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClassBook.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Lesson> Lessons => Set<Lesson>();
        public DbSet<Grade> Grades => Set<Grade>();
        public DbSet<Attendance> Attendances => Set<Attendance>();
        public DbSet<Subject> Subjects => Set<Subject>();
        public DbSet<Student> Students { get; set; } = null!;
        public DbSet<Class> Classes { get; set; } = null!;
        public DbSet<User> Users => Set<User>();
        public DbSet<Role> Roles => Set<Role>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 🔹 Roles (оставляем как есть, включая Student и Parent на будущее)
            modelBuilder.Entity<Role>().HasData(
                new Role { Id = 1, Name = "Администратор" },
                new Role { Id = 2, Name = "Учитель" },
                new Role { Id = 3, Name = "Ученик" },
                new Role { Id = 4, Name = "Родитель" }
            );

            // 🔹 User (учителя и админы)
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(u => u.Id);
                entity.Property(u => u.Login).IsRequired().HasMaxLength(100);
                entity.Property(u => u.PasswordHash).IsRequired();
                entity.Property(u => u.FullName).IsRequired().HasMaxLength(150);
                entity.Property(u => u.IsActive).IsRequired();
                entity.Property(u => u.CreatedAt).IsRequired();

                entity.HasOne(u => u.Role)
                      .WithMany(r => r.Users)
                      .HasForeignKey(u => u.RoleId)
                      .OnDelete(DeleteBehavior.Restrict);

                // Навигации для учителей
                entity.HasMany(u => u.Subjects)
                      .WithOne(s => s.Teacher)
                      .HasForeignKey(s => s.TeacherId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(u => u.Lessons)
                      .WithOne(l => l.Teacher)
                      .HasForeignKey(l => l.TeacherId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // 🔹 Classes
            modelBuilder.Entity<Class>(entity =>
            {
                entity.HasKey(c => c.ClassId);
                entity.Property(c => c.Name).IsRequired().HasMaxLength(50);
                entity.HasMany(c => c.Students).WithOne(s => s.Class)
                      .HasForeignKey(s => s.ClassId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasMany(c => c.Lessons).WithOne(l => l.Class)
                      .HasForeignKey(l => l.ClassId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // 🔹 Students
            modelBuilder.Entity<Student>(entity =>
            {
                entity.HasKey(s => s.StudentId);
                entity.Property(s => s.FirstName).IsRequired().HasMaxLength(50);
                entity.Property(s => s.LastName).IsRequired().HasMaxLength(50);
                entity.HasMany(s => s.Grades).WithOne(g => g.Student)
                      .HasForeignKey(g => g.StudentId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasMany(s => s.Attendances).WithOne(a => a.Student)
                      .HasForeignKey(a => a.StudentId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // 🔹 Subjects (TeacherId → User.Id)
            modelBuilder.Entity<Subject>(entity =>
            {
                entity.HasKey(s => s.SubjectId);
                entity.Property(s => s.Name).IsRequired().HasMaxLength(50);
                entity.HasOne(s => s.Teacher)
                      .WithMany(u => u.Subjects)
                      .HasForeignKey(s => s.TeacherId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(s => s.Lessons).WithOne(l => l.Subject)
                      .HasForeignKey(l => l.SubjectId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // 🔹 Lessons (TeacherId → User.Id)
            modelBuilder.Entity<Lesson>(entity =>
            {
                entity.HasKey(l => l.LessonId);
                entity.Property(l => l.Topic).IsRequired().HasMaxLength(100);
                entity.Property(l => l.Date).IsRequired();

                entity.HasOne(l => l.Subject)
                      .WithMany(s => s.Lessons)
                      .HasForeignKey(l => l.SubjectId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(l => l.Class)
                      .WithMany(c => c.Lessons)
                      .HasForeignKey(l => l.ClassId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(l => l.Teacher)
                      .WithMany(u => u.Lessons)
                      .HasForeignKey(l => l.TeacherId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(l => l.Grades)
                      .WithOne(g => g.Lesson)
                      .HasForeignKey(g => g.LessonId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(l => l.Attendances)
                      .WithOne(a => a.Lesson)
                      .HasForeignKey(a => a.LessonId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // 🔹 Grades
            modelBuilder.Entity<Grade>(entity =>
            {
                entity.HasKey(g => g.GradeId);
                entity.HasOne(g => g.Student)
                      .WithMany(s => s.Grades)
                      .HasForeignKey(g => g.StudentId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(g => g.Lesson)
                      .WithMany(l => l.Grades)
                      .HasForeignKey(g => g.LessonId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // 🔹 Attendance
            modelBuilder.Entity<Attendance>(entity =>
            {
                entity.HasKey(a => a.AttendanceId);
                entity.HasOne(a => a.Student)
                      .WithMany(s => s.Attendances)
                      .HasForeignKey(a => a.StudentId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(a => a.Lesson)
                      .WithMany(l => l.Attendances)
                      .HasForeignKey(a => a.LessonId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(a => new { a.StudentId, a.LessonId })
                      .IsUnique();
            });
        }
    }
}