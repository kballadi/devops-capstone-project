using LogiTrack.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Manager")]
public class InventoryController : ControllerBase
{
    private readonly LogiTrackContext _db;

    public InventoryController(LogiTrackContext db)
    {
        _db = db;
    }

    // Controller actions would go here
    [HttpGet]
    public async Task<IActionResult> GetInventoryItems()
    {
        var items = await _db.InventoryItems.ToListAsync();
        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> AddInventoryItem([FromBody] InventoryItem item)
    {
        if (item == null)
        {
            return BadRequest("Inventory item is null.");
        }

        _db.InventoryItems.Add(item);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetInventoryItems), new { id = item.ItemId }, item);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteInventoryItem(int id)
    {
        var item = await _db.InventoryItems.FindAsync(id);
        if (item == null)
        {
            return NotFound();
        }

        _db.InventoryItems.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}