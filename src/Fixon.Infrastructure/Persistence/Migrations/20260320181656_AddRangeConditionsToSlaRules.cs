using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fixon.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRangeConditionsToSlaRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "condition_type",
                table: "sla_rules",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "threshold");

            migrationBuilder.Sql("UPDATE sla_rules SET condition_type = 'threshold' WHERE condition_type IS NULL;");

            migrationBuilder.AddColumn<decimal>(
                name: "max_value",
                table: "sla_rules",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "min_value",
                table: "sla_rules",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "condition_type",
                table: "sla_rules");

            migrationBuilder.DropColumn(
                name: "max_value",
                table: "sla_rules");

            migrationBuilder.DropColumn(
                name: "min_value",
                table: "sla_rules");
        }
    }
}
