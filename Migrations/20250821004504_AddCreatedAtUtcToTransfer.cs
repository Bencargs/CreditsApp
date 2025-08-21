using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreditsApp.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatedAtUtcToTransfer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Transfers",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Transfers");
        }
    }
}
