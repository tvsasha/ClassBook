using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ClassBook.Infrastructure.Data
{
    public class AppDbContextFactory
        : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

            optionsBuilder.UseSqlServer(
                "Server=DESKTOP-GPL6FO9\\SQLEXPRESS;Database=ClassBookDb;Trusted_Connection=True;TrustServerCertificate=True"
            );

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
