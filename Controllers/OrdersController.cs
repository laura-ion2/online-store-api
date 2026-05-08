using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnlineStoreAPI.Data;
using OnlineStoreAPI.DTOs;
using OnlineStoreAPI.Models;

namespace OnlineStoreAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] 
    
    public class OrdersController: ControllerBase 
    {
        private readonly ApplicationDbContext _context;

        public OrdersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/orders/{customer_id}
        [HttpGet("{customer_id}")]
        public async Task<ActionResult<IEnumerable<OrderDTO>>> GetCustomerOrders(string customer_id)
        {
            // Verify that the requesting user can access these orders
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (currentUserId != customer_id)
                return Forbid();

            var orders = await _context.Orders
                .Include(o => o.Items)
                .ThenInclude(oi => oi.Product)
                .Where(o => o.CustomerId == customer_id)
                .OrderByDescending(o => o.OrderDate)
                .Select(o => new OrderDTO
                {
                    Id = o.Id,
                    CustomerId = o.CustomerId,
                    TotalAmount = o.TotalAmount,
                    Status = o.Status.ToString(),
                    ShippingAddress = o.ShippingAddress,
                    OrderDate = o.OrderDate,
                    Items = o.Items.Select(oi => new OrderItemDTO
                    {
                        ProductId = oi.ProductId,
                        ProductName = oi.Product.Name,
                        Quantity = oi.Quantity,
                        UnitPrice = oi.UnitPrice,
                        Subtotal = oi.Subtotal
                    }).ToList()
                })
                .ToListAsync();

            return Ok(orders);
        }

        // GET: api/orders/order/{order_id}
        [HttpGet("order/{order_id}")]
        public async Task<ActionResult<OrderDTO>> GetOrder(int order_id)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == order_id);

            if (order == null)
                return NotFound();

            // Verify that the requesting user can access this order
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (currentUserId != order.CustomerId)
                return Forbid();

            var orderDto = new OrderDTO
            {
                Id = order.Id,
                CustomerId = order.CustomerId,
                TotalAmount = order.TotalAmount,
                Status = order.Status.ToString(),
                ShippingAddress = order.ShippingAddress,
                OrderDate = order.OrderDate,
                Items = order.Items.Select(oi => new OrderItemDTO
                {
                    ProductId = oi.ProductId,
                    ProductName = oi.Product.Name,
                    Quantity = oi.Quantity,
                    UnitPrice = oi.UnitPrice,
                    Subtotal = oi.Subtotal
                }).ToList()
            };

            return Ok(orderDto);
        }

        // POST: api/orders
        [HttpPost]
        public async Task<ActionResult<OrderDTO>> CreateOrder([FromBody] CreateOrderDTO createOrderDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized();

            // Get all cart items for the current user
            var cartItems = await _context.CartItems
                .Include(ci => ci.Product)
                .Where(ci => ci.CustomerId == currentUserId)
                .ToListAsync();

            if (!cartItems.Any())
                return BadRequest("Cart is empty.");

            // Validate stock availability
            foreach (var cartItem in cartItems)
            {
                if (cartItem.Product.StockQuantity < cartItem.Quantity)
                {
                    return BadRequest($"Insufficient stock for product: {cartItem.Product.Name}. Available: {cartItem.Product.StockQuantity}, Requested: {cartItem.Quantity}");
                }
            }

            // Create order
            var order = new Order
            {
                CustomerId = currentUserId,
                TotalAmount = cartItems.Sum(ci => ci.UnitPrice * ci.Quantity),
                Status = OrderStatus.Pending,
                ShippingAddress = createOrderDto.ShippingAddress,
                OrderDate = DateTime.UtcNow
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // Create order items and update product stock
            var orderItems = new List<OrderItem>();
            foreach (var cartItem in cartItems)
            {
                var orderItem = new OrderItem
                {
                    OrderId = order.Id,
                    ProductId = cartItem.ProductId,
                    Quantity = cartItem.Quantity,
                    UnitPrice = cartItem.UnitPrice,
                    Subtotal = cartItem.UnitPrice * cartItem.Quantity
                };

                orderItems.Add(orderItem);

                // Update product stock
                cartItem.Product.StockQuantity -= cartItem.Quantity;
            }

            _context.OrderItems.AddRange(orderItems);

            // Clear the cart
            _context.CartItems.RemoveRange(cartItems);

            await _context.SaveChangesAsync();

            // Return order details
            var orderDto = new OrderDTO
            {
                Id = order.Id,
                CustomerId = order.CustomerId,
                TotalAmount = order.TotalAmount,
                Status = order.Status.ToString(),
                ShippingAddress = order.ShippingAddress,
                OrderDate = order.OrderDate,
                Items = orderItems.Select(oi => new OrderItemDTO
                {
                    ProductId = oi.ProductId,
                    ProductName = cartItems.First(ci => ci.ProductId == oi.ProductId).Product.Name,
                    Quantity = oi.Quantity,
                    UnitPrice = oi.UnitPrice,
                    Subtotal = oi.Subtotal
                }).ToList()
            };

            return CreatedAtAction(nameof(GetOrder), new { order_id = order.Id }, orderDto);
        }

        // PUT: api/orders/{order_id}/status
        [HttpPut("{order_id}/status")]
        public async Task<IActionResult> UpdateOrderStatus(int order_id, [FromBody] UpdateOrderStatusDTO updateStatusDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var order = await _context.Orders.FindAsync(order_id);
            if (order == null)
                return NotFound();

            // Verify that the requesting user can modify this order
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (currentUserId != order.CustomerId)
                return Forbid();

            // Parse the status
            if (!Enum.TryParse<OrderStatus>(updateStatusDto.Status, out var newStatus))
                return BadRequest("Invalid order status.");

            // Only allow certain status transitions for customers
            if (order.Status == OrderStatus.Pending && newStatus == OrderStatus.Cancelled)
            {
                // Customer can cancel pending orders
                order.Status = newStatus;
                order.UpdatedAt = DateTime.UtcNow;

                // If cancelling, restore product stock
                var orderItems = await _context.OrderItems
                    .Include(oi => oi.Product)
                    .Where(oi => oi.OrderId == order_id)
                    .ToListAsync();

                foreach (var item in orderItems)
                {
                    item.Product.StockQuantity += item.Quantity;
                }

                await _context.SaveChangesAsync();
                return Ok(new { Message = "Order cancelled successfully." });
            }
            else
            {
                return BadRequest("Invalid status transition. Only pending orders can be cancelled by customers.");
            }
        }
    }

    public class UpdateOrderStatusDTO
    {
        public string Status { get; set; } = string.Empty;
    }
}    
