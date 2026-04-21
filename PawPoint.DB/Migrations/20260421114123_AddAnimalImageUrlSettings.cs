using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PawPoint.DB.Migrations
{
    /// <inheritdoc />
    public partial class AddAnimalImageUrlSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ImagePositionY",
                table: "Animals",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImagePositionY",
                table: "Animals");
        }
    }
}
