using ClassBook.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassBook.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260521115500_CapitalizePrimarySubjectNames")]
    public partial class CapitalizePrimarySubjectNames : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE Subjects
                SET [Name] = N'Музыка'
                WHERE LOWER(LTRIM(RTRIM([Name]))) = N'музыка';

                UPDATE Subjects
                SET [Name] = N'Труд'
                WHERE LOWER(LTRIM(RTRIM([Name]))) = N'труд';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
