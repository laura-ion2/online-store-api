using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OnlineStoreAPI.Models;

namespace OnlineStoreAPI.Data
{

    public class ApplicationDbContext : IdentityDbContext<User>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Product> Products { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure relationships
            builder.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<CartItem>()
                .HasOne(ci => ci.Product)
                .WithMany()
                .HasForeignKey(ci => ci.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<OrderItem>()
                .HasOne(oi => oi.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<OrderItem>()
                .HasOne(oi => oi.Product)
                .WithMany()
                .HasForeignKey(oi => oi.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // Add indexes
            builder.Entity<CartItem>()
                .HasIndex(ci => ci.CustomerId);

            builder.Entity<Order>()
                .HasIndex(o => o.CustomerId);

            // Add some seed data
            builder.Entity<Category>().HasData(
                new Category
                {
                    Id = 1, Name = "Laptop, Tablete & Telefoane",
                    Description = "Dispozitive moderne pentru muncă, joacă și comunicare, oriunde te-ai afla."
                },
                new Category
                {
                    Id = 2, Name = "Gaming, Carti & Birotica",
                    Description = "Relaxare, lectură și organizare — jocuri captivante, cărți pentru toate gusturile și produse utile pentru biroul tău."
                },
                new Category
                {
                    Id = 3, Name = "Ingrijire personala & Cosmetice",
                    Description = "Produse esențiale pentru frumusețe și bunăstare, dedicate rutinei tale zilnice de îngrijire."
                }
            );

            builder.Entity<Product>().HasData(
                new Product
                {
                    Id = 1,
                    Name = "Laptop Auusda",
                    Description = "Procesor Centrino N95, 16GB RAM, SSD 512GB, Ecran IPS 15.6\" FHD, Intel UHD Graphics",
                    Price = 1783.54m,
                    StockQuantity = 64,
                    CategoryId = 1
                },
                new Product
                {
                    Id = 2,
                    Name = "Twisted love",
                    Description = "Alex poartă povara unei traume și setea de răzbunare, iar Ava încearcă să-și vindece rănile trecutului uitat. Când drumurile lor se intersectează, dragostea interzisă dintre ei dezgheață inimi, trezește dorințe și scoate la lumină secrete periculoase ce le-ar putea distruge lumea.",
                    Price = 49.68m,
                    StockQuantity = 82,
                    CategoryId = 2
                },
                new Product
                {
                    Id = 3,
                    Name = "Gel de spalare hidratant CeraVe",
                    Description = "Gel cu ceramide si acid hialuronic, pentru piele normal-uscata, 473 ml",
                    Price = 62.08m,
                    StockQuantity = 127,
                    CategoryId = 3
                }
            );
            
            builder.Entity<IdentityRole>().HasData(
                new IdentityRole { Id = "1", Name = "Admin", NormalizedName = "ADMIN" }
            );
        }
    }
}