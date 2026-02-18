using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fixon.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserTenantMemberships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_tenant_memberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_tenant_memberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_tenant_memberships_companies_TenantId",
                        column: x => x.TenantId,
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_tenant_memberships_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_tenant_memberships_TenantId",
                table: "user_tenant_memberships",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_user_tenant_memberships_UserId",
                table: "user_tenant_memberships",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_tenant_memberships_UserId_TenantId",
                table: "user_tenant_memberships",
                columns: new[] { "UserId", "TenantId" },
                unique: true);

            // Backward-compatibility: existing users get a default membership in their current tenant.
            // Role/Admin=0, Status/Active=0.
            // We rely on pgcrypto for gen_random_uuid(); create extension if missing (idempotent).
            migrationBuilder.Sql(@"CREATE EXTENSION IF NOT EXISTS pgcrypto;");

            migrationBuilder.Sql(@"
INSERT INTO user_tenant_memberships (""Id"", ""UserId"", ""TenantId"", ""Role"", ""Status"", ""CreatedAt"")
SELECT gen_random_uuid(), u.""Id"", u.""CompanyId"", 0, 0, u.""CreatedAt""
FROM users u
WHERE NOT EXISTS (
    SELECT 1
    FROM user_tenant_memberships m
    WHERE m.""UserId"" = u.""Id"" AND m.""TenantId"" = u.""CompanyId""
);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_tenant_memberships");
        }
    }
}
