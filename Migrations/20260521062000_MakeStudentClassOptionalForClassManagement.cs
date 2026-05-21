using ClassBook.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassBook.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260521062000_MakeStudentClassOptionalForClassManagement")]
    public partial class MakeStudentClassOptionalForClassManagement : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Students_Classes_ClassId",
                table: "Students");

            migrationBuilder.AlterColumn<int>(
                name: "ClassId",
                table: "Students",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_Students_Classes_ClassId",
                table: "Students",
                column: "ClassId",
                principalTable: "Classes",
                principalColumn: "ClassId",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Students_Classes_ClassId",
                table: "Students");

            migrationBuilder.Sql(@"
                DECLARE @FallbackClassId int;

                SELECT TOP(1) @FallbackClassId = [ClassId]
                FROM [Classes]
                ORDER BY [ClassId];

                IF @FallbackClassId IS NULL
                BEGIN
                    INSERT INTO [Classes] ([Name]) VALUES (N'Без класса');
                    SET @FallbackClassId = SCOPE_IDENTITY();
                END

                UPDATE [Students]
                SET [ClassId] = @FallbackClassId
                WHERE [ClassId] IS NULL;
            ");

            migrationBuilder.AlterColumn<int>(
                name: "ClassId",
                table: "Students",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Students_Classes_ClassId",
                table: "Students",
                column: "ClassId",
                principalTable: "Classes",
                principalColumn: "ClassId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
