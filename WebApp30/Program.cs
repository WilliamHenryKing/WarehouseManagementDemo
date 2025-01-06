using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => { });

builder.Services.AddDbContext<WarehouseContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection") ??
        "Server=LAPTOP-KRUSRHFJ\\SQLEXPRESS;Database=Demo;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True"));

builder.Services.AddCors(options => {
    options.AddPolicy("AllowAll",
        policy => policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});

var app = builder.Build();

app.UseCors("AllowAll");

if (app.Environment.IsDevelopment() || app.Environment.IsStaging() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Warehouse API V1");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new ErrorResponse("An unexpected error occurred. Please try again later."));
    }
});

app.MapGet("/products", async (WarehouseContext db) =>
{
    var products = await db.Products.ToListAsync();
    return Results.Ok(products);
})
.WithName("GetProducts")
.WithTags("Products")
.Produces<List<Product>>(StatusCodes.Status200OK)
.Produces<ErrorResponse>(StatusCodes.Status500InternalServerError);

app.MapPost("/products", async (WarehouseContext db, Product product) =>
{
    if (string.IsNullOrWhiteSpace(product.Code))
        return Results.BadRequest(new ErrorResponse("Product code is required"));

    if (product.Code.Length > 20)
        return Results.BadRequest(new ErrorResponse("Product code cannot exceed 20 characters"));

    if (!Regex.IsMatch(product.Code, "^[A-Za-z0-9\\-]+$"))
        return Results.BadRequest(new ErrorResponse("Product code can only contain letters, numbers, and hyphens"));

    if (await db.Products.AnyAsync(p => p.Code == product.Code))
        return Results.BadRequest(new ErrorResponse("Product code must be unique"));

    if (product.Description?.Length > 500)
        return Results.BadRequest(new ErrorResponse("Description cannot exceed 500 characters"));

    product.Code = product.Code.Trim();
    product.Description = product.Description?.Trim();

    db.Products.Add(product);
    await db.SaveChangesAsync();
    return Results.Created($"/products/{product.ProductId}", product);
})
.WithName("CreateProduct")
.WithTags("Products")
.Produces<Product>(StatusCodes.Status201Created)
.Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

app.MapGet("/warehouses", async (WarehouseContext db) =>
{
    var warehouses = await db.Warehouses.ToListAsync();
    return Results.Ok(warehouses);
})
.WithName("GetWarehouses")
.WithTags("Warehouses")
.Produces<List<Warehouse>>(StatusCodes.Status200OK)
.Produces<ErrorResponse>(StatusCodes.Status500InternalServerError);

app.MapPost("/warehouses", async (WarehouseContext db, Warehouse warehouse) =>
{
    if (string.IsNullOrWhiteSpace(warehouse.Code))
        return Results.BadRequest(new ErrorResponse("Warehouse code is required"));

    if (warehouse.Code.Length > 7)
        return Results.BadRequest(new ErrorResponse("Warehouse code cannot exceed 7 characters"));

    if (!Regex.IsMatch(warehouse.Code, "^[A-Z]{3}-\\d{3}$"))
        return Results.BadRequest(new ErrorResponse("Warehouse code must be in format 'XXX-000'"));

    if (string.IsNullOrWhiteSpace(warehouse.Name))
        return Results.BadRequest(new ErrorResponse("Warehouse name is required"));

    if (warehouse.Name.Length > 100)
        return Results.BadRequest(new ErrorResponse("Warehouse name cannot exceed 100 characters"));

    if (await db.Warehouses.AnyAsync(w => w.Code == warehouse.Code))
        return Results.BadRequest(new ErrorResponse("Warehouse code must be unique"));

    warehouse.Code = warehouse.Code.Trim();
    warehouse.Name = warehouse.Name.Trim();

    db.Warehouses.Add(warehouse);
    await db.SaveChangesAsync();
    return Results.Created($"/warehouses/{warehouse.WarehouseId}", warehouse);
})
.WithName("CreateWarehouse")
.WithTags("Warehouses")
.Produces<Warehouse>(StatusCodes.Status201Created)
.Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

app.MapPost("/transfer", async (WarehouseContext db, TransferOrder order) =>
{
    var validationErrors = new Dictionary<string, string[]>();

    if (order.Quantity <= 0)
        return Results.BadRequest(new ErrorResponse("Quantity must be greater than 0"));

    var product = await db.Products.FindAsync(order.ProductId);
    var sourceWarehouse = await db.Warehouses.FindAsync(order.SourceWarehouseId);
    var destWarehouse = await db.Warehouses.FindAsync(order.DestinationWarehouseId);

    if (product == null)
        validationErrors.Add("productId", new[] { "Product not found" });
    if (sourceWarehouse == null)
        validationErrors.Add("sourceWarehouseId", new[] { "Source warehouse not found" });
    if (destWarehouse == null)
        validationErrors.Add("destinationWarehouseId", new[] { "Destination warehouse not found" });

    if (validationErrors.Any())
        return Results.BadRequest(new ErrorResponse("Validation failed", validationErrors));

    if (order.SourceWarehouseId == order.DestinationWarehouseId)
        return Results.BadRequest(new ErrorResponse("Source and destination warehouses cannot be the same"));

    var sourceInventory = await db.WarehouseInventory
        .FirstOrDefaultAsync(wi =>
            wi.WarehouseId == order.SourceWarehouseId &&
            wi.ProductId == order.ProductId);

    if (sourceInventory == null || sourceInventory.Quantity <= 0)
        return Results.BadRequest(new ErrorResponse("No inventory available at source warehouse"));

    if (sourceInventory.Quantity < order.Quantity)
        return Results.BadRequest(new ErrorResponse($"Insufficient inventory at source warehouse. Available: {sourceInventory.Quantity}"));

    try
    {
        await db.Database.BeginTransactionAsync();

        sourceInventory.Quantity -= order.Quantity;

        var destInventory = await db.WarehouseInventory
            .FirstOrDefaultAsync(wi =>
                wi.WarehouseId == order.DestinationWarehouseId &&
                wi.ProductId == order.ProductId);

        if (destInventory == null)
        {
            destInventory = new WarehouseInventory
            {
                WarehouseId = order.DestinationWarehouseId,
                ProductId = order.ProductId,
                Quantity = order.Quantity
            };
            db.WarehouseInventory.Add(destInventory);
        }
        else
        {
            if (destInventory.Quantity + order.Quantity < 0)
                return Results.BadRequest(new ErrorResponse("Transfer would exceed maximum inventory limit"));

            destInventory.Quantity += order.Quantity;
        }

        db.TransferOrders.Add(order);
        await db.SaveChangesAsync();
        await db.Database.CommitTransactionAsync();

        return Results.Created($"/transfer/{order.OrderId}", order);
    }
    catch
    {
        await db.Database.RollbackTransactionAsync();
        throw;
    }
})
.WithName("CreateTransferOrder")
.WithTags("TransferOrders")
.Produces<TransferOrder>(StatusCodes.Status201Created)
.Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
.Produces<ErrorResponse>(StatusCodes.Status500InternalServerError);

app.MapGet("/inventory", async (WarehouseContext db, string? productCode, string? warehouseCode) =>
{

    var query = from wi in db.WarehouseInventory
                join p in db.Products on wi.ProductId equals p.ProductId
                join w in db.Warehouses on wi.WarehouseId equals w.WarehouseId
                select new
                {
                    WarehouseCode = w.Code,
                    ProductCode = p.Code,
                    Quantity = wi.Quantity
                };

    if (!string.IsNullOrWhiteSpace(productCode))
    {
        query = query.Where(x => x.ProductCode == productCode);
    }
    if (!string.IsNullOrWhiteSpace(warehouseCode))
    {
        query = query.Where(x => x.WarehouseCode == warehouseCode);
    }

    var inventory = await query.ToListAsync();
    return Results.Ok(inventory);
})
.WithName("GetInventory")
.WithTags("Inventory")
.Produces<List<object>>(StatusCodes.Status200OK);

app.Run();
public record ErrorResponse(string Message, Dictionary<string, string[]>? ValidationErrors = null);
public record InventoryResponse(string WarehouseCode, string ProductCode, int Quantity);