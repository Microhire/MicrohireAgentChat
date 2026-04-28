using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicrohireAgentChat.Migrations
{
    /// <inheritdoc />
    public partial class AddEventScheduleJsonToWestinLeads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EventScheduleJson",
                schema: "dbo",
                table: "WestinLeads",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EventScheduleJson",
                schema: "dbo",
                table: "WestinLeads");
        }
    }
}
