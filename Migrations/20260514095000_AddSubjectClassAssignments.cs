using System;
using ClassBook.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassBook.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260514095000_AddSubjectClassAssignments")]
    public partial class AddSubjectClassAssignments : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SubjectClassAssignments",
                columns: table => new
                {
                    SubjectClassAssignmentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubjectId = table.Column<int>(type: "int", nullable: false),
                    ClassId = table.Column<int>(type: "int", nullable: false),
                    TeacherId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubjectClassAssignments", x => x.SubjectClassAssignmentId);
                    table.ForeignKey(
                        name: "FK_SubjectClassAssignments_Classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "ClassId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SubjectClassAssignments_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "SubjectId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SubjectClassAssignments_Users_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(@"
                INSERT INTO [SubjectClassAssignments] ([SubjectId], [ClassId], [TeacherId], [CreatedAt])
                SELECT DISTINCT [SubjectId], [ClassId], [TeacherId], SYSUTCDATETIME()
                FROM [Lessons]
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [SubjectClassAssignments] AS [sca]
                    WHERE [sca].[SubjectId] = [Lessons].[SubjectId]
                      AND [sca].[ClassId] = [Lessons].[ClassId]
                      AND [sca].[TeacherId] = [Lessons].[TeacherId]
                );
            ");

            migrationBuilder.CreateIndex(
                name: "IX_SubjectClassAssignments_ClassId",
                table: "SubjectClassAssignments",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_SubjectClassAssignments_SubjectId_ClassId_TeacherId",
                table: "SubjectClassAssignments",
                columns: new[] { "SubjectId", "ClassId", "TeacherId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubjectClassAssignments_TeacherId_ClassId",
                table: "SubjectClassAssignments",
                columns: new[] { "TeacherId", "ClassId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubjectClassAssignments");
        }
    }
}
