using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fixon.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ImportTwoPhaseV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ImportBatchId",
                table: "facts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayloadHash",
                table: "facts",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "import_batches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    ExternalBatchId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Checksum = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_batches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "import_rows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    RawPayload = table.Column<string>(type: "jsonb", nullable: false),
                    ValidationStatus = table.Column<int>(type: "integer", nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_rows", x => x.Id);
                    table.CheckConstraint("ck_import_rows_validation_status", "\"ValidationStatus\" in (1,2,3,4)");
                    table.ForeignKey(
                        name: "FK_import_rows_import_batches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "import_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_facts_CompanyId_ExternalReference_OccurredAt",
                table: "facts",
                columns: new[] { "CompanyId", "ExternalReference", "OccurredAt" },
                unique: true,
                filter: "\"ExternalReference\" is not null");

            migrationBuilder.CreateIndex(
                name: "IX_facts_CompanyId_ImportBatchId_CreatedAt",
                table: "facts",
                columns: new[] { "CompanyId", "ImportBatchId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_import_batches_CompanyId",
                table: "import_batches",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_import_batches_CompanyId_Source_ExternalBatchId",
                table: "import_batches",
                columns: new[] { "CompanyId", "Source", "ExternalBatchId" },
                unique: true,
                filter: "\"ExternalBatchId\" is not null");

            migrationBuilder.CreateIndex(
                name: "IX_import_batches_CompanyId_Status_StartedAt",
                table: "import_batches",
                columns: new[] { "CompanyId", "Status", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_import_rows_CompanyId",
                table: "import_rows",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_import_rows_CompanyId_ImportBatchId_RowNumber",
                table: "import_rows",
                columns: new[] { "CompanyId", "ImportBatchId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_import_rows_CompanyId_ImportBatchId_ValidationStatus",
                table: "import_rows",
                columns: new[] { "CompanyId", "ImportBatchId", "ValidationStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_import_rows_ImportBatchId",
                table: "import_rows",
                column: "ImportBatchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "import_rows");

            migrationBuilder.DropTable(
                name: "import_batches");

            migrationBuilder.DropIndex(
                name: "IX_facts_CompanyId_ExternalReference_OccurredAt",
                table: "facts");

            migrationBuilder.DropIndex(
                name: "IX_facts_CompanyId_ImportBatchId_CreatedAt",
                table: "facts");

            migrationBuilder.DropColumn(
                name: "ImportBatchId",
                table: "facts");

            migrationBuilder.DropColumn(
                name: "PayloadHash",
                table: "facts");
        }
    }
}
