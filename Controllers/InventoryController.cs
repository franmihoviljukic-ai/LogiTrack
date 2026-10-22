using LogiTrack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

[Authorize]
[ApiController]
[Route("api/inventory")]
public class InventoryController : ControllerBase
{
    private readonly LogiTrackContext _context;
    private readonly IMemoryCache _cache;

    private const string InventoryCacheKey = "inventory:list";

    public InventoryController(
        LogiTrackContext context,
        IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    [HttpGet]
    public async Task<IActionResult> GetInventory()
    {
        var items = await _cache.GetOrCreateAsync(
            InventoryCacheKey,
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow =
                    TimeSpan.FromSeconds(30);

                return await _context.InventoryItems
                    .AsNoTracking()
                    .ToListAsync();
            });

        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> AddInventory(InventoryItem item)
    {
        if (item.ItemId != 0)
            ModelState.AddModelError(nameof(item.ItemId),
                "A new inventory item must have ID 0.");

        if (string.IsNullOrWhiteSpace(item.Name))
            ModelState.AddModelError(nameof(item.Name),
                "Item name is required.");

        if (string.IsNullOrWhiteSpace(item.Location))
            ModelState.AddModelError(nameof(item.Location),
                "Location is required.");

        if (item.Quantity < 0)
            ModelState.AddModelError(nameof(item.Quantity),
                "Quantity cannot be negative.");

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        _context.InventoryItems.Add(item);
        await _context.SaveChangesAsync();

        _cache.Remove(InventoryCacheKey);

        return StatusCode(StatusCodes.Status201Created, item);
    }

    [Authorize(Roles = "Manager")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteInventory(int id)
    {
        var item = await _context.InventoryItems.FindAsync(id);

        if (item == null)
            return NotFound();

        _context.InventoryItems.Remove(item);
        await _context.SaveChangesAsync();

        _cache.Remove(InventoryCacheKey);

        return NoContent();
    }
}