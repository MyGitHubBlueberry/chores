using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Password = table.Column<byte[]>(type: "BLOB", nullable: false),
                    AvatarUrl = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Chores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OwnerId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Chores_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChoreDescriptions",
                columns: table => new
                {
                    ChoreId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Body = table.Column<string>(type: "TEXT", nullable: true),
                    AvatarUrl = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChoreDescriptions", x => x.ChoreId);
                    table.ForeignKey(
                        name: "FK_ChoreDescriptions_Chores_ChoreId",
                        column: x => x.ChoreId,
                        principalTable: "Chores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChoreLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChoreId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Duration = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChoreLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChoreLogs_Chores_ChoreId",
                        column: x => x.ChoreId,
                        principalTable: "Chores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChoreLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ChoreMembers",
                columns: table => new
                {
                    ChoreId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    RotationOrder = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChoreMembers", x => new { x.ChoreId, x.UserId });
                    table.ForeignKey(
                        name: "FK_ChoreMembers_Chores_ChoreId",
                        column: x => x.ChoreId,
                        principalTable: "Chores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChoreMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChoreSchedules",
                columns: table => new
                {
                    ChoreId = table.Column<int>(type: "INTEGER", nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Duration = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    Interval = table.Column<TimeSpan>(type: "TEXT", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "ChoreQueue",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChoreId = table.Column<int>(type: "INTEGER", nullable: false),
                    AssignedMemberId = table.Column<int>(type: "INTEGER", nullable: false),
                    ScheduledDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChoreQueue", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChoreQueue_ChoreMembers_ChoreId_AssignedMemberId",
                        columns: x => new { x.ChoreId, x.AssignedMemberId },
                        principalTable: "ChoreMembers",
                        principalColumns: new[] { "ChoreId", "UserId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChoreQueue_Chores_ChoreId",
                        column: x => x.ChoreId,
                        principalTable: "Chores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChoreLogs_ChoreId",
                table: "ChoreLogs",
                column: "ChoreId");

            migrationBuilder.CreateIndex(
                name: "IX_ChoreLogs_UserId",
                table: "ChoreLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChoreMembers_UserId",
                table: "ChoreMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChoreQueue_ChoreId_AssignedMemberId",
                table: "ChoreQueue",
                columns: new[] { "ChoreId", "AssignedMemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_Chores_OwnerId",
                table: "Chores",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChoreAdmins");

            migrationBuilder.DropTable(
                name: "ChoreDescriptions");

            migrationBuilder.DropTable(
                name: "ChoreLogs");

            migrationBuilder.DropTable(
                name: "ChoreQueue");

            migrationBuilder.DropTable(
                name: "ChoreSchedules");

            migrationBuilder.DropTable(
                name: "ChoreStates");

            migrationBuilder.DropTable(
                name: "ChoreMembers");

            migrationBuilder.DropTable(
                name: "Chores");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
