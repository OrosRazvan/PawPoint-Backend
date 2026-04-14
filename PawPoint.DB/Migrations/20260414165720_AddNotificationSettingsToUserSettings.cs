using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PawPoint.DB.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationSettingsToUserSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AppointmentNotifications",
                table: "UserSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "DewormingNotifications",
                table: "UserSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableNotifications",
                table: "UserSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NotificationBadgeMode",
                table: "UserSettings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "VaccinationNotifications",
                table: "UserSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppointmentNotifications",
                table: "UserSettings");

            migrationBuilder.DropColumn(
                name: "DewormingNotifications",
                table: "UserSettings");

            migrationBuilder.DropColumn(
                name: "EnableNotifications",
                table: "UserSettings");

            migrationBuilder.DropColumn(
                name: "NotificationBadgeMode",
                table: "UserSettings");

            migrationBuilder.DropColumn(
                name: "VaccinationNotifications",
                table: "UserSettings");
        }
    }
}
