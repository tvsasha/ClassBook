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
                "Data Source=DESKTOP-B7HA073;Initial Catalog=ClassBookDb;Integrated Security=True;Encrypt=False;"
            );

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
