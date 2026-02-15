using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fixon.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ContractXminConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // xmin is a PostgreSQL system column. It must NOT be created/altered via migrations.
            // This migration exists to keep the EF model snapshot in sync with the runtime model,
            // while relying on the built-in system column for optimistic concurrency.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op (see Up).
        }
    }
}
