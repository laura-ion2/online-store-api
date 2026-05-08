using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnlineStoreAPI.Data;
using OnlineStoreAPI.Models;
using OnlineStoreAPI.DTOs;

namespace OnlineStoreAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    
    public class CartController: ControllerBase 
   {
        private readonly ApplicationDbContext _context;

        public CartController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/cart/{customer_id}
        [HttpGet("{customer_id}")]
        public async Task<ActionResult<IEnumerable<CartItemDTO>>> GetCart(string customer_id)
        {
            // Optional: Verify that the requesting user can access this cart
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (currentUserId != customer_id)
                return Forbid();

            var cartItems = await _context.CartItems
                .Include(ci => ci.Product)
                .Where(ci => ci.CustomerId == customer_id)
                .Select(ci => new CartItemDTO
                {
                    Id = ci.Id,
                    ProductId = ci.ProductId,
                    ProductName = ci.Product.Name,
                    UnitPrice = ci.UnitPrice,
                    Quantity = ci.Quantity,
                    Subtotal = ci.UnitPrice * ci.Quantity
                })
                .ToListAsync();

            return Ok(cartItems);
        }   

        // POST: api/cart/{customer_id}/add
        [HttpPost("{customer_id}/add")]
        public async Task<IActionResult> AddToCart(string customer_id, [FromBody] AddToCartDTO addToCartDto)
        {
            // Verify that the requesting user can modify this cart
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;


            if (currentUserId != customer_id)
                return Forbid();

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Verify product exists and is active
            var product = await _context.Products.FindAsync(addToCartDto.ProductId);
            if (product == null || !product.IsActive)
                return BadRequest("Product not found or inactive.");

            // Check if product has sufficient stock
            if (product.StockQuantity < addToCartDto.Quantity)
                return BadRequest("Insufficient stock available.");

            // Check if item already exists in cart
            var existingCartItem = await _context.CartItems
                .FirstOrDefaultAsync(ci => ci.CustomerId == customer_id && ci.ProductId == addToCartDto.ProductId);

            if (existingCartItem != null)
            {
                // Update quantity if item already exists
                var newQuantity = existingCartItem.Quantity + addToCartDto.Quantity;
                
                if (product.StockQuantity < newQuantity)
                    return BadRequest("Insufficient stock available for total quantity requested.");

                existingCartItem.Quantity = newQuantity;
                existingCartItem.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                // Add new item to cart
                var cartItem = new CartItem
                {
                    CustomerId = customer_id,
                    ProductId = addToCartDto.ProductId,
                    Quantity = addToCartDto.Quantity,
                    UnitPrice = product.Price,
                    CreatedAt = DateTime.UtcNow
                };

                _context.CartItems.Add(cartItem);
            }

            await _context.SaveChangesAsync();

            return Ok(new { Message = "Product added to cart successfully." });
        }

        // POST: api/cart/{customer_id}/remove
        [HttpPost("{customer_id}/remove")]
        public async Task<IActionResult> RemoveFromCart(string customer_id, [FromBody] RemoveFromCartDTO removeFromCartDto)
        {
            // Verify that the requesting user can modify this cart
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;


            if (currentUserId != customer_id)
                return Forbid();

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var cartItem = await _context.CartItems
                .FirstOrDefaultAsync(ci => ci.Id == removeFromCartDto.CartItemId && ci.CustomerId == customer_id);

            if (cartItem == null)
                return NotFound("Cart item not found.");

            _context.CartItems.Remove(cartItem);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Product removed from cart successfully." });
        }

        // POST: api/cart/{customer_id}/checkout
        [HttpPost("{customer_id}/checkout")]
        public async Task<ActionResult<OrderDTO>> Checkout(string customer_id, [FromBody] CreateOrderDTO createOrderDto)
        {
            // Verify that the requesting user can checkout this cart
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;


            if (currentUserId != customer_id)
                return Forbid();

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Get all cart items for the customer
            var cartItems = await _context.CartItems
                .Include(ci => ci.Product)
                .Where(ci => ci.CustomerId == customer_id)
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
                CustomerId = customer_id,
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

            return CreatedAtAction("GetOrder", "Orders", new { order_id = order.Id }, orderDto);
        }

        // GET: api/cart/{customer_id}/total
        [HttpGet("{customer_id}/total")]
        public async Task<ActionResult<decimal>> GetCartTotal(string customer_id)
        {
            // Verify that the requesting user can access this cart
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;


            if (currentUserId != customer_id)
                return Forbid();

            var total = await _context.CartItems
                .Where(ci => ci.CustomerId == customer_id)
                .SumAsync(ci => ci.UnitPrice * ci.Quantity);

            return Ok(new { Total = total });
        }
    }
}