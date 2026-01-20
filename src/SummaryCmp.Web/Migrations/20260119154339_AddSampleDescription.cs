using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SummaryCmp.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddSampleDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SampleDescription",
                table: "ComparisonSessions",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SampleDescription",
                table: "ComparisonSessions");
        }
    }
}
