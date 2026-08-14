using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoanProject.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Payment_LoanId_PaidAtUtc",
                table: "Payment",
                columns: new[] { "LoanId", "PaidAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_Payment_StripeEventId",
                table: "Payment",
                column: "StripeEventId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payment_LoanId_PaidAtUtc",
                table: "Payment");

            migrationBuilder.DropIndex(
                name: "UX_Payment_StripeEventId",
                table: "Payment");
        }
    }
}
