using LogiTrack.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

public class LogiTrackContext : IdentityDbContext<ApplicationUser>
{
    public LogiTrackContext(DbContextOptions<LogiTrackContext> options)
        : base(options)
    {
    }

    public DbSet<Order> Orders { get; set; }
    public DbSet<InventoryItem> InventoryItems { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite("Data Source=logitrack.db");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Ensure Identity model is configured (sets keys, table names, etc.)
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<InventoryItem>()
            .HasOne(i => i.Order)
            .WithMany(o => o.Items)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Performance: Add indexes for frequently queried columns
        modelBuilder.Entity<InventoryItem>()
            .HasIndex(i => i.Name)
            .HasDatabaseName("IX_InventoryItem_Name");

        modelBuilder.Entity<InventoryItem>()
            .HasIndex(i => i.Quantity)
            .HasDatabaseName("IX_InventoryItem_Quantity");

        modelBuilder.Entity<InventoryItem>()
            .HasIndex(i => i.OrderId)
            .HasDatabaseName("IX_InventoryItem_OrderId");

        modelBuilder.Entity<Order>()
            .HasIndex(o => o.DatePlaced)
            .HasDatabaseName("IX_Order_DatePlaced");

        modelBuilder.Entity<Order>()
            .HasIndex(o => o.CustomerName)
            .HasDatabaseName("IX_Order_CustomerName");
    }
}