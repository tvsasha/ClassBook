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
                "Server=(localdb)\\MSSQLLocalDB;Database=ClassBookDb;Trusted_Connection=True;MultipleActiveResultSets=true;Encrypt=False;"
            );

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
