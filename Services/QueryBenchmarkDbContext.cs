using Microsoft.EntityFrameworkCore;
using OutOfMemoryWorkbook.Models;

namespace OutOfMemoryWorkbook.Services;

public sealed class QueryBenchmarkDbContext(
    DbContextOptions<QueryBenchmarkDbContext> options) : DbContext(options)
{
    public DbSet<InventoryQueryBenchmark> InventoryItems => Set<InventoryQueryBenchmark>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var inventoryItem = modelBuilder.Entity<InventoryQueryBenchmark>();
        inventoryItem.ToTable("Estoques");
        inventoryItem.HasKey(item => item.Id);
        inventoryItem.Property(item => item.Code).HasColumnName("Codigo").HasMaxLength(20);
        inventoryItem.Property(item => item.Description).HasColumnName("Descricao").HasMaxLength(120);
        inventoryItem.Property(item => item.Status).HasColumnName("Status").HasConversion<int>();
        inventoryItem.Property(item => item.Quantity).HasColumnName("Quantidade");
        inventoryItem.Property(item => item.UnitCost).HasColumnName("CustoUnitario").HasPrecision(18, 2);
        inventoryItem.Property(item => item.LastMovement).HasColumnName("UltimaMovimentacao");
        inventoryItem.HasIndex(item => item.Status);
    }
}
