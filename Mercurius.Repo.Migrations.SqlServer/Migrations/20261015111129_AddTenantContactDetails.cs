using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercurius.Repo.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantContactDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Tenants",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactNumber",
                table: "Tenants",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Address",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ContactNumber",
                table: "Tenants");
        }
    }
}
