using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PawPoint.DB.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrencyToUserSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "UserSettings",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Currency",
                table: "UserSettings");
        }
    }
}
