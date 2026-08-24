using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorEquipos.Migrations
{
    /// <inheritdoc />
    public partial class MakePeripheralDesktopOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Peripheral_Desktop_DesktopId",
                table: "Peripheral");

            migrationBuilder.AlterColumn<int>(
                name: "DesktopId",
                table: "Peripheral",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_Peripheral_Desktop_DesktopId",
                table: "Peripheral",
                column: "DesktopId",
                principalTable: "Desktop",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Peripheral_Desktop_DesktopId",
                table: "Peripheral");

            migrationBuilder.AlterColumn<int>(
                name: "DesktopId",
                table: "Peripheral",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Peripheral_Desktop_DesktopId",
                table: "Peripheral",
                column: "DesktopId",
                principalTable: "Desktop",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
