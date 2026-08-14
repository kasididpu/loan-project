using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoanProject.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Hand-written migration: stored procedures live outside the EF model,
    /// so the body ships as raw SQL. CREATE OR ALTER keeps the migration
    /// re-runnable while the procedure evolves during development.
    /// </summary>
    public partial class AddEndOfDaySummaryProcedure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Why a procedure instead of LINQ: pure set-based aggregation —
            // thousands of rows collapse to one summary row per loan next to
            // the data, and only the summary crosses the wire. Business
            // rules stay in C#; this is reporting arithmetic.
            migrationBuilder.Sql(
                """
                CREATE OR ALTER PROCEDURE dbo.usp_GetEndOfDayLoanSummary
                    @AsOfDate date
                AS
                BEGIN
                    SET NOCOUNT ON;

                    -- Half-open range on the raw column keeps the predicate
                    -- sargable; wrapping PaidAtUtc in CAST would force a scan
                    -- regardless of any index.
                    SELECT
                        LoanId,
                        COUNT(*)       AS PaymentsCount,
                        SUM(Amount)    AS TotalCollected,
                        MAX(PaidAtUtc) AS LastPaymentAtUtc
                    FROM dbo.Payment
                    WHERE PaidAtUtc >= @AsOfDate
                      AND PaidAtUtc < DATEADD(day, 1, @AsOfDate)
                    GROUP BY LoanId
                    ORDER BY TotalCollected DESC;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_GetEndOfDayLoanSummary;");
        }
    }
}
