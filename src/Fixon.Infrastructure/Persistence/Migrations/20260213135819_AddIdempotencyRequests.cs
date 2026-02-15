using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fixon.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIdempotencyRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "idempotency_requests",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Endpoint = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ResponseStatusCode = table.Column<int>(type: "integer", nullable: false),
                    ResponseBody = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_requests", x => new { x.TenantId, x.Key, x.Endpoint });
                });

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_requests_CreatedAt",
                table: "idempotency_requests",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_requests_TenantId",
                table: "idempotency_requests",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_requests_TenantId_Key_Endpoint",
                table: "idempotency_requests",
                columns: new[] { "TenantId", "Key", "Endpoint" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "idempotency_requests");
        }
    }
}
