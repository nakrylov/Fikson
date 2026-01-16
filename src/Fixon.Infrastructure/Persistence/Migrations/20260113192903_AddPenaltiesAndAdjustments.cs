using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fixon.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPenaltiesAndAdjustments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "penalties",
                columns: table => new
                {
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: false),
                    CalculatedAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CalculatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PenaltyJsonSnapshot = table.Column<string>(type: "jsonb", nullable: false),
                    ExplanationJson = table.Column<string>(type: "jsonb", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_penalties", x => x.ClaimId);
                    table.ForeignKey(
                        name: "FK_penalties_claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "penalty_adjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalPenaltyAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    NewPenaltyAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AdjustedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdjustedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_penalty_adjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_penalty_adjustments_claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_penalty_adjustments_users_AdjustedByUserId",
                        column: x => x.AdjustedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_penalties_CompanyId",
                table: "penalties",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_penalties_CompanyId_CalculatedAt",
                table: "penalties",
                columns: new[] { "CompanyId", "CalculatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_penalty_adjustments_AdjustedByUserId",
                table: "penalty_adjustments",
                column: "AdjustedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_penalty_adjustments_ClaimId",
                table: "penalty_adjustments",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_penalty_adjustments_CompanyId",
                table: "penalty_adjustments",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_penalty_adjustments_CompanyId_ClaimId_AdjustedAt",
                table: "penalty_adjustments",
                columns: new[] { "CompanyId", "ClaimId", "AdjustedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "penalties");

            migrationBuilder.DropTable(
                name: "penalty_adjustments");
        }
    }
}
