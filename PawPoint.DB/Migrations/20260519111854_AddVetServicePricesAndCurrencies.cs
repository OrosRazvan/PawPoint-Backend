using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PawPoint.DB.Migrations
{
    /// <inheritdoc />
    public partial class AddVetServicePricesAndCurrencies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BasePriceRon",
                table: "VetCabinets");

            migrationBuilder.DropColumn(
                name: "VaccineName",
                table: "Vaccinations");

            migrationBuilder.RenameColumn(
                name: "IntervalDays",
                table: "Dewormings",
                newName: "Currency");

            migrationBuilder.RenameColumn(
                name: "EstimatedPriceRon",
                table: "Appointments",
                newName: "Price");

            migrationBuilder.AddColumn<int>(
                name: "Currency",
                table: "Vaccinations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "Vaccinations",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VaccineType",
                table: "Vaccinations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "Dewormings",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Currency",
                table: "Appointments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "VetServicePrices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VetCabinetId = table.Column<int>(type: "integer", nullable: false),
                    ServiceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DewormingType = table.Column<int>(type: "integer", nullable: true),
                    VaccineType = table.Column<int>(type: "integer", nullable: true),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VetServicePrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VetServicePrices_VetCabinets_VetCabinetId",
                        column: x => x.VetCabinetId,
                        principalTable: "VetCabinets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VetServicePrices_VetCabinetId_ServiceType_DewormingType_Vac~",
                table: "VetServicePrices",
                columns: new[] { "VetCabinetId", "ServiceType", "DewormingType", "VaccineType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VetServicePrices");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Vaccinations");

            migrationBuilder.DropColumn(
                name: "Price",
                table: "Vaccinations");

            migrationBuilder.DropColumn(
                name: "VaccineType",
                table: "Vaccinations");

            migrationBuilder.DropColumn(
                name: "Price",
                table: "Dewormings");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Appointments");

            migrationBuilder.RenameColumn(
                name: "Currency",
                table: "Dewormings",
                newName: "IntervalDays");

            migrationBuilder.RenameColumn(
                name: "Price",
                table: "Appointments",
                newName: "EstimatedPriceRon");

            migrationBuilder.AddColumn<decimal>(
                name: "BasePriceRon",
                table: "VetCabinets",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "VaccineName",
                table: "Vaccinations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }
    }
}
