using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoanProject.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Hand-written migration: these three tables intentionally live outside
    /// the EF model (no DbSet, no configuration) so the change tracker —
    /// built to UPDATE — can never touch the ledger. The event store is
    /// reached only through its repository, which appends and reads.
    /// </summary>
    public partial class AddEventStoreTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventStore",
                columns: table => new
                {
                    // Global write order across ALL aggregates — the dispatcher
                    // (phase 5) publishes everything with Sequence > cursor.
                    Sequence = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AggregateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    // Per-aggregate order, starting at 1 for every stream.
                    Version = table.Column<int>(type: "int", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EventData = table.Column<string>(type: "nvarchar(max)", nullable: false), // JSON payload
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventStore", x => x.Sequence);
                    // Optimistic concurrency referee: two writers that loaded
                    // the same version race to insert version + 1 — the loser
                    // violates this constraint and must reload-and-retry. Its
                    // backing index also matches the replay query exactly
                    // (WHERE AggregateId = @id ORDER BY Version), so the same
                    // structure is both the referee and the fast lane.
                    table.UniqueConstraint("UQ_EventStore_AggVer", x => new { x.AggregateId, x.Version });
                });

            migrationBuilder.CreateTable(
                name: "LoanSnapshot",
                columns: table => new
                {
                    // Only the latest snapshot is kept per loan, so the
                    // aggregate id itself is the key: a newer snapshot
                    // overwrites the row. This table is a cache, not the
                    // ledger — losing it costs nothing but a longer replay.
                    AggregateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    StateData = table.Column<string>(type: "nvarchar(max)", nullable: false), // JSON of replayed state
                    TakenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_LoanSnapshot", x => x.AggregateId));

            migrationBuilder.CreateTable(
                name: "DispatcherCursor",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    // Highest EventStore.Sequence already published to Redpanda.
                    LastSequence = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DispatcherCursor", x => x.Id);
                    // The bookmark is a singleton: the check makes a second row
                    // impossible, mechanically backing the single-active-
                    // dispatcher rule from the event sourcing design.
                    table.CheckConstraint("CK_DispatcherCursor_SingleRow", "[Id] = 1");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Local-dev rollback only: once real events exist anywhere,
            // reverting this migration destroys the ledger. The append-only
            // rule forbids exactly that past the first deployment.
            migrationBuilder.DropTable(name: "DispatcherCursor");
            migrationBuilder.DropTable(name: "LoanSnapshot");
            migrationBuilder.DropTable(name: "EventStore");
        }
    }
}
