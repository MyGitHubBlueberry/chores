using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Database.Migrations
{
    /// <inheritdoc />
    public partial class MovedOneToOneTablesToChoresTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChoreSchedules");

            migrationBuilder.DropTable(
                name: "ChoreStates");

            migrationBuilder.AddColumn<TimeSpan>(
                name: "Duration",
                table: "Chores",
                type: "TEXT",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));

            migrationBuilder.AddColumn<TimeSpan>(
                name: "Interval",
                table: "Chores",
                type: "TEXT",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));

            migrationBuilder.AddColumn<bool>(
                name: "IsPaused",
                table: "Chores",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "NextMemberIdx",
                table: "Chores",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "Chores",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Duration",
                table: "Chores");

            migrationBuilder.DropColumn(
                name: "Interval",
                table: "Chores");

            migrationBuilder.DropColumn(
                name: "IsPaused",
                table: "Chores");

            migrationBuilder.DropColumn(
                name: "NextMemberIdx",
                table: "Chores");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "Chores");

            migrationBuilder.CreateTable(
                name: "ChoreSchedules",
                columns: table => new
                {
                    ChoreId = table.Column<int>(type: "INTEGER", nullable: false),
                    Duration = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    Interval = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChoreSchedules", x => x.ChoreId);
                    table.ForeignKey(
                        name: "FK_ChoreSchedules_Chores_ChoreId",
                        column: x => x.ChoreId,
                        principalTable: "Chores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChoreStates",
                columns: table => new
                {
                    ChoreId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsPaused = table.Column<bool>(type: "INTEGER", nullable: false),
                    NextMemberIdx = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChoreStates", x => x.ChoreId);
                    table.ForeignKey(
                        name: "FK_ChoreStates_Chores_ChoreId",
                        column: x => x.ChoreId,
                        principalTable: "Chores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }
    }
}
