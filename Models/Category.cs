using System.ComponentModel.DataAnnotations;

namespace OnlineStoreAPI.Models
{
    public class Category
    {
        [Key] 
        public int Id { get; set; }

        [Required] 
        [MaxLength(50)] 
        public string Name { get; set; } = string.Empty;

        [MaxLength(200)] 
        public string? Description { get; set; }

        public ICollection<Product>? Products { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}