using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace VirtoCommerce.SystemOperations.Tests
{
    public class ExporterTestEntity
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    public class ExporterTestDbContext : DbContext
    {
        public ExporterTestDbContext(DbContextOptions<ExporterTestDbContext> options)
            : base(options)
        {
        }

        public DbSet<ExporterTestEntity> Items { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ExporterTestEntity>(b =>
            {
                b.ToTable("ExporterTestItems");
                b.HasKey(x => x.Id);
                b.Property(x => x.Id).HasColumnType("nvarchar(128)");
                b.Property(x => x.Name).HasColumnType("nvarchar(max)");
            });
        }
    }

    /// <summary>
    /// Hand-written migration with explicit column types so idempotent SQL generation is independent of a
    /// design-time model snapshot.
    /// </summary>
    [DbContextAttribute(typeof(ExporterTestDbContext))]
    [Migration("20240101000000_InitialExporterTest")]
    public class InitialExporterTest : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExporterTestItems",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(128)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExporterTestItems", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ExporterTestItems");
        }
    }
}
