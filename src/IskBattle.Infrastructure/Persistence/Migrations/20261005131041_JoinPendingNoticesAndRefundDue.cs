using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IskBattle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class JoinPendingNoticesAndRefundDue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RefundDueOre",
                table: "Purchases",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "JoinPendingNotices",
                columns: table => new
                {
                    LeagueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JoinPendingNotices", x => new { x.LeagueId, x.UserId });
                    table.ForeignKey(
                        name: "FK_JoinPendingNotices_Leagues_LeagueId",
                        column: x => x.LeagueId,
                        principalTable: "Leagues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JoinPendingNotices_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JoinPendingNotices_UserId",
                table: "JoinPendingNotices",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JoinPendingNotices");

            migrationBuilder.DropColumn(
                name: "RefundDueOre",
                table: "Purchases");
        }
    }
}
