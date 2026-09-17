using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorEquipos.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexOnDesktopSerialNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Desktop_SerialNumber",
                table: "Desktop",
                column: "SerialNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Desktop_SerialNumber",
                table: "Desktop");
        }
    }
}
