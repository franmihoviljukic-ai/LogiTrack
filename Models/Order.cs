using System.ComponentModel.DataAnnotations;

namespace LogiTrack.Models
{
    public class Order
    {
        [Key]
        public int OrderId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public DateTime DatePlaced { get; set; }

        public List<InventoryItem> Items { get; set; } = new();

        public void AddItem(InventoryItem item)
        {
            Items.Add(item);
        }

        public void RemoveItem(int itemId)
        {
            Items.RemoveAll(item => item.ItemId == itemId);
        }

        public string GetOrderSummary()
        {
            return $"Order #{OrderId} for {CustomerName} | Items: {Items.Count} | Placed: {DatePlaced:M/d/yyyy}";
        }
    }
}
