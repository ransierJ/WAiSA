using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WAiSA.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentsAndCommandQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Agents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ComputerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ApiKeyHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LastHeartbeat = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSystemInfo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InstallDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OsVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InstallationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Agents", x => x.Id);
                    table.UniqueConstraint("AK_Agents_AgentId", x => x.AgentId);
                });

            migrationBuilder.CreateTable(
                name: "CommandQueue",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CommandId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Command = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExecutionContext = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Output = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExecutionTimeSeconds = table.Column<double>(type: "float", nullable: true),
                    TimeoutSeconds = table.Column<int>(type: "int", nullable: false),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    InitiatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ChatSessionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RequiresApproval = table.Column<bool>(type: "bit", nullable: false),
                    Approved = table.Column<bool>(type: "bit", nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandQueue", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommandQueue_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "AgentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Agents_AgentId",
                table: "Agents",
                column: "AgentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Agents_ComputerName",
                table: "Agents",
                column: "ComputerName");

            migrationBuilder.CreateIndex(
                name: "IX_Agents_IsEnabled",
                table: "Agents",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_Agents_LastHeartbeat",
                table: "Agents",
                column: "LastHeartbeat");

            migrationBuilder.CreateIndex(
                name: "IX_Agents_Status",
                table: "Agents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CommandQueue_AgentId",
                table: "CommandQueue",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_CommandQueue_AgentId_Status",
                table: "CommandQueue",
                columns: new[] { "AgentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CommandQueue_ChatSessionId",
                table: "CommandQueue",
                column: "ChatSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_CommandQueue_CommandId",
                table: "CommandQueue",
                column: "CommandId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommandQueue_CreatedAt",
                table: "CommandQueue",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CommandQueue_Status",
                table: "CommandQueue",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommandQueue");

            migrationBuilder.DropTable(
                name: "Agents");
        }
    }
}
