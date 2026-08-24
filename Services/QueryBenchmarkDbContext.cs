using Microsoft.EntityFrameworkCore;
using OutOfMemoryWorkbook.Models;

namespace OutOfMemoryWorkbook.Services;

public sealed class QueryBenchmarkDbContext(
    DbContextOptions<QueryBenchmarkDbContext> options) : DbContext(options)
{
    public DbSet<EstoqueQueryBenchmark> Estoques => Set<EstoqueQueryBenchmark>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var estoque = modelBuilder.Entity<EstoqueQueryBenchmark>();
        estoque.ToTable("Estoques");
        estoque.HasKey(item => item.Id);
        estoque.Property(item => item.Codigo).HasMaxLength(20);
        estoque.Property(item => item.Descricao).HasMaxLength(120);
        estoque.Property(item => item.Status).HasConversion<int>();
        estoque.Property(item => item.CustoUnitario).HasPrecision(18, 2);
        estoque.HasIndex(item => item.Status);
    }
}
