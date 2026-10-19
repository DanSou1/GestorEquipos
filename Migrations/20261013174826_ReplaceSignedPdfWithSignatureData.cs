using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorEquipos.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceSignedPdfWithSignatureData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceCampaignItem_UserSystem_SignedPdfUploadedByUserSystemId",
                table: "MaintenanceCampaignItem");

            migrationBuilder.DropColumn(
                name: "SignedPdfContent",
                table: "MaintenanceCampaignItem");

            migrationBuilder.DropColumn(
                name: "SignedPdfFileName",
                table: "MaintenanceCampaignItem");

            migrationBuilder.RenameColumn(
                name: "SignedPdfUploadedByUserSystemId",
                table: "MaintenanceCampaignItem",
                newName: "SignerUserSystemId");

            migrationBuilder.RenameColumn(
                name: "SignedPdfUploadedAtUtc",
                table: "MaintenanceCampaignItem",
                newName: "SignedAtUtc");

            migrationBuilder.RenameIndex(
                name: "IX_MaintenanceCampaignItem_SignedPdfUploadedByUserSystemId",
                table: "MaintenanceCampaignItem",
                newName: "IX_MaintenanceCampaignItem_SignerUserSystemId");

            migrationBuilder.AddColumn<int>(
                name: "AssignedSignerUsersId",
                table: "MaintenanceCampaignItem",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignatureData",
                table: "MaintenanceCampaignItem",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SignatureMethod",
                table: "MaintenanceCampaignItem",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserSavedSignature",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    SignatureData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SavedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSavedSignature", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSavedSignature_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Backfill best-effort para ítems que ya habían llegado a CorreoEnviado/Realizado
            // antes de esta migración (el esquema viejo no guardaba el Users.Id del firmante,
            // solo el correo al que se envió) — por email exacto contra Users.Email. Los ítems
            // que no encuentren coincidencia (o sigan Pendiente) quedan en NULL: no podrán
            // firmarse/regenerarse hasta que alguien los complete de nuevo bajo el flujo actual.
            migrationBuilder.Sql(@"
                UPDATE mci
                SET mci.AssignedSignerUsersId = u.Id
                FROM [MaintenanceCampaignItem] mci
                INNER JOIN [Users] u ON u.Email = mci.EmailSentToAddress
                WHERE mci.EmailSentToAddress IS NOT NULL;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceCampaignItem_AssignedSignerUsersId",
                table: "MaintenanceCampaignItem",
                column: "AssignedSignerUsersId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSavedSignature_UserId",
                table: "UserSavedSignature",
                column: "UserId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceCampaignItem_UserSystem_SignerUserSystemId",
                table: "MaintenanceCampaignItem",
                column: "SignerUserSystemId",
                principalTable: "UserSystem",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceCampaignItem_Users_AssignedSignerUsersId",
                table: "MaintenanceCampaignItem",
                column: "AssignedSignerUsersId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceCampaignItem_UserSystem_SignerUserSystemId",
                table: "MaintenanceCampaignItem");

            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceCampaignItem_Users_AssignedSignerUsersId",
                table: "MaintenanceCampaignItem");

            migrationBuilder.DropTable(
                name: "UserSavedSignature");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceCampaignItem_AssignedSignerUsersId",
                table: "MaintenanceCampaignItem");

            migrationBuilder.DropColumn(
                name: "AssignedSignerUsersId",
                table: "MaintenanceCampaignItem");

            migrationBuilder.DropColumn(
                name: "SignatureData",
                table: "MaintenanceCampaignItem");

            migrationBuilder.DropColumn(
                name: "SignatureMethod",
                table: "MaintenanceCampaignItem");

            migrationBuilder.RenameColumn(
                name: "SignerUserSystemId",
                table: "MaintenanceCampaignItem",
                newName: "SignedPdfUploadedByUserSystemId");

            migrationBuilder.RenameColumn(
                name: "SignedAtUtc",
                table: "MaintenanceCampaignItem",
                newName: "SignedPdfUploadedAtUtc");

            migrationBuilder.RenameIndex(
                name: "IX_MaintenanceCampaignItem_SignerUserSystemId",
                table: "MaintenanceCampaignItem",
                newName: "IX_MaintenanceCampaignItem_SignedPdfUploadedByUserSystemId");

            migrationBuilder.AddColumn<byte[]>(
                name: "SignedPdfContent",
                table: "MaintenanceCampaignItem",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignedPdfFileName",
                table: "MaintenanceCampaignItem",
                type: "nvarchar(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceCampaignItem_UserSystem_SignedPdfUploadedByUserSystemId",
                table: "MaintenanceCampaignItem",
                column: "SignedPdfUploadedByUserSystemId",
                principalTable: "UserSystem",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
