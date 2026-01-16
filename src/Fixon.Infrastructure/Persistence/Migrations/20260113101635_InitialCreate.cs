using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fixon.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "companies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_companies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "counterparties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExternalCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_counterparties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_counterparties_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_users_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "contracts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CounterpartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contracts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contracts_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_contracts_counterparties_CounterpartyId",
                        column: x => x.CounterpartyId,
                        principalTable: "counterparties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ActionType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ChangesJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_audit_logs_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_audit_logs_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_roles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_user_roles_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_roles_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "contract_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PdfFilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contract_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contract_versions_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_contract_versions_contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_contract_versions_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "facts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExternalReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FactType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AttributesJson = table.Column<string>(type: "jsonb", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_facts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_facts_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_facts_contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sla_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sla_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sla_rules_contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sla_rule_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SlaRuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    AppliesWhenJson = table.Column<string>(type: "jsonb", nullable: false),
                    ConditionJson = table.Column<string>(type: "jsonb", nullable: false),
                    PenaltyJson = table.Column<string>(type: "jsonb", nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sla_rule_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sla_rule_versions_sla_rules_SlaRuleId",
                        column: x => x.SlaRuleId,
                        principalTable: "sla_rules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sla_rule_versions_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sla_evaluations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SlaRuleVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvaluationResult = table.Column<int>(type: "integer", nullable: false),
                    CalculatedValuesJson = table.Column<string>(type: "jsonb", nullable: false),
                    EvaluatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sla_evaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sla_evaluations_contract_versions_ContractVersionId",
                        column: x => x.ContractVersionId,
                        principalTable: "contract_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sla_evaluations_sla_rule_versions_SlaRuleVersionId",
                        column: x => x.SlaRuleVersionId,
                        principalTable: "sla_rule_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "claims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SlaRuleVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SlaEvaluationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_claims", x => x.Id);
                    table.CheckConstraint("ck_claims_status", "\"Status\" in (1,2,3)");
                    table.ForeignKey(
                        name: "FK_claims_contract_versions_ContractVersionId",
                        column: x => x.ContractVersionId,
                        principalTable: "contract_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_claims_sla_evaluations_SlaEvaluationId",
                        column: x => x.SlaEvaluationId,
                        principalTable: "sla_evaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_claims_sla_rule_versions_SlaRuleVersionId",
                        column: x => x.SlaRuleVersionId,
                        principalTable: "sla_rule_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_claims_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_CompanyId",
                table: "audit_logs",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_CompanyId_CreatedAt",
                table: "audit_logs",
                columns: new[] { "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_CompanyId_EntityType_EntityId",
                table: "audit_logs",
                columns: new[] { "CompanyId", "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_CompanyId_UserId_CreatedAt",
                table: "audit_logs",
                columns: new[] { "CompanyId", "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_UserId",
                table: "audit_logs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_claims_CompanyId",
                table: "claims",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_claims_CompanyId_ContractVersionId",
                table: "claims",
                columns: new[] { "CompanyId", "ContractVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_claims_CompanyId_CreatedAt",
                table: "claims",
                columns: new[] { "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_claims_CompanyId_SlaEvaluationId",
                table: "claims",
                columns: new[] { "CompanyId", "SlaEvaluationId" });

            migrationBuilder.CreateIndex(
                name: "IX_claims_CompanyId_SlaRuleVersionId",
                table: "claims",
                columns: new[] { "CompanyId", "SlaRuleVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_claims_CompanyId_Status",
                table: "claims",
                columns: new[] { "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_claims_ContractVersionId",
                table: "claims",
                column: "ContractVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_claims_CreatedByUserId",
                table: "claims",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_claims_SlaEvaluationId",
                table: "claims",
                column: "SlaEvaluationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_claims_SlaRuleVersionId",
                table: "claims",
                column: "SlaRuleVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_companies_IsActive",
                table: "companies",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_companies_Name",
                table: "companies",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_contract_versions_CompanyId",
                table: "contract_versions",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_contract_versions_CompanyId_ContractId",
                table: "contract_versions",
                columns: new[] { "CompanyId", "ContractId" });

            migrationBuilder.CreateIndex(
                name: "IX_contract_versions_ContractId_VersionNumber",
                table: "contract_versions",
                columns: new[] { "ContractId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contract_versions_CreatedByUserId",
                table: "contract_versions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_contracts_CompanyId",
                table: "contracts",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_contracts_CompanyId_CounterpartyId",
                table: "contracts",
                columns: new[] { "CompanyId", "CounterpartyId" });

            migrationBuilder.CreateIndex(
                name: "IX_contracts_CounterpartyId",
                table: "contracts",
                column: "CounterpartyId");

            migrationBuilder.CreateIndex(
                name: "IX_counterparties_CompanyId",
                table: "counterparties",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_counterparties_CompanyId_ExternalCode",
                table: "counterparties",
                columns: new[] { "CompanyId", "ExternalCode" });

            migrationBuilder.CreateIndex(
                name: "IX_counterparties_CompanyId_Name",
                table: "counterparties",
                columns: new[] { "CompanyId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_facts_CompanyId",
                table: "facts",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_facts_CompanyId_ContractId_OccurredAt",
                table: "facts",
                columns: new[] { "CompanyId", "ContractId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_facts_CompanyId_FactType_OccurredAt",
                table: "facts",
                columns: new[] { "CompanyId", "FactType", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_facts_CompanyId_OccurredAt",
                table: "facts",
                columns: new[] { "CompanyId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_facts_ContractId",
                table: "facts",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_roles_Name",
                table: "roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sla_evaluations_CompanyId",
                table: "sla_evaluations",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_sla_evaluations_CompanyId_ContractVersionId",
                table: "sla_evaluations",
                columns: new[] { "CompanyId", "ContractVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_sla_evaluations_CompanyId_EvaluatedAt",
                table: "sla_evaluations",
                columns: new[] { "CompanyId", "EvaluatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_sla_evaluations_CompanyId_SlaRuleVersionId",
                table: "sla_evaluations",
                columns: new[] { "CompanyId", "SlaRuleVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_sla_evaluations_ContractVersionId",
                table: "sla_evaluations",
                column: "ContractVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_sla_evaluations_SlaRuleVersionId",
                table: "sla_evaluations",
                column: "SlaRuleVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_sla_rule_versions_CompanyId",
                table: "sla_rule_versions",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_sla_rule_versions_CompanyId_SlaRuleId",
                table: "sla_rule_versions",
                columns: new[] { "CompanyId", "SlaRuleId" });

            migrationBuilder.CreateIndex(
                name: "IX_sla_rule_versions_CreatedByUserId",
                table: "sla_rule_versions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_sla_rule_versions_SlaRuleId_VersionNumber",
                table: "sla_rule_versions",
                columns: new[] { "SlaRuleId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sla_rules_CompanyId",
                table: "sla_rules",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_sla_rules_CompanyId_ContractId",
                table: "sla_rules",
                columns: new[] { "CompanyId", "ContractId" });

            migrationBuilder.CreateIndex(
                name: "IX_sla_rules_ContractId",
                table: "sla_rules",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_RoleId",
                table: "user_roles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_UserId",
                table: "user_roles",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_users_CompanyId",
                table: "users",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_users_CompanyId_Email",
                table: "users",
                columns: new[] { "CompanyId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_CompanyId_IsActive",
                table: "users",
                columns: new[] { "CompanyId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "claims");

            migrationBuilder.DropTable(
                name: "facts");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "sla_evaluations");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "contract_versions");

            migrationBuilder.DropTable(
                name: "sla_rule_versions");

            migrationBuilder.DropTable(
                name: "sla_rules");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "contracts");

            migrationBuilder.DropTable(
                name: "counterparties");

            migrationBuilder.DropTable(
                name: "companies");
        }
    }
}
