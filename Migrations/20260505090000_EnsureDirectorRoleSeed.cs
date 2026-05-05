using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ClassBook.Infrastructure.Data;

#nullable disable

namespace ClassBook.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260505090000_EnsureDirectorRoleSeed")]
    public partial class EnsureDirectorRoleSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [Roles] WHERE [Id] = 5)
BEGIN
    SET IDENTITY_INSERT [Roles] ON;
    INSERT INTO [Roles] ([Id], [Name]) VALUES (5, N'Менеджер расписания');
    SET IDENTITY_INSERT [Roles] OFF;
END

IF NOT EXISTS (SELECT 1 FROM [Roles] WHERE [Id] = 6)
BEGIN
    SET IDENTITY_INSERT [Roles] ON;
    INSERT INTO [Roles] ([Id], [Name]) VALUES (6, N'Директор');
    SET IDENTITY_INSERT [Roles] OFF;
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 6);
        }
    }
}
