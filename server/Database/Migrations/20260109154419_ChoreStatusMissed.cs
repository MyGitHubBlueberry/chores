using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Database.Migrations
{
    /// <inheritdoc />
    public partial class ChoreStatusMissed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "NextMemberIdx",
                table: "Chores",
                newName: "CurrentQueueMemberIdx");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CurrentQueueMemberIdx",
                table: "Chores",
                newName: "NextMemberIdx");
        }
    }
}
