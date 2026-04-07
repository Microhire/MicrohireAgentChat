using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicrohireAgentChat.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteSignedToWestinLeads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "QuoteSignedUtc",
                schema: "dbo",
                table: "WestinLeads",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignedByName",
                schema: "dbo",
                table: "WestinLeads",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QuoteSignedUtc",
                schema: "dbo",
                table: "WestinLeads");

            migrationBuilder.DropColumn(
                name: "SignedByName",
                schema: "dbo",
                table: "WestinLeads");
        }
    }
}
