namespace Catalog.Endpoints;

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/products");

        // GET all
        group.MapGet("/", async (ProductService service) =>
        {
            var products = await service.GetProductsAsync();
            return Results.Ok(products);
        })
        .WithName("GetAllProducts")
        .Produces<List<Product>>(StatusCodes.Status200OK);

        // GET by ID
        group.MapGet("/{id}", async (int id, ProductService service) =>
        {
            var product = await service.GetProductByIdAsync(id);
            if (product is null) return Results.NotFound();

            return Results.Ok(product);
        })
        .WithName("GetProductById")
        .Produces<Product>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // POST (Create)
        group.MapPost("/", async (Product product, ProductService service) =>
        {
            await service.CreateProductAsync(product);
            return Results.Created($"/products/{product.Id}", product);
        })
        .WithName("CreateProduct")
        .Produces<Product>(StatusCodes.Status201Created)
        .RequireAuthorization("AdminOnly");

        // PUT (Update)
        group.MapPut("/{id}", async (int id, Product inputProduct, ProductService service) =>
        {
            var updatedProduct = await service.FindProductForUpdateAsync(id);
            if (updatedProduct is null) return Results.NotFound();

            await service.UpdateProductAsync(updatedProduct, inputProduct);
            return Results.NoContent();
        })
        .WithName("UpdateProduct")
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status204NoContent)
        .RequireAuthorization("AdminOnly");

        // DELETE
        group.MapDelete("/{id}", async (int id, ProductService service) =>
        {
            var deletedProduct = await service.FindProductForUpdateAsync(id);
            if (deletedProduct is null) return Results.NotFound();

            await service.DeleteProductAsync(deletedProduct);
            return Results.NoContent();
        })
        .WithName("DeleteProduct")
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status204NoContent)
        .RequireAuthorization("AdminOnly");

        // Upload (or replace) a product's image. Multipart form with a single "file" field.
        // Antiforgery is off because Catalog is called with a JWT, not a browser cookie
        // (the BFF, which does use a cookie, enforces its own CSRF header check).
        group.MapPost("/{id}/image", async (int id, IFormFile file, ProductService service) =>
        {
            if (file.Length == 0 || file.Length > ProductImageStorage.MaxBytes)
            {
                return Results.BadRequest($"Image must be between 1 byte and {ProductImageStorage.MaxBytes / (1024 * 1024)} MB.");
            }

            if (!ProductImageStorage.AllowedTypes.ContainsKey(file.ContentType))
            {
                return Results.BadRequest("Only JPEG, PNG, WebP or GIF images are allowed.");
            }

            var product = await service.FindProductForUpdateAsync(id);
            if (product is null) return Results.NotFound();

            await using var stream = file.OpenReadStream();
            await service.SetProductImageAsync(product, stream, file.ContentType);

            return Results.Ok(product);
        })
        .WithName("UploadProductImage")
        .Produces<Product>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuthorization("AdminOnly")
        .DisableAntiforgery();

        // Serve an uploaded image. Blob names are unique (a new name on every upload),
        // so the browser may cache them for a long time.
        group.MapGet("/images/{**blobName}", async (string blobName, ProductImageStorage imageStorage, HttpContext httpContext) =>
        {
            if (!ProductImageStorage.IsUploadedImage(blobName)) return Results.NotFound();

            var image = await imageStorage.OpenReadAsync(blobName);
            if (image is null) return Results.NotFound();

            httpContext.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            httpContext.Response.Headers.XContentTypeOptions = "nosniff";
            return Results.Stream(image.Value.Content, image.Value.ContentType);
        })
        .WithName("GetProductImage")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // Search
        group.MapGet("search/{query}", async (string query, ProductService service) =>
        {
            var products = await service.SearchProductsAsync(query);

            return Results.Ok(products);
        })
        .WithName("SearchProducts")
        .Produces<List<Product>>(StatusCodes.Status200OK);
    }
}
