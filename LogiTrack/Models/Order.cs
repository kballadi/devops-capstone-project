using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;

namespace LogiTrack.Models;
public class Order
{
    [Key]
    public int OrderId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public DateTime DatePlaced { get; set; }
    // Navigation collection for related InventoryItems (one-to-many)
    public List<InventoryItem> Items { get; set; } = [];

    public void AddItem(InventoryItem item)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));

        // Keep in-memory object graph consistent so EF will track correctly.
        item.Order = this;
        // If this Order already has a database id, set the FK to avoid extra tracking surprises.
        item.OrderId = this.OrderId == 0 ? item.OrderId : this.OrderId;

        Items.Add(item);
    }
    public void RemoveItem(int itemId)
    {
        Items.RemoveAll(i => i.ItemId == itemId);
    }
    // Returns a concise summary string for this order.
    // Use this when the Order instance (and its Items collection) is already loaded.
    public string ToSummary()
    {
        var itemCount = Items?.Count ?? 0;
        return $"Order #{OrderId} for {CustomerName} | Items {itemCount} | Placed {DatePlaced:yyyy-MM-dd}";
    }

    // Convenience: print the summary to console
    public void PrintSummary()
    {
        Console.WriteLine(ToSummary());
    }

    // EF-aware helper: add a single InventoryItem to an order without loading the full Items collection.
    // This avoids pulling the Order.Items navigation into memory.
    public static async Task AddItemToOrderAsync(LogiTrackContext db, int orderId, InventoryItem item, CancellationToken cancellationToken = default)
    {
        if (db == null) throw new ArgumentNullException(nameof(db));
        if (item == null) throw new ArgumentNullException(nameof(item));

        item.OrderId = orderId;
        // Do NOT set item.Order to avoid attaching the full Order entity to the change tracker.
        db.InventoryItems.Add(item);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    // EF-aware helper: add multiple items efficiently using AddRange to minimize round-trips.
    public static async Task AddItemsToOrderAsync(LogiTrackContext db, int orderId, IEnumerable<InventoryItem> items, CancellationToken cancellationToken = default)
    {
        if (db == null) throw new ArgumentNullException(nameof(db));
        if (items == null) throw new ArgumentNullException(nameof(items));

        foreach (var it in items)
        {
            it.OrderId = orderId;
        }

        db.InventoryItems.AddRange(items);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}