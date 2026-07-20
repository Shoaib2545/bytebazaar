using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByteBazaar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRedirects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Redirects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FromPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ToPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsPermanent = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Redirects", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Redirects_FromPath",
                table: "Redirects",
                column: "FromPath",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Redirects");
        }
    }
}
