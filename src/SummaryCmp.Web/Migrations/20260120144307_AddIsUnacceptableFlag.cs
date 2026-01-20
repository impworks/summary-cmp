using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SummaryCmp.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddIsUnacceptableFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsUnacceptable",
                table: "SummaryResults",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsUnacceptable",
                table: "SummaryResults");
        }
    }
}
