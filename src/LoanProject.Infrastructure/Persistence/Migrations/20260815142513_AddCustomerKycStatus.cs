using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoanProject.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerKycStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KycStatus",
                table: "Customer",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KycStatus",
                table: "Customer");
        }
    }
}
