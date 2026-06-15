using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassBook.Migrations
{
    /// <inheritdoc />
    public partial class AddReusableQrLogin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "QrLoginIssuedAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QrLoginTokenHash",
                table: "Users",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_QrLoginTokenHash",
                table: "Users",
                column: "QrLoginTokenHash",
                unique: true,
                filter: "[QrLoginTokenHash] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_QrLoginTokenHash",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "QrLoginIssuedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "QrLoginTokenHash",
                table: "Users");
        }
    }
}
