using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineStoreAPI.Models
{
    public class CartItem
    {
        [Key] public int Id { get; set; }

        [Required] public string CustomerId { get; set; } = string.Empty;

        [Required] public int ProductId { get; set; }
        public Product? Product { get; set; }

        [Required] public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")] public decimal UnitPrice { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}