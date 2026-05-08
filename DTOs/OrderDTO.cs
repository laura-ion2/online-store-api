namespace OnlineStoreAPI.DTOs
{ 
    public class OrderDTO
    {
        public int Id { get; set; }
        public string CustomerId { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<OrderItemDTO> Items { get; set; } = new List<OrderItemDTO>();
        public string? ShippingAddress { get; set; }
        public DateTime OrderDate { get; set; }
    }

    public class OrderItemDTO
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal { get; set; }
    }

    public class CreateOrderDTO
    {
        public string? ShippingAddress { get; set; }
    }
}