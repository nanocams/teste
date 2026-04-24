using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataBaseManager.Migrations
{
    /// <inheritdoc />
    public partial class CreateAllTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "industries",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_industries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "providers",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    api_url = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    auth_type = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_providers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sectors",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    designation = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sectors", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stocks",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    symbol = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    industry_id = table.Column<int>(type: "int", nullable: true),
                    sector_id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stocks", x => x.id);
                    table.ForeignKey(
                        name: "FK_stocks_industries_industry_id",
                        column: x => x.industry_id,
                        principalTable: "industries",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_stocks_sectors_sector_id",
                        column: x => x.sector_id,
                        principalTable: "sectors",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "fundamentals",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    earnings = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    balance_sheet = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    cash_flow = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    provider_id = table.Column<int>(type: "int", nullable: true),
                    stock_id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fundamentals", x => x.id);
                    table.ForeignKey(
                        name: "FK_fundamentals_providers_provider_id",
                        column: x => x.provider_id,
                        principalTable: "providers",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_fundamentals_stocks_stock_id",
                        column: x => x.stock_id,
                        principalTable: "stocks",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "indicators",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    sma = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    rsi = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    macd = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    provider_id = table.Column<int>(type: "int", nullable: true),
                    stock_id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_indicators", x => x.id);
                    table.ForeignKey(
                        name: "FK_indicators_providers_provider_id",
                        column: x => x.provider_id,
                        principalTable: "providers",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_indicators_stocks_stock_id",
                        column: x => x.stock_id,
                        principalTable: "stocks",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "news",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    headline = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    sentiment = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    source = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    stock_id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_news", x => x.id);
                    table.ForeignKey(
                        name: "FK_news_stocks_stock_id",
                        column: x => x.stock_id,
                        principalTable: "stocks",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "prices",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    open = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    high = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    low = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    close = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    volume = table.Column<long>(type: "bigint", nullable: false),
                    provider_id = table.Column<int>(type: "int", nullable: true),
                    stock_id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prices", x => x.id);
                    table.ForeignKey(
                        name: "FK_prices_providers_provider_id",
                        column: x => x.provider_id,
                        principalTable: "providers",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_prices_stocks_stock_id",
                        column: x => x.stock_id,
                        principalTable: "stocks",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_fundamentals_provider_id",
                table: "fundamentals",
                column: "provider_id");

            migrationBuilder.CreateIndex(
                name: "IX_fundamentals_stock_id",
                table: "fundamentals",
                column: "stock_id");

            migrationBuilder.CreateIndex(
                name: "IX_indicators_provider_id",
                table: "indicators",
                column: "provider_id");

            migrationBuilder.CreateIndex(
                name: "IX_indicators_stock_id",
                table: "indicators",
                column: "stock_id");

            migrationBuilder.CreateIndex(
                name: "IX_news_stock_id",
                table: "news",
                column: "stock_id");

            migrationBuilder.CreateIndex(
                name: "IX_prices_provider_id",
                table: "prices",
                column: "provider_id");

            migrationBuilder.CreateIndex(
                name: "IX_prices_stock_id",
                table: "prices",
                column: "stock_id");

            migrationBuilder.CreateIndex(
                name: "IX_stocks_industry_id",
                table: "stocks",
                column: "industry_id");

            migrationBuilder.CreateIndex(
                name: "IX_stocks_sector_id",
                table: "stocks",
                column: "sector_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fundamentals");

            migrationBuilder.DropTable(
                name: "indicators");

            migrationBuilder.DropTable(
                name: "news");

            migrationBuilder.DropTable(
                name: "prices");

            migrationBuilder.DropTable(
                name: "providers");

            migrationBuilder.DropTable(
                name: "stocks");

            migrationBuilder.DropTable(
                name: "industries");

            migrationBuilder.DropTable(
                name: "sectors");
        }
    }
}
