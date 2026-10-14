using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTripCoordinatesAndSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "DestinationLatitude",
                table: "Trips",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DestinationLongitude",
                table: "Trips",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Trips",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "OriginLatitude",
                table: "Trips",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "OriginLongitude",
                table: "Trips",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DestinationLatitude",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "DestinationLongitude",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "OriginLatitude",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "OriginLongitude",
                table: "Trips");
        }
    }
}
