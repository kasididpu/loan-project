using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoanProject.Infrastructure.ReadModel.Migrations
{
    /// <inheritdoc />
    public partial class InitialReadModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "installment_read_model",
                columns: table => new
                {
                    LoanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstallmentNo = table.Column<int>(type: "int", nullable: false),
                    DueDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Paid = table.Column<bool>(type: "bit", nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaidAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_installment_read_model", x => new { x.LoanId, x.InstallmentNo });
                });

            migrationBuilder.CreateTable(
                name: "loan_read_model",
                columns: table => new
                {
                    LoanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Principal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AnnualRate = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    RateType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TermMonths = table.Column<int>(type: "int", nullable: false),
                    OutstandingBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NextInstallmentNo = table.Column<int>(type: "int", nullable: false),
                    NextDueDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalPaid = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InstallmentsPaid = table.Column<int>(type: "int", nullable: false),
                    OriginatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DisbursedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SettledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DefaultedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastProjectedVersion = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loan_read_model", x => x.LoanId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_installment_read_model_DueDateUtc",
                table: "installment_read_model",
                column: "DueDateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_installment_read_model_PaidAtUtc",
                table: "installment_read_model",
                column: "PaidAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_loan_read_model_Status",
                table: "loan_read_model",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "installment_read_model");

            migrationBuilder.DropTable(
                name: "loan_read_model");
        }
    }
}
