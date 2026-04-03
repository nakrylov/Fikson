using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fixon.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFactTypeDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fact_type_definitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ValueType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DefaultConditionType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Unit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fact_type_definitions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fact_type_definitions_EventType",
                table: "fact_type_definitions",
                column: "EventType",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fact_type_definitions_IsActive",
                table: "fact_type_definitions",
                column: "IsActive");

            migrationBuilder.Sql(@"
INSERT INTO fact_type_definitions (""Id"", ""EventType"", ""DisplayName"", ""ValueType"", ""DefaultConditionType"", ""Unit"", ""IsActive"", ""CreatedAt"")
VALUES
('a7fbf6c8-a293-43f8-b020-34915211f334', 'TEMPERATURE_READING', 'Temperature reading', 'number', 'range', '°C', true, NOW()),
('30d09e8a-7724-4f81-a32d-46415618d647', 'DELIVERY_DELAY', 'Delivery delay', 'number', 'threshold', null, true, NOW()),
('1324e2bb-3ca5-4ff1-b2fc-6d80c4a88f0f', 'DOCUMENT_MISSING', 'Document missing', 'boolean', 'boolean', null, true, NOW()),
('f18e6cf4-2a32-43a7-9794-d2fa259127cd', 'DELIVERY_PLANNED', 'Delivery planned', 'datetime', 'threshold', null, true, NOW()),
('ade3d48e-f13c-4030-9ba0-c690d8457807', 'DELIVERY_ACTUAL', 'Delivery actual', 'datetime', 'threshold', null, true, NOW())
ON CONFLICT (""EventType"") DO NOTHING;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fact_type_definitions");
        }
    }
}
