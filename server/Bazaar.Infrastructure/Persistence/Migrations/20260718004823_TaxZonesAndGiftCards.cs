using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bazaar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TaxZonesAndGiftCards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TaxCategory",
                table: "Products",
                type: "TEXT",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "GiftCardCode",
                table: "Orders",
                type: "TEXT",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "GiftCardTotalAmount",
                table: "Orders",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "GiftCardTotalCurrency",
                table: "Orders",
                type: "TEXT",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "GiftCards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    InitialAmount = table.Column<long>(type: "INTEGER", nullable: false),
                    InitialCurrency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    BalanceAmount = table.Column<long>(type: "INTEGER", nullable: false),
                    BalanceCurrency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiftCards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaxZones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Country = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false),
                    Region = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    StandardRate = table.Column<decimal>(type: "TEXT", precision: 9, scale: 5, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxZones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaxCategoryRates",
                columns: table => new
                {
                    Category = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    TaxZoneId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Rate = table.Column<decimal>(type: "TEXT", precision: 9, scale: 5, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxCategoryRates", x => new { x.TaxZoneId, x.Category });
                    table.ForeignKey(
                        name: "FK_TaxCategoryRates_TaxZones_TaxZoneId",
                        column: x => x.TaxZoneId,
                        principalTable: "TaxZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GiftCards_Code",
                table: "GiftCards",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxZones_Country_Region",
                table: "TaxZones",
                columns: new[] { "Country", "Region" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GiftCards");

            migrationBuilder.DropTable(
                name: "TaxCategoryRates");

            migrationBuilder.DropTable(
                name: "TaxZones");

            migrationBuilder.DropColumn(
                name: "TaxCategory",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "GiftCardCode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "GiftCardTotalAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "GiftCardTotalCurrency",
                table: "Orders");
        }
    }
}
