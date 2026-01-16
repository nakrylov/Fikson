using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fixon.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClaimsDisputesLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_claims_sla_evaluations_SlaEvaluationId",
                table: "claims");

            migrationBuilder.DropCheckConstraint(
                name: "ck_claims_status",
                table: "claims");

            migrationBuilder.RenameColumn(
                name: "SlaEvaluationId",
                table: "claims",
                newName: "SlaViolationId");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "claims",
                newName: "OpenedAt");

            migrationBuilder.RenameIndex(
                name: "IX_claims_SlaEvaluationId",
                table: "claims",
                newName: "IX_claims_SlaViolationId");

            migrationBuilder.RenameIndex(
                name: "IX_claims_CompanyId_SlaEvaluationId",
                table: "claims",
                newName: "IX_claims_CompanyId_SlaViolationId");

            migrationBuilder.RenameIndex(
                name: "IX_claims_CompanyId_CreatedAt",
                table: "claims",
                newName: "IX_claims_CompanyId_OpenedAt");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClosedAt",
                table: "claims",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ContractId",
                table: "claims",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "claim_decisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: false),
                    DecisionType = table.Column<int>(type: "integer", nullable: false),
                    DecisionAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    DecidedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_claim_decisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_claim_decisions_claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_claim_decisions_users_DecidedByUserId",
                        column: x => x.DecidedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "disputes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: false),
                    InitiatedBy = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OpenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_disputes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_disputes_claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sla_violations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SlaRuleVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SlaEvaluationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CalculatedValuesJson = table.Column<string>(type: "jsonb", nullable: false),
                    DetectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sla_violations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sla_violations_contract_versions_ContractVersionId",
                        column: x => x.ContractVersionId,
                        principalTable: "contract_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sla_violations_sla_evaluations_SlaEvaluationId",
                        column: x => x.SlaEvaluationId,
                        principalTable: "sla_evaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sla_violations_sla_rule_versions_SlaRuleVersionId",
                        column: x => x.SlaRuleVersionId,
                        principalTable: "sla_rule_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Backfill: ContractId from ContractVersion (existing rows)
            migrationBuilder.Sql("""
                update claims c
                set "ContractId" = cv."ContractId"
                from contract_versions cv
                where c."ContractVersionId" = cv."Id";
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "ContractId",
                table: "claims",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            // Backfill: create SLA violations for existing claims by reusing old evaluation id (renamed to SlaViolationId)
            migrationBuilder.Sql("""
                insert into sla_violations ("Id","CompanyId","ContractVersionId","SlaRuleVersionId","SlaEvaluationId","CalculatedValuesJson","DetectedAt")
                select
                    c."SlaViolationId" as "Id",
                    c."CompanyId",
                    c."ContractVersionId",
                    c."SlaRuleVersionId",
                    c."SlaViolationId" as "SlaEvaluationId",
                    e."CalculatedValuesJson",
                    e."EvaluatedAt" as "DetectedAt"
                from claims c
                join sla_evaluations e on e."Id" = c."SlaViolationId"
                on conflict ("Id") do nothing;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_claims_CompanyId_ContractId",
                table: "claims",
                columns: new[] { "CompanyId", "ContractId" });

            migrationBuilder.CreateIndex(
                name: "IX_claims_ContractId",
                table: "claims",
                column: "ContractId");

            migrationBuilder.AddCheckConstraint(
                name: "ck_claims_status",
                table: "claims",
                sql: "\"Status\" in (1,2,3,4,5,6,7,8)");

            migrationBuilder.CreateIndex(
                name: "IX_claim_decisions_ClaimId",
                table: "claim_decisions",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_claim_decisions_CompanyId",
                table: "claim_decisions",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_claim_decisions_CompanyId_ClaimId_DecidedAt",
                table: "claim_decisions",
                columns: new[] { "CompanyId", "ClaimId", "DecidedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_claim_decisions_CompanyId_DecidedByUserId_DecidedAt",
                table: "claim_decisions",
                columns: new[] { "CompanyId", "DecidedByUserId", "DecidedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_claim_decisions_DecidedByUserId",
                table: "claim_decisions",
                column: "DecidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_disputes_ClaimId",
                table: "disputes",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_disputes_CompanyId",
                table: "disputes",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_disputes_CompanyId_ClaimId",
                table: "disputes",
                columns: new[] { "CompanyId", "ClaimId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_disputes_CompanyId_Status_OpenedAt",
                table: "disputes",
                columns: new[] { "CompanyId", "Status", "OpenedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_sla_violations_CompanyId",
                table: "sla_violations",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_sla_violations_CompanyId_ContractVersionId",
                table: "sla_violations",
                columns: new[] { "CompanyId", "ContractVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_sla_violations_CompanyId_DetectedAt",
                table: "sla_violations",
                columns: new[] { "CompanyId", "DetectedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_sla_violations_CompanyId_SlaEvaluationId",
                table: "sla_violations",
                columns: new[] { "CompanyId", "SlaEvaluationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sla_violations_CompanyId_SlaRuleVersionId",
                table: "sla_violations",
                columns: new[] { "CompanyId", "SlaRuleVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_sla_violations_ContractVersionId",
                table: "sla_violations",
                column: "ContractVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_sla_violations_SlaEvaluationId",
                table: "sla_violations",
                column: "SlaEvaluationId");

            migrationBuilder.CreateIndex(
                name: "IX_sla_violations_SlaRuleVersionId",
                table: "sla_violations",
                column: "SlaRuleVersionId");

            migrationBuilder.AddForeignKey(
                name: "FK_claims_contracts_ContractId",
                table: "claims",
                column: "ContractId",
                principalTable: "contracts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_claims_sla_violations_SlaViolationId",
                table: "claims",
                column: "SlaViolationId",
                principalTable: "sla_violations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_claims_contracts_ContractId",
                table: "claims");

            migrationBuilder.DropForeignKey(
                name: "FK_claims_sla_violations_SlaViolationId",
                table: "claims");

            migrationBuilder.DropTable(
                name: "claim_decisions");

            migrationBuilder.DropTable(
                name: "disputes");

            migrationBuilder.DropTable(
                name: "sla_violations");

            migrationBuilder.DropIndex(
                name: "IX_claims_CompanyId_ContractId",
                table: "claims");

            migrationBuilder.DropIndex(
                name: "IX_claims_ContractId",
                table: "claims");

            migrationBuilder.DropCheckConstraint(
                name: "ck_claims_status",
                table: "claims");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "claims");

            migrationBuilder.DropColumn(
                name: "ContractId",
                table: "claims");

            migrationBuilder.RenameColumn(
                name: "SlaViolationId",
                table: "claims",
                newName: "SlaEvaluationId");

            migrationBuilder.RenameColumn(
                name: "OpenedAt",
                table: "claims",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_claims_SlaViolationId",
                table: "claims",
                newName: "IX_claims_SlaEvaluationId");

            migrationBuilder.RenameIndex(
                name: "IX_claims_CompanyId_SlaViolationId",
                table: "claims",
                newName: "IX_claims_CompanyId_SlaEvaluationId");

            migrationBuilder.RenameIndex(
                name: "IX_claims_CompanyId_OpenedAt",
                table: "claims",
                newName: "IX_claims_CompanyId_CreatedAt");

            migrationBuilder.AddCheckConstraint(
                name: "ck_claims_status",
                table: "claims",
                sql: "\"Status\" in (1,2,3)");

            migrationBuilder.AddForeignKey(
                name: "FK_claims_sla_evaluations_SlaEvaluationId",
                table: "claims",
                column: "SlaEvaluationId",
                principalTable: "sla_evaluations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
