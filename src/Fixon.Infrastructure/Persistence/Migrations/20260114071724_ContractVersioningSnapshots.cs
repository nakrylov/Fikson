using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fixon.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ContractVersioningSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CurrentVersionId",
                table: "contracts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "contracts",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "PenaltyRulesSnapshotJson",
                table: "contract_versions",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SignedAt",
                table: "contract_versions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SlaRulesSnapshotJson",
                table: "contract_versions",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "contract_versions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_contracts_CompanyId_Status",
                table: "contracts",
                columns: new[] { "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_contract_versions_CompanyId_ContractId_Status",
                table: "contract_versions",
                columns: new[] { "CompanyId", "ContractId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_contracts_CompanyId_Status",
                table: "contracts");

            migrationBuilder.DropIndex(
                name: "IX_contract_versions_CompanyId_ContractId_Status",
                table: "contract_versions");

            migrationBuilder.DropColumn(
                name: "CurrentVersionId",
                table: "contracts");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "contracts");

            migrationBuilder.DropColumn(
                name: "PenaltyRulesSnapshotJson",
                table: "contract_versions");

            migrationBuilder.DropColumn(
                name: "SignedAt",
                table: "contract_versions");

            migrationBuilder.DropColumn(
                name: "SlaRulesSnapshotJson",
                table: "contract_versions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "contract_versions");
        }
    }
}
