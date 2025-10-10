using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERPApi.Migrations
{
    /// <inheritdoc />
    public partial class RenameOrderStatusToOrderState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "OrderStatus",
                table: "Orders",
                newName: "OrderState");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "OrderState",
                table: "Orders",
                newName: "OrderStatus");
        }
    }
}
