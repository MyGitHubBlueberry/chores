using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Database.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceAdminTableWithBool : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChoreAdmins");

            migrationBuilder.AddColumn<bool>(
                name: "IsAdmin",
                table: "ChoreMembers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAdmin",
                table: "ChoreMembers");

            migrationBuilder.CreateTable(
                name: "ChoreAdmins",
                columns: table => new
                {
                    ChoreId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChoreAdmins", x => new { x.ChoreId, x.UserId });
                    table.ForeignKey(
                        name: "FK_ChoreAdmins_ChoreMembers_ChoreId_UserId",
                        columns: x => new { x.ChoreId, x.UserId },
                        principalTable: "ChoreMembers",
                        principalColumns: new[] { "ChoreId", "UserId" },
                        onDelete: ReferentialAction.Cascade);
                });
        }
    }
}
