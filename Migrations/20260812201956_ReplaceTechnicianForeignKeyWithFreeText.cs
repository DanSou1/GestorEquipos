using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorEquipos.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceTechnicianForeignKeyWithFreeText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: add the new column nullable — backfilled below before being made required.
            migrationBuilder.AddColumn<string>(
                name: "TechnicianName",
                table: "Maintenance",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicianName",
                table: "PeripheralMaintenance",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            // Step 2: backfill from the existing FK join, preserving any historical technician
            // names as text before the FK/UserSystem link is dropped.
            migrationBuilder.Sql(@"
UPDATE m
SET m.[TechnicianName] = COALESCE(u.[Name] + N' ' + u.[LastName], N'Técnico no registrado')
FROM [Maintenance] m
LEFT JOIN [UserSystem] us ON us.[Id] = m.[TechnicianUserSystemId]
LEFT JOIN [Users] u ON u.[Id] = us.[UserId];");

            migrationBuilder.Sql(@"
UPDATE m
SET m.[TechnicianName] = COALESCE(u.[Name] + N' ' + u.[LastName], N'Técnico no registrado')
FROM [PeripheralMaintenance] m
LEFT JOIN [UserSystem] us ON us.[Id] = m.[TechnicianUserSystemId]
LEFT JOIN [Users] u ON u.[Id] = us.[UserId];");

            // Step 3: enforce NOT NULL now that every row has a value.
            migrationBuilder.AlterColumn<string>(
                name: "TechnicianName",
                table: "Maintenance",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TechnicianName",
                table: "PeripheralMaintenance",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldNullable: true);

            // Step 4: drop the old FK-backed column, its FK, and its index — clean removal, no
            // dual-column compatibility shim.
            migrationBuilder.DropForeignKey(
                name: "FK_Maintenance_UserSystem_TechnicianUserSystemId",
                table: "Maintenance");

            migrationBuilder.DropForeignKey(
                name: "FK_PeripheralMaintenance_UserSystem_TechnicianUserSystemId",
                table: "PeripheralMaintenance");

            migrationBuilder.DropIndex(
                name: "IX_PeripheralMaintenance_TechnicianUserSystemId",
                table: "PeripheralMaintenance");

            migrationBuilder.DropIndex(
                name: "IX_Maintenance_TechnicianUserSystemId",
                table: "Maintenance");

            migrationBuilder.DropColumn(
                name: "TechnicianUserSystemId",
                table: "PeripheralMaintenance");

            migrationBuilder.DropColumn(
                name: "TechnicianUserSystemId",
                table: "Maintenance");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Best-effort rollback: recreates the FK-backed column shape, but the free-text
            // TechnicianName values do not round-trip back to a UserSystem reference — there is
            // no reliable way to re-derive who the FK should point to from a name string alone.
            migrationBuilder.DropColumn(
                name: "TechnicianName",
                table: "PeripheralMaintenance");

            migrationBuilder.DropColumn(
                name: "TechnicianName",
                table: "Maintenance");

            migrationBuilder.AddColumn<int>(
                name: "TechnicianUserSystemId",
                table: "PeripheralMaintenance",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TechnicianUserSystemId",
                table: "Maintenance",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_PeripheralMaintenance_TechnicianUserSystemId",
                table: "PeripheralMaintenance",
                column: "TechnicianUserSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_Maintenance_TechnicianUserSystemId",
                table: "Maintenance",
                column: "TechnicianUserSystemId");

            migrationBuilder.AddForeignKey(
                name: "FK_Maintenance_UserSystem_TechnicianUserSystemId",
                table: "Maintenance",
                column: "TechnicianUserSystemId",
                principalTable: "UserSystem",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PeripheralMaintenance_UserSystem_TechnicianUserSystemId",
                table: "PeripheralMaintenance",
                column: "TechnicianUserSystemId",
                principalTable: "UserSystem",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
