using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LogiTrack.Models;

[ApiController]
[Route("api/[controller]")]
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
        var orders = await _db.Orders.ToListAsync();
        return Ok(orders);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetOrder(int id)
    {
        var order = await _db.Orders
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
    public async Task<IActionResult> DeleteOrder(int id)    
    {
        var order = await _db.Orders.FindAsync(id);
        if (order == null)
        {
            return NotFound();
        }

        _db.Orders.Remove(order);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}