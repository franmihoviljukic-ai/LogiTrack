using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LogiTrack.Models;
using System.Diagnostics;

[Authorize]
[ApiController]
[Route("api/orders")]
public class OrderController : ControllerBase
{
    private readonly LogiTrackContext _context;
    private readonly IMemoryCache _cache;

    public OrderController(
        LogiTrackContext context,
        IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    [HttpGet]
    public async Task<IActionResult> GetOrders()
    {
        var stopwatch = Stopwatch.StartNew();

        var orders = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .ToListAsync();

        stopwatch.Stop();
        Console.WriteLine($"GetOrders: {orders.Count} orders fetched in {stopwatch.Elapsed.TotalMilliseconds:F2} ms.");

        return Ok(orders);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetOrder(int id)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderId == id);

        if (order == null)
            return NotFound();

        return Ok(order);
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] Order order)
    {
        if (order == null || order.OrderId != 0 || order.Items == null ||
            order.Items.Any(item => item == null || item.ItemId < 0))
            return BadRequest("Provide a new order with valid inventory items.");

        if (string.IsNullOrWhiteSpace(order.CustomerName))
            ModelState.AddModelError(nameof(order.CustomerName),"Customer name is required.");

        if (order.DatePlaced == default)
            ModelState.AddModelError(nameof(order.DatePlaced),
                "Order date is required.");

        for (var i = 0; i < order.Items.Count; i++)
        {
            var item = order.Items[i];

            // Existing items are loaded from the database using their ID.
            if (item.ItemId > 0)
                continue;

            if (string.IsNullOrWhiteSpace(item.Name))
                ModelState.AddModelError($"Items[{i}].Name",
                    "Item name is required.");

            if (string.IsNullOrWhiteSpace(item.Location))
                ModelState.AddModelError($"Items[{i}].Location",
                    "Location is required.");

            if (item.Quantity < 0)
                ModelState.AddModelError($"Items[{i}].Quantity",
                    "Quantity cannot be negative.");
        }

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var existingIds = order.Items.Where(item => item.ItemId > 0)
            .Select(item => item.ItemId).ToList();

        if (existingIds.Count != existingIds.Distinct().Count())
            return BadRequest("An inventory item cannot be included more than once.");

        var existingItems = await _context.InventoryItems
            .Where(item => existingIds.Contains(item.ItemId))
            .ToListAsync();

        if (existingItems.Count != existingIds.Count)
            return BadRequest("One or more inventory item IDs do not exist.");

        // The current schema permits each inventory item to belong to one order.
        // Reject reassignment so an existing order does not silently lose its items.
        if (await _context.InventoryItems.AnyAsync(item =>
            existingIds.Contains(item.ItemId) && EF.Property<int?>(item, "OrderId") != null))
            return Conflict("One or more inventory items already belong to an order.");

        var itemsById = existingItems.ToDictionary(item => item.ItemId);
        order.Items = order.Items.Select(item =>
            item.ItemId == 0 ? item : itemsById[item.ItemId]).ToList();

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        _cache.Remove("inventory:list");

        return CreatedAtAction(
            nameof(GetOrder),
            new { id = order.OrderId },
            order
        );
    }

    [Authorize(Roles = "Manager")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteOrder(int id)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderId == id);

        if (order == null)
            return NotFound();

        order.Items.Clear();

        _context.Orders.Remove(order);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
