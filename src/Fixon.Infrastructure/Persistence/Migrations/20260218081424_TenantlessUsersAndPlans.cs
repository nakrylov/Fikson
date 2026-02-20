using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fixon.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TenantlessUsersAndPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "CompanyId",
                table: "users",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<bool>(
                name: "EmailConfirmed",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MonthlyUsageLimit",
                table: "companies",
                type: "integer",
                nullable: false,
                defaultValue: 1000);

            migrationBuilder.AddColumn<int>(
                name: "PlanType",
                table: "companies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill existing tenants to have a sane default usage limit.
            migrationBuilder.Sql(@"UPDATE companies SET ""MonthlyUsageLimit"" = 1000 WHERE ""MonthlyUsageLimit"" = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailConfirmed",
                table: "users");

            migrationBuilder.DropColumn(
                name: "MonthlyUsageLimit",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "PlanType",
                table: "companies");

            migrationBuilder.AlterColumn<Guid>(
                name: "CompanyId",
                table: "users",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
