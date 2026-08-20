using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventHub.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyIdAndStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IdCompany",
                table: "People",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkedInUrl",
                table: "People",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkedInUrl",
                table: "Companies",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_People_IdCompany",
                table: "People",
                column: "IdCompany");

            migrationBuilder.AddForeignKey(
                name: "FK_People_Companies_IdCompany",
                table: "People",
                column: "IdCompany",
                principalTable: "Companies",
                principalColumn: "IdCompany",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_People_Companies_IdCompany",
                table: "People");

            migrationBuilder.DropIndex(
                name: "IX_People_IdCompany",
                table: "People");

            migrationBuilder.DropColumn(
                name: "IdCompany",
                table: "People");

            migrationBuilder.DropColumn(
                name: "LinkedInUrl",
                table: "People");

            migrationBuilder.DropColumn(
                name: "LinkedInUrl",
                table: "Companies");
        }
    }
}
