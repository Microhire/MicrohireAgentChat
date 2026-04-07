using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicrohireAgentChat.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingNoToWestinLeads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BookingNo",
                schema: "dbo",
                table: "WestinLeads",
                type: "nvarchar(35)",
                maxLength: 35,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DraftStateJson",
                schema: "dbo",
                table: "AgentThreads",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                schema: "dbo",
                table: "AgentThreads",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentThreads_Email",
                schema: "dbo",
                table: "AgentThreads",
                column: "Email");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AgentThreads_Email",
                schema: "dbo",
                table: "AgentThreads");

            migrationBuilder.DropColumn(
                name: "BookingNo",
                schema: "dbo",
                table: "WestinLeads");

            migrationBuilder.DropColumn(
                name: "DraftStateJson",
                schema: "dbo",
                table: "AgentThreads");

            migrationBuilder.DropColumn(
                name: "Email",
                schema: "dbo",
                table: "AgentThreads");
        }
    }
}
