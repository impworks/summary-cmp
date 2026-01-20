using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SummaryCmp.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddTokenTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InputTokens",
                table: "SummaryResults",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InternalTokens",
                table: "SummaryResults",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OutputTokens",
                table: "SummaryResults",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InputTokens",
                table: "SummaryResults");

            migrationBuilder.DropColumn(
                name: "InternalTokens",
                table: "SummaryResults");

            migrationBuilder.DropColumn(
                name: "OutputTokens",
                table: "SummaryResults");
        }
    }
}
