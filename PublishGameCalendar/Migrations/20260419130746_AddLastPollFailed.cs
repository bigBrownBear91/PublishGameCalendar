using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PublishGameCalendar.Migrations
{
    /// <inheritdoc />
    public partial class AddLastPollFailed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "LastPollFailed",
                table: "polling_config",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastPollFailed",
                table: "polling_config");
        }
    }
}
