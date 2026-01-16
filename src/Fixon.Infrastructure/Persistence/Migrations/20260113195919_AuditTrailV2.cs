using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fixon.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditTrailV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_audit_logs_users_UserId",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_CompanyId_UserId_CreatedAt",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_UserId",
                table: "audit_logs");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "audit_logs",
                newName: "ActorUserId");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "audit_logs",
                newName: "Timestamp");

            migrationBuilder.RenameColumn(
                name: "ChangesJson",
                table: "audit_logs",
                newName: "DetailsJson");

            migrationBuilder.RenameColumn(
                name: "ActionType",
                table: "audit_logs",
                newName: "Action");

            migrationBuilder.RenameIndex(
                name: "IX_audit_logs_CompanyId_CreatedAt",
                table: "audit_logs",
                newName: "IX_audit_logs_CompanyId_Timestamp");

            migrationBuilder.AddColumn<string>(
                name: "ActorSystem",
                table: "audit_logs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ActorType",
                table: "audit_logs",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "audit_logs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PreviousAuditLogId",
                table: "audit_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_ActorUserId",
                table: "audit_logs",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_CompanyId_ActorUserId_Timestamp",
                table: "audit_logs",
                columns: new[] { "CompanyId", "ActorUserId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_CompanyId_CorrelationId",
                table: "audit_logs",
                columns: new[] { "CompanyId", "CorrelationId" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_PreviousAuditLogId",
                table: "audit_logs",
                column: "PreviousAuditLogId");

            migrationBuilder.AddForeignKey(
                name: "FK_audit_logs_audit_logs_PreviousAuditLogId",
                table: "audit_logs",
                column: "PreviousAuditLogId",
                principalTable: "audit_logs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_audit_logs_users_ActorUserId",
                table: "audit_logs",
                column: "ActorUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_audit_logs_audit_logs_PreviousAuditLogId",
                table: "audit_logs");

            migrationBuilder.DropForeignKey(
                name: "FK_audit_logs_users_ActorUserId",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_ActorUserId",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_CompanyId_ActorUserId_Timestamp",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_CompanyId_CorrelationId",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_PreviousAuditLogId",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "ActorSystem",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "ActorType",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "PreviousAuditLogId",
                table: "audit_logs");

            migrationBuilder.RenameColumn(
                name: "Timestamp",
                table: "audit_logs",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "DetailsJson",
                table: "audit_logs",
                newName: "ChangesJson");

            migrationBuilder.RenameColumn(
                name: "Action",
                table: "audit_logs",
                newName: "ActionType");

            migrationBuilder.RenameIndex(
                name: "IX_audit_logs_CompanyId_Timestamp",
                table: "audit_logs",
                newName: "IX_audit_logs_CompanyId_CreatedAt");

            migrationBuilder.RenameColumn(
                name: "ActorUserId",
                table: "audit_logs",
                newName: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_CompanyId_UserId_CreatedAt",
                table: "audit_logs",
                columns: new[] { "CompanyId", "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_UserId",
                table: "audit_logs",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_audit_logs_users_UserId",
                table: "audit_logs",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
