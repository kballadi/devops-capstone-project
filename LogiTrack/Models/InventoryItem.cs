using System.ComponentModel.DataAnnotations;

namespace LogiTrack.Models;
public class InventoryItem
{
    [Key]
    public int ItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Location { get; set; } = string.Empty;

    // Foreign key to Order (one-to-many)
    public int? OrderId { get; set; }
    public Order? Order { get; set; }

    public void DisplayInfo()
    {
        Console.WriteLine($"Item : {Name} | Quantity: {Quantity} | Location: {Location}");
    }
}