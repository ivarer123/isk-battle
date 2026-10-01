using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IskBattle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LeagueNextCadenceSeason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NextCadenceSeason",
                table: "Leagues",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NextCadenceSeason",
                table: "Leagues");
        }
    }
}
