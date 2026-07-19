using ByteBazaar.Domain;
using ByteBazaar.Domain.Entities;
using ByteBazaar.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ByteBazaar.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = services.GetRequiredService<UserManager<AppUser>>();

        foreach (var role in new[] { "Admin", "Staff", "Customer" })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
        }

        const string adminEmail = "admin@bytebazaar.local";
        var admin = await userManager.FindByEmailAsync(adminEmail);
        if (admin is null)
        {
            admin = new AppUser
            {
                Id = Guid.NewGuid(),
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FullName = "ByteBazaar Admin"
            };
            var created = await userManager.CreateAsync(admin, "Admin123$");
            if (created.Succeeded)
                await userManager.AddToRoleAsync(admin, "Admin");
        }

        const string customerEmail = "customer@bytebazaar.local";
        var customer = await userManager.FindByEmailAsync(customerEmail);
        if (customer is null)
        {
            customer = new AppUser
            {
                Id = Guid.NewGuid(),
                UserName = customerEmail,
                Email = customerEmail,
                EmailConfirmed = true,
                FullName = "Demo Customer",
                Phone = "0300-1234567"
            };
            var createdCustomer = await userManager.CreateAsync(customer, "Customer123$");
            if (createdCustomer.Succeeded)
                await userManager.AddToRoleAsync(customer, "Customer");
        }

        await SeedCatalogAsync(db);
        await SeedDemoOrdersAsync(db, customer.Id);
    }

    private static async Task SeedCatalogAsync(AppDbContext db)
    {
        if (await db.Categories.AnyAsync())
            return;

        // ----- Categories -----
        Category Cat(string name, string slug, Category? parent = null, int sortOrder = 0) => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Slug = slug,
            ParentId = parent?.Id,
            ImageUrl = $"https://placehold.co/600x400?text={Uri.EscapeDataString(name)}",
            SortOrder = sortOrder,
            IsActive = true,
            MetaTitle = $"{name} | ByteBazaar",
            MetaDescription = $"Shop the best {name.ToLower()} in Pakistan at ByteBazaar."
        };

        var laptops = Cat("Laptops", "laptops", sortOrder: 1);
        var desktops = Cat("Desktops", "desktops", sortOrder: 2);
        var components = Cat("Components", "components", sortOrder: 3);
        var processors = Cat("Processors", "processors", components, 1);
        var graphicsCards = Cat("Graphics Cards", "graphics-cards", components, 2);
        var motherboards = Cat("Motherboards", "motherboards", components, 3);
        var ram = Cat("RAM", "ram", components, 4);
        var monitors = Cat("Monitors", "monitors", sortOrder: 4);
        var accessories = Cat("Accessories", "accessories", sortOrder: 5);

        db.Categories.AddRange(laptops, desktops, components, processors, graphicsCards, motherboards, ram, monitors, accessories);

        // ----- Attribute definitions -----
        AttributeDefinition Attr(Category category, string name, string code, List<string> options,
            FilterWidget widget = FilterWidget.Checkbox, int sortOrder = 0) => new()
        {
            Id = Guid.NewGuid(),
            CategoryId = category.Id,
            Name = name,
            Code = code,
            Type = AttributeType.Select,
            Options = options,
            IsFilterable = true,
            IsRequired = false,
            FilterWidget = widget,
            SortOrder = sortOrder
        };

        db.AttributeDefinitions.AddRange(
            Attr(laptops, "Processor", "processor", new List<string> { "Intel Core i5", "Intel Core i7", "Intel Core i9", "AMD Ryzen 5", "AMD Ryzen 7" }, sortOrder: 1),
            Attr(laptops, "RAM", "ram", new List<string> { "8GB", "16GB", "32GB" }, sortOrder: 2),
            Attr(laptops, "Storage", "storage", new List<string> { "256GB SSD", "512GB SSD", "1TB SSD" }, sortOrder: 3),
            Attr(laptops, "Screen Size", "screen_size", new List<string> { "13.3\"", "14\"", "15.6\"", "16\"" }, sortOrder: 4),
            Attr(desktops, "Processor", "processor", new List<string> { "Intel Core i5", "Intel Core i7", "Intel Core i9", "AMD Ryzen 5", "AMD Ryzen 7" }, sortOrder: 1),
            Attr(desktops, "RAM", "ram", new List<string> { "8GB", "16GB", "32GB" }, sortOrder: 2),
            Attr(processors, "Cores", "cores", new List<string> { "6", "8", "12", "16" }, sortOrder: 1),
            Attr(processors, "Socket", "socket", new List<string> { "LGA1700", "AM5" }, FilterWidget.Radio, 2),
            Attr(graphicsCards, "Memory", "memory", new List<string> { "8GB", "12GB", "16GB" }, sortOrder: 1),
            Attr(graphicsCards, "Chipset", "chipset", new List<string> { "NVIDIA GeForce RTX 4060", "NVIDIA GeForce RTX 4070", "AMD Radeon RX 7600", "AMD Radeon RX 7800 XT" }, sortOrder: 2),
            Attr(motherboards, "Socket", "socket", new List<string> { "LGA1700", "AM5" }, FilterWidget.Radio, 1),
            Attr(motherboards, "Form Factor", "form_factor", new List<string> { "ATX", "Micro-ATX" }, FilterWidget.Radio, 2),
            Attr(ram, "Capacity", "capacity", new List<string> { "8GB", "16GB", "32GB" }, sortOrder: 1),
            Attr(ram, "Type", "type", new List<string> { "DDR4", "DDR5" }, FilterWidget.Radio, 2),
            Attr(monitors, "Screen Size", "screen_size", new List<string> { "24\"", "27\"", "32\"" }, sortOrder: 1),
            Attr(monitors, "Refresh Rate", "refresh_rate", new List<string> { "60Hz", "144Hz", "165Hz" }, sortOrder: 2),
            Attr(monitors, "Resolution", "resolution", new List<string> { "1080p", "1440p", "4K" }, sortOrder: 3),
            Attr(accessories, "Connectivity", "connectivity", new List<string> { "Wired", "Wireless" }, FilterWidget.Radio, 1));

        // ----- Brands -----
        Brand Br(string name, string slug) => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Slug = slug,
            LogoUrl = $"https://placehold.co/200x80?text={Uri.EscapeDataString(name)}"
        };

        var asus = Br("Asus", "asus");
        var hp = Br("HP", "hp");
        var dell = Br("Dell", "dell");
        var lenovo = Br("Lenovo", "lenovo");
        var msi = Br("MSI", "msi");
        var gigabyte = Br("Gigabyte", "gigabyte");
        db.Brands.AddRange(asus, hp, dell, lenovo, msi, gigabyte);

        // ----- Products -----
        var createdAt = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        Product Prod(Category category, Brand brand, string name, string slug, decimal price, int stock,
            Dictionary<string, string> attributes, decimal? salePrice = null, string? description = null)
        {
            createdAt = createdAt.AddHours(6);
            var product = new Product
            {
                Id = Guid.NewGuid(),
                CategoryId = category.Id,
                BrandId = brand.Id,
                Name = name,
                Slug = slug,
                Description = description ?? $"{name} — quality {category.Name.ToLower()} from {brand.Name}, backed by official warranty in Pakistan.",
                Price = price,
                SalePrice = salePrice,
                SaleStart = salePrice is null ? null : createdAt,
                SaleEnd = salePrice is null ? null : createdAt.AddDays(60),
                Stock = stock,
                Status = ProductStatus.Active,
                Attributes = attributes,
                MetaTitle = $"{name} Price in Pakistan | ByteBazaar",
                MetaDescription = $"Buy {name} at the best price in Pakistan. Fast delivery nationwide.",
                CreatedAt = createdAt
            };
            product.Images.Add(new ProductImage
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                Url = $"https://placehold.co/600x400?text={Uri.EscapeDataString(name)}",
                SortOrder = 0
            });
            return product;
        }

        db.Products.AddRange(
            // Laptops (8)
            Prod(laptops, asus, "Asus VivoBook 15 X1504", "asus-vivobook-15-x1504", 165000m, 12,
                new() { ["processor"] = "Intel Core i5", ["ram"] = "8GB", ["storage"] = "512GB SSD", ["screen_size"] = "15.6\"" }, 154900m),
            Prod(laptops, asus, "Asus ROG Strix G16", "asus-rog-strix-g16", 485000m, 5,
                new() { ["processor"] = "Intel Core i9", ["ram"] = "32GB", ["storage"] = "1TB SSD", ["screen_size"] = "16\"" }),
            Prod(laptops, hp, "HP Pavilion 14", "hp-pavilion-14", 175000m, 10,
                new() { ["processor"] = "Intel Core i5", ["ram"] = "16GB", ["storage"] = "512GB SSD", ["screen_size"] = "14\"" }),
            Prod(laptops, hp, "HP Envy x360 13", "hp-envy-x360-13", 265000m, 6,
                new() { ["processor"] = "AMD Ryzen 7", ["ram"] = "16GB", ["storage"] = "1TB SSD", ["screen_size"] = "13.3\"" }, 249900m),
            Prod(laptops, dell, "Dell Inspiron 15 3530", "dell-inspiron-15-3530", 155000m, 15,
                new() { ["processor"] = "Intel Core i5", ["ram"] = "8GB", ["storage"] = "256GB SSD", ["screen_size"] = "15.6\"" }),
            Prod(laptops, dell, "Dell XPS 14", "dell-xps-14", 545000m, 3,
                new() { ["processor"] = "Intel Core i7", ["ram"] = "32GB", ["storage"] = "1TB SSD", ["screen_size"] = "14\"" }),
            Prod(laptops, lenovo, "Lenovo IdeaPad Slim 5", "lenovo-ideapad-slim-5", 185000m, 9,
                new() { ["processor"] = "AMD Ryzen 5", ["ram"] = "16GB", ["storage"] = "512GB SSD", ["screen_size"] = "14\"" }, 172500m),
            Prod(laptops, msi, "MSI Katana 15", "msi-katana-15", 345000m, 7,
                new() { ["processor"] = "Intel Core i7", ["ram"] = "16GB", ["storage"] = "1TB SSD", ["screen_size"] = "15.6\"" }),

            // Desktops (3)
            Prod(desktops, hp, "HP ProDesk 400 G9 Tower", "hp-prodesk-400-g9-tower", 195000m, 8,
                new() { ["processor"] = "Intel Core i5", ["ram"] = "16GB" }),
            Prod(desktops, dell, "Dell OptiPlex 7010 SFF", "dell-optiplex-7010-sff", 225000m, 6,
                new() { ["processor"] = "Intel Core i7", ["ram"] = "16GB" }),
            Prod(desktops, lenovo, "Lenovo Legion Tower 5", "lenovo-legion-tower-5", 425000m, 4,
                new() { ["processor"] = "AMD Ryzen 7", ["ram"] = "32GB" }, 399000m),

            // Processors (4)
            Prod(processors, msi, "Intel Core i5-14600K", "intel-core-i5-14600k", 92000m, 20,
                new() { ["cores"] = "12", ["socket"] = "LGA1700" }),
            Prod(processors, gigabyte, "Intel Core i7-14700K", "intel-core-i7-14700k", 135000m, 14,
                new() { ["cores"] = "16", ["socket"] = "LGA1700" }, 128500m),
            Prod(processors, asus, "AMD Ryzen 5 7600X", "amd-ryzen-5-7600x", 78000m, 18,
                new() { ["cores"] = "6", ["socket"] = "AM5" }),
            Prod(processors, msi, "AMD Ryzen 7 7800X3D", "amd-ryzen-7-7800x3d", 145000m, 10,
                new() { ["cores"] = "8", ["socket"] = "AM5" }),

            // Graphics cards (4)
            Prod(graphicsCards, asus, "Asus Dual GeForce RTX 4060 OC", "asus-dual-rtx-4060-oc", 125000m, 11,
                new() { ["memory"] = "8GB", ["chipset"] = "NVIDIA GeForce RTX 4060" }, 118000m),
            Prod(graphicsCards, msi, "MSI Gaming X GeForce RTX 4070", "msi-gaming-x-rtx-4070", 235000m, 6,
                new() { ["memory"] = "12GB", ["chipset"] = "NVIDIA GeForce RTX 4070" }),
            Prod(graphicsCards, gigabyte, "Gigabyte Gaming OC Radeon RX 7600", "gigabyte-gaming-oc-rx-7600", 98000m, 9,
                new() { ["memory"] = "8GB", ["chipset"] = "AMD Radeon RX 7600" }),
            Prod(graphicsCards, gigabyte, "Gigabyte Radeon RX 7800 XT", "gigabyte-rx-7800-xt", 215000m, 5,
                new() { ["memory"] = "16GB", ["chipset"] = "AMD Radeon RX 7800 XT" }, 204900m),

            // Motherboards (3)
            Prod(motherboards, asus, "Asus TUF Gaming B760-Plus WiFi", "asus-tuf-gaming-b760-plus-wifi", 62000m, 13,
                new() { ["socket"] = "LGA1700", ["form_factor"] = "ATX" }),
            Prod(motherboards, msi, "MSI PRO B650M-A WiFi", "msi-pro-b650m-a-wifi", 58000m, 12,
                new() { ["socket"] = "AM5", ["form_factor"] = "Micro-ATX" }),
            Prod(motherboards, gigabyte, "Gigabyte X670 Aorus Elite AX", "gigabyte-x670-aorus-elite-ax", 96000m, 7,
                new() { ["socket"] = "AM5", ["form_factor"] = "ATX" }, 89900m),

            // RAM (3)
            Prod(ram, gigabyte, "Gigabyte Aorus DDR5 16GB 5200MHz", "gigabyte-aorus-ddr5-16gb-5200", 21500m, 25,
                new() { ["capacity"] = "16GB", ["type"] = "DDR5" }),
            Prod(ram, asus, "Asus ROG Strix DDR5 32GB 6000MHz", "asus-rog-strix-ddr5-32gb-6000", 42500m, 16,
                new() { ["capacity"] = "32GB", ["type"] = "DDR5" }, 39900m),
            Prod(ram, msi, "MSI Spatium DDR4 16GB 3200MHz", "msi-spatium-ddr4-16gb-3200", 13500m, 30,
                new() { ["capacity"] = "16GB", ["type"] = "DDR4" }),

            // Monitors (4)
            Prod(monitors, dell, "Dell S2425H 24 Monitor", "dell-s2425h-24-monitor", 48000m, 14,
                new() { ["screen_size"] = "24\"", ["refresh_rate"] = "60Hz", ["resolution"] = "1080p" }),
            Prod(monitors, asus, "Asus TUF Gaming VG27AQ", "asus-tuf-gaming-vg27aq", 105000m, 8,
                new() { ["screen_size"] = "27\"", ["refresh_rate"] = "165Hz", ["resolution"] = "1440p" }, 98500m),
            Prod(monitors, hp, "HP OMEN 27q", "hp-omen-27q", 92000m, 10,
                new() { ["screen_size"] = "27\"", ["refresh_rate"] = "165Hz", ["resolution"] = "1440p" }),
            Prod(monitors, gigabyte, "Gigabyte M32U 4K Gaming Monitor", "gigabyte-m32u-4k", 185000m, 4,
                new() { ["screen_size"] = "32\"", ["refresh_rate"] = "144Hz", ["resolution"] = "4K" }),

            // Accessories (3)
            Prod(accessories, asus, "Asus ROG Keris Wireless Mouse", "asus-rog-keris-wireless", 14500m, 22,
                new() { ["connectivity"] = "Wireless" }),
            Prod(accessories, hp, "HP K500F Wired Gaming Keyboard", "hp-k500f-wired-keyboard", 6500m, 28,
                new() { ["connectivity"] = "Wired" }, 5900m),
            Prod(accessories, lenovo, "Lenovo Legion H300 Headset", "lenovo-legion-h300-headset", 12500m, 18,
                new() { ["connectivity"] = "Wired" }));

        await db.SaveChangesAsync();
    }

    private static async Task SeedDemoOrdersAsync(AppDbContext db, Guid customerId)
    {
        if (await db.Orders.AnyAsync())
            return;

        var products = await db.Products
            .Include(p => p.Images)
            .Where(p => p.Status == ProductStatus.Active)
            .OrderBy(p => p.Name)
            .Take(6)
            .ToListAsync();
        if (products.Count < 3)
            return;

        var sequence = 0;
        Order MakeOrder(OrderStatus status, DateTime createdAt, params (Product Product, int Quantity)[] lines)
        {
            sequence++;
            const decimal shippingFee = 250m;
            var order = new Order
            {
                Id = Guid.NewGuid(),
                OrderNumber = $"BB-{sequence:D6}",
                UserId = customerId,
                Status = status,
                PaymentMethod = PaymentMethod.COD,
                ShippingFee = shippingFee,
                ShippingCode = "standard",
                FullName = "Demo Customer",
                Phone = "0300-1234567",
                Email = "customer@bytebazaar.local",
                AddressLine = "House 12, Street 5, Gulberg III",
                City = "Lahore",
                Region = "Punjab",
                Notes = "Please call before delivery.",
                CreatedAt = createdAt
            };

            foreach (var (product, quantity) in lines)
            {
                order.Items.Add(new OrderItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    ProductId = product.Id,
                    ProductName = product.Name,
                    ProductSlug = product.Slug,
                    ImageUrl = product.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault(),
                    UnitPrice = product.SalePrice ?? product.Price,
                    Quantity = quantity
                });
            }

            order.Subtotal = order.Items.Sum(i => i.UnitPrice * i.Quantity);
            order.Total = order.Subtotal + shippingFee;

            order.History.Add(new OrderStatusHistory
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                Status = OrderStatus.Pending,
                Note = "Order placed.",
                CreatedAt = createdAt
            });
            if (status is OrderStatus.Confirmed or OrderStatus.Shipped)
            {
                order.History.Add(new OrderStatusHistory
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    Status = OrderStatus.Confirmed,
                    Note = "Order confirmed.",
                    CreatedAt = createdAt.AddHours(3)
                });
            }
            if (status == OrderStatus.Shipped)
            {
                order.History.Add(new OrderStatusHistory
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    Status = OrderStatus.Shipped,
                    Note = "Handed over to courier.",
                    CreatedAt = createdAt.AddDays(1)
                });
            }

            return order;
        }

        var now = DateTime.UtcNow;
        db.Orders.AddRange(
            MakeOrder(OrderStatus.Shipped, now.AddDays(-6), (products[0], 1), (products[1], 1)),
            MakeOrder(OrderStatus.Confirmed, now.AddDays(-2), (products[2], 2)),
            MakeOrder(OrderStatus.Pending, now.AddHours(-4), (products[3 % products.Count], 1)));

        await db.SaveChangesAsync();
    }
}
