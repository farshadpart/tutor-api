using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorApi.Migrations
{
    /// <inheritdoc />
    public partial class UpdateColumnNamesInRefreshTokenTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RevokedUtc",
                table: "RefreshTokens",
                newName: "RevokedAt");

            migrationBuilder.RenameColumn(
                name: "ExpiresUtc",
                table: "RefreshTokens",
                newName: "ExpiresAt");

            migrationBuilder.RenameColumn(
                name: "CreatedUtc",
                table: "RefreshTokens",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_RefreshTokens_UserId_ExpiresUtc",
                table: "RefreshTokens",
                newName: "IX_RefreshTokens_UserId_ExpiresAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RevokedAt",
                table: "RefreshTokens",
                newName: "RevokedUtc");

            migrationBuilder.RenameColumn(
                name: "ExpiresAt",
                table: "RefreshTokens",
                newName: "ExpiresUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "RefreshTokens",
                newName: "CreatedUtc");

            migrationBuilder.RenameIndex(
                name: "IX_RefreshTokens_UserId_ExpiresAt",
                table: "RefreshTokens",
                newName: "IX_RefreshTokens_UserId_ExpiresUtc");
        }
    }
}
