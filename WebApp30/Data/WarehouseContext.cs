using Microsoft.EntityFrameworkCore;

public class WarehouseContext : DbContext
{
    public WarehouseContext(DbContextOptions<WarehouseContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<WarehouseInventory> WarehouseInventory => Set<WarehouseInventory>();
    public DbSet<TransferOrder> TransferOrders => Set<TransferOrder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Products
        modelBuilder.Entity<Product>()
            .HasKey(p => p.ProductId);
        modelBuilder.Entity<Product>()
            .HasIndex(p => p.Code)
            .IsUnique();
        modelBuilder.Entity<Product>()
            .Property(p => p.Code)
            .IsRequired();

        // Warehouses
        modelBuilder.Entity<Warehouse>()
            .HasKey(w => w.WarehouseId);
        modelBuilder.Entity<Warehouse>()
            .HasIndex(w => w.Code)
            .IsUnique();
        modelBuilder.Entity<Warehouse>()
            .Property(w => w.Code)
            .IsRequired();
        modelBuilder.Entity<Warehouse>()
            .Property(w => w.Name)
            .IsRequired();

        // WarehouseInventory
        modelBuilder.Entity<WarehouseInventory>()
            .HasKey(wi => new { wi.WarehouseId, wi.ProductId });
        modelBuilder.Entity<WarehouseInventory>()
            .Property(wi => wi.Quantity)
            .HasDefaultValue(0);

        // TransferOrders
        modelBuilder.Entity<TransferOrder>()
            .HasKey(to => to.OrderId);
        modelBuilder.Entity<TransferOrder>()
            .HasCheckConstraint("CK_TransferOrder_Quantity", "[Quantity] > 0");
        modelBuilder.Entity<TransferOrder>()
            .HasCheckConstraint("CK_TransferOrder_SourceDest", "[SourceWarehouseId] <> [DestinationWarehouseId]");

        base.OnModelCreating(modelBuilder);
    }
}