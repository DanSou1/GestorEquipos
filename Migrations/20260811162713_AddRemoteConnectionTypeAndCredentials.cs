using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorEquipos.Migrations
{
    /// <inheritdoc />
    public partial class AddRemoteConnectionTypeAndCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Port",
                table: "Remote",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "IPAddress",
                table: "Remote",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<string>(
                name: "AppDescription",
                table: "Remote",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            // Default to EscritorioRemotoWindows (1), not Aplicativo (0): any pre-existing
            // Remote row already has IPAddress/Port populated (RDP-shaped data), not an
            // AppDescription, so backfilling as Aplicativo would violate the CHECK
            // constraint added below.
            migrationBuilder.AddColumn<int>(
                name: "ConnectionType",
                table: "Remote",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "Password",
                table: "Remote",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "Remote",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            // Added WITH NOCHECK: pre-existing Remote rows (IPAddress/Port only, seeded
            // manually by SQL before this feature existed) have no Username/Password and
            // would fail the RDP branch of this constraint. NOCHECK enforces the rule for
            // all new inserts/updates going forward without retroactively validating (or
            // blocking the migration on) historical data.
            migrationBuilder.Sql(
                "ALTER TABLE [Remote] WITH NOCHECK ADD CONSTRAINT [CK_Remote_ConnectionTypeFields] CHECK (" +
                "(ConnectionType = 0 AND AppDescription IS NOT NULL) OR " +
                "(ConnectionType = 1 AND IPAddress IS NOT NULL AND Port IS NOT NULL AND Username IS NOT NULL AND Password IS NOT NULL))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Remote_ConnectionTypeFields",
                table: "Remote");

            migrationBuilder.DropColumn(
                name: "AppDescription",
                table: "Remote");

            migrationBuilder.DropColumn(
                name: "ConnectionType",
                table: "Remote");

            migrationBuilder.DropColumn(
                name: "Password",
                table: "Remote");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "Remote");

            migrationBuilder.AlterColumn<string>(
                name: "Port",
                table: "Remote",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "IPAddress",
                table: "Remote",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);
        }
    }
}
