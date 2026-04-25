using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PublishGameCalendar.Migrations
{
    /// <inheritdoc />
    public partial class AddLastEventCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LastEventCount",
                table: "polling_config",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastEventCount",
                table: "polling_config");
        }
    }
}
