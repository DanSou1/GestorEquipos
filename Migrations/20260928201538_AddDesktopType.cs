using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorEquipos.Migrations
{
    /// <inheritdoc />
    public partial class AddDesktopType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DesktopType",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DesktopType", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DesktopType_Name",
                table: "DesktopType",
                column: "Name",
                unique: true);

            // Seed a default type so existing Desktop rows have a valid value to backfill to —
            // DesktopTypeId becomes NOT NULL below and admins can rename/add types afterward.
            migrationBuilder.Sql("INSERT INTO [DesktopType] ([Name]) VALUES (N'Escritorio');");

            migrationBuilder.AddColumn<int>(
                name: "DesktopTypeId",
                table: "Desktop",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("UPDATE [Desktop] SET [DesktopTypeId] = (SELECT TOP 1 [Id] FROM [DesktopType] WHERE [Name] = N'Escritorio') WHERE [DesktopTypeId] IS NULL;");

            migrationBuilder.AlterColumn<int>(
                name: "DesktopTypeId",
                table: "Desktop",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Desktop_DesktopTypeId",
                table: "Desktop",
                column: "DesktopTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Desktop_DesktopType_DesktopTypeId",
                table: "Desktop",
                column: "DesktopTypeId",
                principalTable: "DesktopType",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Desktop_DesktopType_DesktopTypeId",
                table: "Desktop");

            migrationBuilder.DropTable(
                name: "DesktopType");

            migrationBuilder.DropIndex(
                name: "IX_Desktop_DesktopTypeId",
                table: "Desktop");

            migrationBuilder.DropColumn(
                name: "DesktopTypeId",
                table: "Desktop");
        }
    }
}
