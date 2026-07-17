using Bazaar.Api.Contracts;
using Bazaar.Api.Validation;
using Bazaar.Domain;
using Bazaar.Domain.Catalog;
using Bazaar.Domain.Common;
using Bazaar.Domain.Inventory;
using Bazaar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bazaar.Api.Endpoints;

public static class AdminCatalogEndpoints
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public static IEndpointRouteBuilder MapAdminCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var products = app.MapGroup("/api/admin/products").WithTags("Admin: Products").RequireAuthorization("Admin");
        products.MapGet("/", ListProducts);
        products.MapGet("/{id:guid}", GetProduct);
        products.MapPost("/", CreateProduct);
        products.MapPut("/{id:guid}", UpdateProduct);
        products.MapDelete("/{id:guid}", DeleteProduct);

        var variants = app.MapGroup("/api/admin/variants").WithTags("Admin: Variants").RequireAuthorization("Admin");
        variants.MapPut("/{id:guid}", UpdateVariant);

        var collections = app.MapGroup("/api/admin/collections").WithTags("Admin: Collections").RequireAuthorization("Admin");
        collections.MapGet("/", ListCollections);
        collections.MapPost("/", CreateCollection);
        collections.MapPut("/{id:guid}", UpdateCollection);
        collections.MapDelete("/{id:guid}", DeleteCollection);

        return app;
    }

    // ---- Products ----

    private static async Task<IResult> ListProducts(
        BazaarDbContext db, string? search, string? status, int? page, int? pageSize, CancellationToken ct)
    {
        var (pageNumber, size) = Paging.Clamp(page, pageSize, DefaultPageSize, MaxPageSize);
        var query = db.Products.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p =>
                EF.Functions.Like(p.Title, $"%{term}%") ||
                EF.Functions.Like(p.Slug, $"%{term}%"));
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ProductStatus>(status, true, out var parsed))
            query = query.Where(p => p.Status == parsed);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((pageNumber - 1) * size)
            .Take(size)
            .Select(p => new ProductSummaryDto(
                p.Id, p.Slug, p.Title, p.Vendor, p.Status.ToString(),
                p.Images.OrderBy(i => i.Position).Select(i => i.Url).FirstOrDefault(),
                p.Variants.OrderBy(v => v.Price.Amount).Select(v => new MoneyDto(v.Price.Amount, v.Price.Currency)).FirstOrDefault(),
                p.Collections.Select(c => c.Slug).ToList()))
            .ToListAsync(ct);

        return Results.Ok(new PagedResult<ProductSummaryDto>(items, pageNumber, size, total));
    }

    private static async Task<IResult> GetProduct(BazaarDbContext db, Guid id, CancellationToken ct)
    {
        var product = await LoadProductGraph(db).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null) return Results.NotFound();
        var stock = await StockMap(db, product.Variants.Select(v => v.Id).ToList(), ct);
        return Results.Ok(product.ToDetailDto(stock));
    }

    private static async Task<IResult> CreateProduct(BazaarDbContext db, CreateProductRequest request, CancellationToken ct)
    {
        var children = request.Variants.Select((v, i) => ($"variants[{i}]", (object)v))
            .Concat(request.Images.Select((im, i) => ($"images[{i}]", (object)im)));
        if (!RequestValidation.TryValidateGraph(request, children, out var errors))
            return Results.ValidationProblem(errors);

        if (!TryResolveStatus(request.Status, ProductStatus.Draft, out var status))
            return Results.ValidationProblem(OneError(nameof(request.Status), "Unknown product status."));

        if (await db.Products.AnyAsync(p => p.Slug == request.Slug, ct))
            return Results.Problem($"A product with slug '{request.Slug}' already exists.", statusCode: StatusCodes.Status409Conflict, title: "Slug already in use");

        var collections = await ResolveCollections(db, request.CollectionSlugs, ct);
        if (collections is null)
            return Results.ValidationProblem(OneError(nameof(request.CollectionSlugs), "One or more collection slugs do not exist."));

        var duplicateSku = await FindDuplicateSku(db, request.Variants.Select(v => v.Sku!), ct);
        if (duplicateSku is not null)
            return Results.Problem($"SKU '{duplicateSku}' already exists.", statusCode: StatusCodes.Status409Conflict, title: "SKU already in use");

        var product = new Product
        {
            Title = request.Title!,
            Slug = request.Slug!,
            Description = request.Description ?? string.Empty,
            Vendor = request.Vendor,
            Status = status,
        };

        foreach (var image in request.Images.OrderBy(i => i.Position))
            product.AddImage(new ProductImage { Url = image.Url!, AltText = image.AltText, Position = image.Position });

        product.SetCollections(collections);

        var inventory = new List<InventoryItem>();
        var position = 0;
        foreach (var input in request.Variants)
        {
            var variant = new ProductVariant
            {
                Sku = input.Sku!,
                Title = string.IsNullOrWhiteSpace(input.Title) ? "Default" : input.Title!,
                Price = new Money(input.Price!.Value, input.Currency ?? Money.DefaultCurrency),
                Position = position++,
            };
            foreach (var option in input.Options)
                variant.SetOption(option.Name!, option.Value!);
            product.AddVariant(variant);
            inventory.Add(new InventoryItem { VariantId = variant.Id, OnHand = input.StockOnHand });
        }

        db.Products.Add(product);
        db.InventoryItems.AddRange(inventory);
        await db.SaveChangesAsync(ct);

        var stock = inventory.ToDictionary(i => i.VariantId, i => i.Available);
        return Results.Created($"/api/admin/products/{product.Id}", product.ToDetailDto(stock));
    }

    private static async Task<IResult> UpdateProduct(BazaarDbContext db, Guid id, UpdateProductRequest request, CancellationToken ct)
    {
        var children = request.Images.Select((im, i) => ($"images[{i}]", (object)im));
        if (!RequestValidation.TryValidateGraph(request, children, out var errors))
            return Results.ValidationProblem(errors);

        var product = await db.Products
            .Include(p => p.Images)
            .Include(p => p.Variants)
            .Include(p => p.Collections)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null) return Results.NotFound();

        if (!TryResolveStatus(request.Status, product.Status, out var status))
            return Results.ValidationProblem(OneError(nameof(request.Status), "Unknown product status."));

        var collections = await ResolveCollections(db, request.CollectionSlugs, ct);
        if (collections is null)
            return Results.ValidationProblem(OneError(nameof(request.CollectionSlugs), "One or more collection slugs do not exist."));

        product.Title = request.Title!;
        product.Description = request.Description ?? string.Empty;
        product.Vendor = request.Vendor;
        product.Status = status;
        product.UpdatedAt = DateTimeOffset.UtcNow;

        product.ClearImages();
        foreach (var image in request.Images.OrderBy(i => i.Position))
            product.AddImage(new ProductImage { Url = image.Url!, AltText = image.AltText, Position = image.Position });

        product.SetCollections(collections);

        await db.SaveChangesAsync(ct);

        var stock = await StockMap(db, product.Variants.Select(v => v.Id).ToList(), ct);
        return Results.Ok(product.ToDetailDto(stock));
    }

    private static async Task<IResult> DeleteProduct(BazaarDbContext db, Guid id, CancellationToken ct)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null) return Results.NotFound();

        db.Products.Remove(product);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Results.Problem(
                "This product cannot be deleted because it is referenced by existing carts or orders.",
                statusCode: StatusCodes.Status409Conflict, title: "Product in use");
        }

        return Results.NoContent();
    }

    // ---- Variants ----

    private static async Task<IResult> UpdateVariant(BazaarDbContext db, Guid id, UpdateVariantRequest request, CancellationToken ct)
    {
        if (!RequestValidation.TryValidate(request, out var errors))
            return Results.ValidationProblem(errors);

        var variant = await db.Variants.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (variant is null) return Results.NotFound();

        if (!string.IsNullOrWhiteSpace(request.Title))
            variant.Title = request.Title!;
        variant.Price = new Money(request.Price!.Value, request.Currency ?? variant.Price.Currency);

        var inventory = await db.InventoryItems.FirstOrDefaultAsync(i => i.VariantId == id, ct);
        if (request.StockOnHand is { } onHand)
        {
            if (inventory is null)
            {
                inventory = new InventoryItem { VariantId = id, OnHand = onHand };
                db.InventoryItems.Add(inventory);
            }
            else
            {
                inventory.OnHand = onHand;
            }
        }

        await db.SaveChangesAsync(ct);
        var available = inventory?.Available ?? 0;
        return Results.Ok(variant.ToDto(available));
    }

    // ---- Collections ----

    private static async Task<IResult> ListCollections(BazaarDbContext db, CancellationToken ct)
    {
        var collections = await db.Collections.AsNoTracking()
            .OrderBy(c => c.Title)
            .Select(c => new CollectionDto(c.Id, c.Slug, c.Title, c.Description, c.Products.Count))
            .ToListAsync(ct);
        return Results.Ok(collections);
    }

    private static async Task<IResult> CreateCollection(BazaarDbContext db, UpsertCollectionRequest request, CancellationToken ct)
    {
        if (!RequestValidation.TryValidate(request, out var errors))
            return Results.ValidationProblem(errors);
        if (await db.Collections.AnyAsync(c => c.Slug == request.Slug, ct))
            return Results.Problem($"A collection with slug '{request.Slug}' already exists.", statusCode: StatusCodes.Status409Conflict, title: "Slug already in use");

        var collection = new Collection { Title = request.Title!, Slug = request.Slug!, Description = request.Description };
        db.Collections.Add(collection);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/admin/collections/{collection.Id}", collection.ToDto(0));
    }

    private static async Task<IResult> UpdateCollection(BazaarDbContext db, Guid id, UpsertCollectionRequest request, CancellationToken ct)
    {
        if (!RequestValidation.TryValidate(request, out var errors))
            return Results.ValidationProblem(errors);

        var collection = await db.Collections.Include(c => c.Products).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (collection is null) return Results.NotFound();

        if (!string.Equals(collection.Slug, request.Slug, StringComparison.Ordinal)
            && await db.Collections.AnyAsync(c => c.Slug == request.Slug && c.Id != id, ct))
            return Results.Problem($"A collection with slug '{request.Slug}' already exists.", statusCode: StatusCodes.Status409Conflict, title: "Slug already in use");

        collection.Title = request.Title!;
        collection.Slug = request.Slug!;
        collection.Description = request.Description;
        await db.SaveChangesAsync(ct);
        return Results.Ok(collection.ToDto(collection.Products.Count));
    }

    private static async Task<IResult> DeleteCollection(BazaarDbContext db, Guid id, CancellationToken ct)
    {
        var collection = await db.Collections.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (collection is null) return Results.NotFound();
        db.Collections.Remove(collection);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    // ---- Helpers ----

    private static IQueryable<Product> LoadProductGraph(BazaarDbContext db) =>
        db.Products.AsNoTracking()
            .Include(p => p.Images)
            .Include(p => p.Variants).ThenInclude(v => v.Options)
            .Include(p => p.Collections)
            .AsSplitQuery();

    private static async Task<Dictionary<Guid, int>> StockMap(BazaarDbContext db, List<Guid> variantIds, CancellationToken ct)
    {
        var stock = await db.InventoryItems.AsNoTracking()
            .Where(i => variantIds.Contains(i.VariantId))
            .Select(i => new { i.VariantId, Available = i.OnHand - i.Reserved })
            .ToListAsync(ct);
        return stock.ToDictionary(s => s.VariantId, s => Math.Max(0, s.Available));
    }

    private static async Task<List<Collection>?> ResolveCollections(BazaarDbContext db, List<string> slugs, CancellationToken ct)
    {
        var wanted = slugs.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
        if (wanted.Count == 0) return new List<Collection>();
        var found = await db.Collections.Where(c => wanted.Contains(c.Slug)).ToListAsync(ct);
        return found.Count == wanted.Count ? found : null;
    }

    private static async Task<string?> FindDuplicateSku(BazaarDbContext db, IEnumerable<string> skus, CancellationToken ct)
    {
        var list = skus.ToList();
        var firstDuplicateInRequest = list.GroupBy(s => s).FirstOrDefault(g => g.Count() > 1)?.Key;
        if (firstDuplicateInRequest is not null) return firstDuplicateInRequest;
        return await db.Variants.Where(v => list.Contains(v.Sku)).Select(v => v.Sku).FirstOrDefaultAsync(ct);
    }

    private static bool TryResolveStatus(string? value, ProductStatus fallback, out ProductStatus status)
    {
        if (string.IsNullOrWhiteSpace(value)) { status = fallback; return true; }
        return Enum.TryParse(value, ignoreCase: true, out status) && Enum.IsDefined(status);
    }

    private static Dictionary<string, string[]> OneError(string field, string message) =>
        new() { [field] = new[] { message } };
}
