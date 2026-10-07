using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorEquipos.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenanceCampaignItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MaintenanceCampaignItem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DesktopId = table.Column<int>(type: "int", nullable: false),
                    PeriodYear = table.Column<int>(type: "int", nullable: false),
                    PeriodMonth = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MaintenanceId = table.Column<int>(type: "int", nullable: true),
                    EmailSentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EmailSentToAddress = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SignedPdfFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    SignedPdfContent = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    SignedPdfUploadedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignedPdfUploadedByUserSystemId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceCampaignItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceCampaignItem_Desktop_DesktopId",
                        column: x => x.DesktopId,
                        principalTable: "Desktop",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceCampaignItem_Maintenance_MaintenanceId",
                        column: x => x.MaintenanceId,
                        principalTable: "Maintenance",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceCampaignItem_UserSystem_SignedPdfUploadedByUserSystemId",
                        column: x => x.SignedPdfUploadedByUserSystemId,
                        principalTable: "UserSystem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceCampaignItem_DesktopId_PeriodYear_PeriodMonth",
                table: "MaintenanceCampaignItem",
                columns: new[] { "DesktopId", "PeriodYear", "PeriodMonth" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceCampaignItem_MaintenanceId",
                table: "MaintenanceCampaignItem",
                column: "MaintenanceId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceCampaignItem_SignedPdfUploadedByUserSystemId",
                table: "MaintenanceCampaignItem",
                column: "SignedPdfUploadedByUserSystemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaintenanceCampaignItem");
        }
    }
}
