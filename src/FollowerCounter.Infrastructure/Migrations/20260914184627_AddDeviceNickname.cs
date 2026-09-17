using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FollowerCounter.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceNickname : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Nickname",
                table: "Devices",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DigitCount",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "Nickname",
                table: "Devices");
        }
    }
}
