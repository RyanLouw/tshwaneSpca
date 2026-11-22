using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TshwaneSPCA.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialPublicServices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Announcements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Slug = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    Body = table.Column<string>(type: "TEXT", nullable: false),
                    Location = table.Column<string>(type: "TEXT", nullable: true),
                    ImagePath = table.Column<string>(type: "TEXT", nullable: true),
                    PublishedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    StartsAt = table.Column<long>(type: "INTEGER", nullable: true),
                    EndsAt = table.Column<long>(type: "INTEGER", nullable: true),
                    ArchivedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Announcements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Enquiries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    Phone = table.Column<string>(type: "TEXT", nullable: true),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    ConsentVersion = table.Column<string>(type: "TEXT", nullable: false),
                    ReceivedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Enquiries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Outbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Payload = table.Column<string>(type: "TEXT", nullable: false),
                    NextAttemptAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ExpiresAt = table.Column<long>(type: "INTEGER", nullable: true),
                    CompletedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    FailedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Attempts = table.Column<int>(type: "INTEGER", nullable: false),
                    LeaseId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LeaseUntil = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Outbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Subscribers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    NormalizedEmail = table.Column<string>(type: "TEXT", nullable: false),
                    ConsentVersion = table.Column<string>(type: "TEXT", nullable: false),
                    ConsentAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ConfirmedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    UnsubscribedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    ConfirmationHash = table.Column<string>(type: "TEXT", nullable: true),
                    ConfirmationExpiresAt = table.Column<long>(type: "INTEGER", nullable: true),
                    UnsubscribeHash = table.Column<string>(type: "TEXT", nullable: true),
                    Version = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscribers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_Slug",
                table: "Announcements",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_CompletedAt_NextAttemptAt",
                table: "Outbox",
                columns: new[] { "CompletedAt", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Subscribers_ConfirmationHash",
                table: "Subscribers",
                column: "ConfirmationHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subscribers_NormalizedEmail",
                table: "Subscribers",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subscribers_UnsubscribeHash",
                table: "Subscribers",
                column: "UnsubscribeHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Announcements");

            migrationBuilder.DropTable(
                name: "Enquiries");

            migrationBuilder.DropTable(
                name: "Outbox");

            migrationBuilder.DropTable(
                name: "Subscribers");
        }
    }
}
