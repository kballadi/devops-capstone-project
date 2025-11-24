using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LogiTrack.Models;
using Microsoft.AspNetCore.Authorization;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Manager")]
public class OrderController : ControllerBase
{
    private readonly LogiTrackContext _db;

    public OrderController(LogiTrackContext db)
    {
        _db = db;
    }

    // Controller actions would go here
    [HttpGet]
    public async Task<IActionResult> GetOrders()
    {
        // Use AsNoTracking for read-only queries + Include to load related items in single query (eager loading)
        var orders = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .ToListAsync();
        return Ok(orders);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetOrder(int id)
    {
        // AsNoTracking for read-only queries + Include for eager loading relationships
        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderId == id);
        
        if (order == null)
        {
            return NotFound(new { message = $"Order {id} not found" });
        }
        
        return Ok(order);
    }

    [HttpPost]
    public async Task<IActionResult> AddOrder([FromBody] Order order)
    {
        if (order == null)
        {
            return BadRequest("Order is null.");
        }

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetOrder), new { id = order.OrderId }, order);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteOrder(int id)    
    {
        // Use FirstOrDefaultAsync for explicit query control instead of FindAsync
        var order = await _db.Orders
            .FirstOrDefaultAsync(o => o.OrderId == id);
        if (order == null)
        {
            return NotFound();
        }

        _db.Orders.Remove(order);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}