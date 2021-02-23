using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace NadekoBot.Migrations
{
    public partial class AddLybriaTyanChanges : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Count",
                table: "WaifuItem",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GifterWaifuInfoId",
                table: "WaifuItem",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Immune",
                table: "WaifuInfo",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Info",
                table: "WaifuInfo",
                nullable: true);

            migrationBuilder.AddColumn<ulong>(
                name: "LastReputation",
                table: "WaifuInfo",
                nullable: false,
                defaultValue: 0ul);

            migrationBuilder.AddColumn<int>(
                name: "Reputation",
                table: "WaifuInfo",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ClubInvetsAmount",
                table: "DiscordUser",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ClubXp",
                table: "DiscordUser",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "XpCardImage",
                table: "DiscordUser",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<ulong>(
                name: "XpCardRole",
                table: "DiscordUser",
                nullable: false,
                defaultValue: 0ul);

            migrationBuilder.AddColumn<int>(
                name: "Currency",
                table: "Clubs",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Members",
                table: "Clubs",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalCurrency",
                table: "Clubs",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "XpImageUrl",
                table: "Clubs",
                nullable: true);

            migrationBuilder.AddColumn<ulong>(
                name: "roleId",
                table: "Clubs",
                nullable: false,
                defaultValue: 0ul);

            migrationBuilder.AddColumn<ulong>(
                name: "textId",
                table: "Clubs",
                nullable: false,
                defaultValue: 0ul);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClubsReset",
                table: "BotConfig",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "MinimumLevel",
                table: "BotConfig",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "EventSchedule",
                columns: table => new
                {
                    DateAdded = table.Column<DateTime>(nullable: true),
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GuildId = table.Column<ulong>(nullable: false),
                    UserId = table.Column<ulong>(nullable: false),
                    Date = table.Column<DateTime>(nullable: false),
                    Type = table.Column<string>(nullable: true),
                    Description = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventSchedule", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModLog",
                columns: table => new
                {
                    DateAdded = table.Column<DateTime>(nullable: true),
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GuildId = table.Column<ulong>(nullable: false),
                    UserId = table.Column<ulong>(nullable: false),
                    Reason = table.Column<string>(nullable: true),
                    Type = table.Column<string>(nullable: true),
                    Moderator = table.Column<ulong>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RepLog",
                columns: table => new
                {
                    DateAdded = table.Column<DateTime>(nullable: true),
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<ulong>(nullable: false),
                    FromId = table.Column<ulong>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "XpCards",
                columns: table => new
                {
                    DateAdded = table.Column<DateTime>(nullable: true),
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(nullable: true),
                    RoleId = table.Column<ulong>(nullable: false),
                    Image = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XpCards", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventSchedule_Description",
                table: "EventSchedule",
                column: "Description");

            migrationBuilder.CreateIndex(
                name: "IX_EventSchedule_GuildId",
                table: "EventSchedule",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_EventSchedule_Type",
                table: "EventSchedule",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_EventSchedule_UserId",
                table: "EventSchedule",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ModLog_DateAdded",
                table: "ModLog",
                column: "DateAdded");

            migrationBuilder.CreateIndex(
                name: "IX_ModLog_GuildId",
                table: "ModLog",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_ModLog_UserId",
                table: "ModLog",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RepLog_FromId",
                table: "RepLog",
                column: "FromId");

            migrationBuilder.CreateIndex(
                name: "IX_RepLog_UserId",
                table: "RepLog",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_XpCards_Name",
                table: "XpCards",
                column: "Name");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventSchedule");

            migrationBuilder.DropTable(
                name: "ModLog");

            migrationBuilder.DropTable(
                name: "RepLog");

            migrationBuilder.DropTable(
                name: "XpCards");

            migrationBuilder.DropColumn(
                name: "Count",
                table: "WaifuItem");

            migrationBuilder.DropColumn(
                name: "GifterWaifuInfoId",
                table: "WaifuItem");

            migrationBuilder.DropColumn(
                name: "Immune",
                table: "WaifuInfo");

            migrationBuilder.DropColumn(
                name: "Info",
                table: "WaifuInfo");

            migrationBuilder.DropColumn(
                name: "LastReputation",
                table: "WaifuInfo");

            migrationBuilder.DropColumn(
                name: "Reputation",
                table: "WaifuInfo");

            migrationBuilder.DropColumn(
                name: "ClubInvetsAmount",
                table: "DiscordUser");

            migrationBuilder.DropColumn(
                name: "ClubXp",
                table: "DiscordUser");

            migrationBuilder.DropColumn(
                name: "XpCardImage",
                table: "DiscordUser");

            migrationBuilder.DropColumn(
                name: "XpCardRole",
                table: "DiscordUser");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Clubs");

            migrationBuilder.DropColumn(
                name: "Members",
                table: "Clubs");

            migrationBuilder.DropColumn(
                name: "TotalCurrency",
                table: "Clubs");

            migrationBuilder.DropColumn(
                name: "XpImageUrl",
                table: "Clubs");

            migrationBuilder.DropColumn(
                name: "roleId",
                table: "Clubs");

            migrationBuilder.DropColumn(
                name: "textId",
                table: "Clubs");

            migrationBuilder.DropColumn(
                name: "ClubsReset",
                table: "BotConfig");

            migrationBuilder.DropColumn(
                name: "MinimumLevel",
                table: "BotConfig");
        }
    }
}
