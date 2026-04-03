using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fixon.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCounterpartyIdToContracts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_contracts_counterparties_CounterpartyId",
                table: "contracts");

            migrationBuilder.RenameColumn(
                name: "CounterpartyId",
                table: "contracts",
                newName: "counterparty_id");

            migrationBuilder.RenameIndex(
                name: "IX_contracts_CounterpartyId",
                table: "contracts",
                newName: "IX_contracts_counterparty_id");

            migrationBuilder.RenameIndex(
                name: "IX_contracts_CompanyId_CounterpartyId",
                table: "contracts",
                newName: "IX_contracts_CompanyId_counterparty_id");

            migrationBuilder.AlterColumn<Guid>(
                name: "counterparty_id",
                table: "contracts",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_contracts_counterparties_counterparty_id",
                table: "contracts",
                column: "counterparty_id",
                principalTable: "counterparties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_contracts_counterparties_counterparty_id",
                table: "contracts");

            migrationBuilder.RenameColumn(
                name: "counterparty_id",
                table: "contracts",
                newName: "CounterpartyId");

            migrationBuilder.RenameIndex(
                name: "IX_contracts_counterparty_id",
                table: "contracts",
                newName: "IX_contracts_CounterpartyId");

            migrationBuilder.RenameIndex(
                name: "IX_contracts_CompanyId_counterparty_id",
                table: "contracts",
                newName: "IX_contracts_CompanyId_CounterpartyId");

            migrationBuilder.AlterColumn<Guid>(
                name: "CounterpartyId",
                table: "contracts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_contracts_counterparties_CounterpartyId",
                table: "contracts",
                column: "CounterpartyId",
                principalTable: "counterparties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
