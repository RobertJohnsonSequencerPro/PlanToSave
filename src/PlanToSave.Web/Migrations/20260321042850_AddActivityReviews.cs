using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlanToSave.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityReviews",
                schema: "plantosave",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: true),
                    Reflection = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ActualAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityReviews_ActivityPlans_ActivityPlanId",
                        column: x => x.ActivityPlanId,
                        principalSchema: "plantosave",
                        principalTable: "ActivityPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityReviews_ActivityPlanId",
                schema: "plantosave",
                table: "ActivityReviews",
                column: "ActivityPlanId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityReviews",
                schema: "plantosave");
        }
    }
}
