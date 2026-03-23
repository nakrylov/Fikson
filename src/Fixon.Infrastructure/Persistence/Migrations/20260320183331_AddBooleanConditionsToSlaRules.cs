using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fixon.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBooleanConditionsToSlaRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "event_type",
                table: "sla_rules",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "event_type",
                table: "sla_rules");
        }
    }
}
