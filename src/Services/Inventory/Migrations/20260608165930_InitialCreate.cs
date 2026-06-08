using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Services.Inventory.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "inventory");

            migrationBuilder.CreateTable(
                name: "inbox",
                schema: "inventory",
                columns: table => new
                {
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbox", x => x.message_id);
                });

            migrationBuilder.CreateTable(
                name: "outbox",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stock_items",
                schema: "inventory",
                columns: table => new
                {
                    sku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    available = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_items", x => x.sku);
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_pending",
                schema: "inventory",
                table: "outbox",
                column: "occurred_at",
                filter: "processed_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inbox",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "outbox",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "stock_items",
                schema: "inventory");
        }
    }
}
