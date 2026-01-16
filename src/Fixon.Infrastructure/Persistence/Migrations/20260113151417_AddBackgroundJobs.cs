using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fixon.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBackgroundJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "background_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    JobType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ScheduledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LockedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LockedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_background_jobs", x => x.Id);
                    table.CheckConstraint("ck_background_jobs_system_tenant", "(\"IsSystem\" = true AND \"CompanyId\" IS NULL) OR (\"IsSystem\" = false AND \"CompanyId\" IS NOT NULL)");
                });

            migrationBuilder.CreateIndex(
                name: "IX_background_jobs_CompanyId_JobType_CorrelationId",
                table: "background_jobs",
                columns: new[] { "CompanyId", "JobType", "CorrelationId" },
                unique: true,
                filter: "\"IsSystem\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_background_jobs_IsSystem_CompanyId_Status_ScheduledAt",
                table: "background_jobs",
                columns: new[] { "IsSystem", "CompanyId", "Status", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_background_jobs_JobType_CorrelationId",
                table: "background_jobs",
                columns: new[] { "JobType", "CorrelationId" },
                unique: true,
                filter: "\"IsSystem\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_background_jobs_Status",
                table: "background_jobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_background_jobs_Status_ScheduledAt",
                table: "background_jobs",
                columns: new[] { "Status", "ScheduledAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "background_jobs");
        }
    }
}
