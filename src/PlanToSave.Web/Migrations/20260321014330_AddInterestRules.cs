using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlanToSave.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddInterestRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InterestRules",
                schema: "plantosave",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnnualRatePct = table.Column<decimal>(type: "numeric(10,5)", precision: 10, scale: 5, nullable: false),
                    Frequency = table.Column<string>(type: "text", nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterestRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InterestRules_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "plantosave",
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InterestRules_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "plantosave",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InterestRules_AccountId",
                schema: "plantosave",
                table: "InterestRules",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_InterestRules_UserId_AccountId",
                schema: "plantosave",
                table: "InterestRules",
                columns: new[] { "UserId", "AccountId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InterestRules",
                schema: "plantosave");
        }
    }
}
