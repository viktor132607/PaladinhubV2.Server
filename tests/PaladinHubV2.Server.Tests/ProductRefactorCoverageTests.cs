using Microsoft.EntityFrameworkCore;
using Moq;
using PaladinHub.Models;
using PaladinHub.Models.Carts;
using PaladinHub.Models.Products;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Products;
using PaladinHubV2.Server.Tests.Support;

namespace PaladinHubV2.Server.Tests;

public sealed class ProductRefactorCoverageTests
{
    private static readonly CancellationToken Ct =
        TestContext.Current.CancellationToken;

    [Fact]
    public async Task FacadeForwardsEveryPublicOperation()
    {
        var catalog = new Mock<IProductCatalogQueryService>();
        var search = new Mock<IProductSearchService>();
        var mutations = new Mock<IProductMutationService>();
        var reviews = new Mock<IProductReviewService>();

        var product = new ProductViewModel
        {
            Id = "p",
            Name = "P",
            Price = 1m
        };
        var cart = new MyCartViewModel();
        var create = new CreateProductViewModel
        {
            Name = "Create",
            Price = 1m
        };
        var edit = new EditProductViewModel
        {
            Id = "p",
            Name = "Edit",
            Price = 2m
        };
        var details = new ProductDetailsViewModel
        {
            Id = "p",
            Name = "P",
            Price = 1m
        };
        var page = new PagedResult<ProductListItem>();
        var user = new User { Id = "user-1" };
        var review = new AddReviewInput
        {
            ProductId = "p",
            Rating = 5,
            Content = "Good"
        };

        catalog.Setup(x => x.GetAllAsync())
            .ReturnsAsync(new List<ProductViewModel> { product });
        catalog.Setup(x => x.GetMyProductsAsync(user))
            .ReturnsAsync(cart);
        catalog.Setup(x => x.GetCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Armor" });
        catalog.Setup(x => x.GetForEditAsync("p", It.IsAny<CancellationToken>()))
            .ReturnsAsync(edit);
        catalog.Setup(x => x.GetDetailsAsync("p", It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);
        catalog.Setup(x => x.GetDetailsAsync(
                "p",
                "user-1",
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);
        search.Setup(x => x.QueryAsync(
                It.IsAny<ProductQueryOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);
        mutations.Setup(x => x.CreateAsync(create))
            .ReturnsAsync(create);
        mutations.Setup(x => x.DeleteAsync("p"))
            .ReturnsAsync(true);
        mutations.Setup(x => x.UpdateAsync(edit, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mutations.Setup(x => x.AddImageAsync(
                "p",
                "https://img.test/a.png",
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mutations.Setup(x => x.RemoveImageAsync(
                7,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        reviews.Setup(x => x.AddReviewAsync(
                review,
                "user-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        reviews.Setup(x => x.DeleteReviewAsync(
                9,
                "user-1",
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new ProductService(
            catalog.Object,
            search.Object,
            mutations.Object,
            reviews.Object);

        Assert.Single(await service.GetAll());
        Assert.Same(create, await service.Create(create));
        Assert.Same(cart, await service.GetMyProducts(user));
        Assert.True(await service.Delete("p"));
        Assert.Equal(
            ["Armor"],
            await service.GetAllCategoriesAsync(Ct));
        Assert.Equal(
            ["Armor"],
            await service.GetCategories());
        Assert.Same(
            page,
            await service.QueryAsync(
                new ProductQueryOptions(),
                Ct));
        Assert.Same(
            edit,
            await service.GetForEditAsync("p", Ct));
        Assert.True(await service.UpdateAsync(edit, Ct));
        Assert.Same(
            details,
            await service.GetDetailsAsync("p", Ct));
        Assert.Same(
            details,
            await service.GetDetailsAsync(
                "p",
                "user-1",
                true,
                Ct));
        Assert.True(await service.AddReviewAsync(
            review,
            "user-1",
            Ct));
        Assert.True(await service.DeleteReviewAsync(
            9,
            "user-1",
            true,
            Ct));
        Assert.True(await service.AddImageAsync(
            "p",
            "https://img.test/a.png",
            1,
            Ct));
        Assert.True(await service.RemoveImageAsync(7, Ct));

        await using AppDbContext db =
            PageBuilderSqliteTestDatabase.CreateContext();
        Assert.NotNull(new ProductService(db));
    }

    [Fact]
    public async Task CatalogQueriesCoverCartCategoriesEditAndDetails()
    {
        await using AppDbContext db =
            PageBuilderSqliteTestDatabase.CreateContext();

        var user = new User
        {
            Id = "user-1",
            UserName = "user1",
            Email = "user1@example.test",
            FullName = "User One"
        };
        db.Users.Add(user);

        var first = new Product("First", 10m)
        {
            Category = "Armor",
            Description = "First description"
        };
        var similar = new Product("Similar", 20m)
        {
            Category = "Armor",
            Description = "Similar"
        };
        var other = new Product("Other", 30m)
        {
            Category = "Books",
            Description = "Other"
        };
        db.Products.AddRange(first, similar, other);
        await db.SaveChangesAsync(Ct);

        var firstImage = new ProductImage
        {
            ProductId = first.Id,
            Url = "https://img.test/first-0.png",
            SortOrder = 0,
            AltText = "first"
        };
        var firstThumb = new ProductImage
        {
            ProductId = first.Id,
            Url = "https://img.test/first-1.png",
            SortOrder = 1
        };
        var similarImage = new ProductImage
        {
            ProductId = similar.Id,
            Url = "https://img.test/similar.png",
            SortOrder = 0
        };
        db.ProductImages.AddRange(
            firstImage,
            firstThumb,
            similarImage);
        await db.SaveChangesAsync(Ct);

        first.ThumbnailImageId = firstThumb.Id;
        similar.ThumbnailImageId = similarImage.Id;

        var cart = new Cart
        {
            UserId = user.Id,
            User = user
        };
        db.Carts.Add(cart);
        db.CartProducts.Add(new CartProduct
        {
            CartId = cart.Id,
            Cart = cart,
            ProductId = first.Id,
            Product = first,
            Quantity = 2
        });

        db.ProductReviews.Add(new ProductReview
        {
            ProductId = first.Id,
            Product = first,
            UserId = user.Id,
            Rating = 4,
            Content = "Solid",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(Ct);

        var service = new ProductCatalogQueryService(db);

        ICollection<ProductViewModel> all =
            await service.GetAllAsync();
        Assert.Equal(3, all.Count);
        Assert.Equal(
            firstThumb.Url,
            all.Single(item => item.Id == first.Id).ImageUrl);

        Assert.Empty(
            (await service.GetMyProductsAsync(null!))
                .MyProducts);

        var missingUser = new User
        {
            Id = "missing",
            FullName = "Missing"
        };
        Assert.Empty(
            (await service.GetMyProductsAsync(missingUser))
                .MyProducts);

        MyCartViewModel myCart =
            await service.GetMyProductsAsync(user);
        Assert.Single(myCart.MyProducts);
        Assert.Equal(20m, myCart.TotalPrice);
        Assert.Equal(
            firstThumb.Url,
            myCart.MyProducts.Single().ImageUrl);

        Assert.Equal(
            ["Armor", "Books"],
            await service.GetCategoriesAsync(Ct));

        Assert.Null(
            await service.GetForEditAsync("missing", Ct));

        EditProductViewModel edit =
            Assert.IsType<EditProductViewModel>(
                await service.GetForEditAsync(
                    first.Id,
                    Ct));
        Assert.Equal(firstThumb.Id, edit.ThumbnailImageId);
        Assert.Equal(1, edit.ThumbnailIndex);
        Assert.Equal(2, edit.Images.Count);

        Assert.Null(
            await service.GetDetailsAsync(
                "missing",
                Ct));

        ProductDetailsViewModel basic =
            Assert.IsType<ProductDetailsViewModel>(
                await service.GetDetailsAsync(
                    first.Id,
                    Ct));
        Assert.Equal(firstThumb.Url, basic.ImageUrl);
        Assert.Equal(2, basic.Images.Count);

        Assert.Null(
            await service.GetDetailsAsync(
                "missing",
                user.Id,
                false,
                Ct));

        ProductDetailsViewModel detailed =
            Assert.IsType<ProductDetailsViewModel>(
                await service.GetDetailsAsync(
                    first.Id,
                    user.Id,
                    false,
                    Ct));

        Assert.Equal(4d, detailed.AverageRating);
        Assert.Single(detailed.Reviews);
        Assert.True(detailed.Reviews[0].CanDelete);
        Assert.Single(detailed.Similar);
        Assert.Equal(similar.Id, detailed.Similar[0].Id);
        Assert.Equal(firstThumb.Url, detailed.ImageUrl);
    }

    [Fact]
    public async Task CatalogCartCoversExistingEmptyCart()
    {
        await using AppDbContext db =
            PageBuilderSqliteTestDatabase.CreateContext();

        var user = new User
        {
            Id = "empty-user",
            UserName = "empty",
            FullName = "Empty"
        };
        db.Users.Add(user);
        db.Carts.Add(new Cart
        {
            UserId = user.Id,
            User = user
        });
        await db.SaveChangesAsync(Ct);

        var service = new ProductCatalogQueryService(db);
        Assert.Empty(
            (await service.GetMyProductsAsync(user))
                .MyProducts);
    }

    [Fact]
    public async Task SearchRulesAndServiceCoverFiltersBandsSortingAndPaging()
    {
        var rules = new ProductQueryOptions
        {
            MinPrice = 200m,
            MaxPrice = 100m,
            PriceRanges =
            [
                "0-100",
                "100-200",
                "200-500",
                "500+",
                "ignored"
            ],
            Page = 0,
            PageSize = 500
        };

        ProductSearchRules.NormalizePriceBounds(rules);
        Assert.Equal(100m, rules.MinPrice);
        Assert.Equal(200m, rules.MaxPrice);
        Assert.Equal(
            5,
            ProductSearchRules.BuildPriceBands(rules).Count);
        Assert.Equal(1, ProductSearchRules.Page(rules));
        Assert.Equal(200, ProductSearchRules.PageSize(rules));

        var maxOnly = new ProductQueryOptions
        {
            MaxPrice = 50m
        };
        ProductPriceBand maxBand =
            Assert.Single(
                ProductSearchRules.BuildPriceBands(maxOnly));
        Assert.Null(maxBand.Min);
        Assert.Equal(50m, maxBand.Max);

        await using AppDbContext db =
            PageBuilderSqliteTestDatabase.CreateContext();

        var user = new User
        {
            Id = "reviewer",
            UserName = "reviewer",
            FullName = "Reviewer"
        };
        db.Users.Add(user);

        var alpha = new Product("Alpha Armor", 50m)
        {
            Category = "Armor",
            Description = "Holy plate"
        };
        var beta = new Product("Beta Armor", 150m)
        {
            Category = "Armor",
            Description = "Protection plate"
        };
        var gamma = new Product("Gamma Book", 600m)
        {
            Category = "Books",
            Description = "Guide"
        };
        db.Products.AddRange(alpha, beta, gamma);
        await db.SaveChangesAsync(Ct);

        db.ProductImages.Add(new ProductImage
        {
            ProductId = alpha.Id,
            Url = "https://img.test/alpha.png",
            SortOrder = 0
        });
        db.ProductReviews.AddRange(
            new ProductReview
            {
                ProductId = alpha.Id,
                Product = alpha,
                UserId = user.Id,
                Rating = 4,
                Content = "A"
            },
            new ProductReview
            {
                ProductId = beta.Id,
                Product = beta,
                UserId = user.Id,
                Rating = 5,
                Content = "B"
            });
        await db.SaveChangesAsync(Ct);

        var service = new ProductSearchService(db);

        var filtered = new ProductQueryOptions
        {
            Search = " armor ",
            Categories = ["Armor", " "],
            PriceRanges =
            [
                "0-100",
                "100-200",
                "200-500",
                "500+"
            ],
            MinPrice = 200m,
            MaxPrice = 100m,
            MinRating = 4,
            SortBy = ProductSortBy.Rating,
            Desc = true,
            Page = 0,
            PageSize = 500
        };

        PagedResult<ProductListItem> result =
            await service.QueryAsync(filtered, Ct);

        Assert.Equal(1, result.Page);
        Assert.Equal(200, result.PageSize);
        Assert.Equal(1, result.TotalItems);
        Assert.All(
            result.Items,
            item => Assert.Equal("Armor", item.Category));

        PagedResult<ProductListItem> maxOnlyResult =
            await service.QueryAsync(
                new ProductQueryOptions
                {
                    MaxPrice = 50m,
                    SortBy = ProductSortBy.Price
                },
                Ct);
        Assert.Single(maxOnlyResult.Items);

        foreach (ProductSortBy sort in Enum.GetValues<ProductSortBy>())
        {
            foreach (bool desc in new[] { false, true })
            {
                PagedResult<ProductListItem> sorted =
                    await service.QueryAsync(
                        new ProductQueryOptions
                        {
                            SortBy = sort,
                            Desc = desc,
                            Page = 1,
                            PageSize = 2
                        },
                        Ct);
                Assert.Equal(2, sorted.Items.Count);
                Assert.Equal(3, sorted.TotalItems);
            }
        }
    }

    [Fact]
    public async Task MutationsCoverCreateDeleteUpdateAndImageSynchronization()
    {
        await using AppDbContext db =
            PageBuilderSqliteTestDatabase.CreateContext();
        var service = new ProductMutationService(db);

        Assert.Null(await service.CreateAsync(null!));

        var duplicateSeed = new Product("Duplicate", 1m);
        db.Products.Add(duplicateSeed);
        await db.SaveChangesAsync(Ct);

        Assert.Null(
            await service.CreateAsync(
                new CreateProductViewModel
                {
                    Name = "Duplicate",
                    Price = 2m
                }));

        var create = new CreateProductViewModel
        {
            Name = "Created",
            Price = 10m,
            Category = "Armor",
            Description = "Created product",
            ThumbnailIndex = 99,
            Images =
            [
                new ProductImageInputModel
                {
                    Url = " https://img.test/created-1.png ",
                    SortOrder = 1,
                    AltText = " image one "
                },
                new ProductImageInputModel
                {
                    Url = "https://img.test/created-0.png",
                    SortOrder = 0,
                    AltText = " "
                },
                new ProductImageInputModel
                {
                    Url = " ",
                    SortOrder = 2
                }
            ]
        };

        Assert.Same(create, await service.CreateAsync(create));

        Product created = await db.Products
            .Include(item => item.Images)
            .SingleAsync(
                item => item.Name == "Created",
                Ct);
        Assert.Equal(2, created.Images.Count);
        Assert.NotNull(created.ThumbnailImageId);
        Assert.Contains(
            created.Images,
            image =>
                image.Url == "https://img.test/created-1.png" &&
                image.AltText == "image one");

        var createDefaultThumb = new CreateProductViewModel
        {
            Name = "Default thumb",
            Price = 11m,
            Images =
            [
                new ProductImageInputModel
                {
                    Url = "https://img.test/default.png",
                    SortOrder = 0
                }
            ]
        };
        Assert.Same(
            createDefaultThumb,
            await service.CreateAsync(createDefaultThumb));

        Assert.False(await service.DeleteAsync(" "));
        Assert.False(await service.DeleteAsync("missing"));

        var deleteMe = new Product("Delete me", 2m);
        db.Products.Add(deleteMe);
        await db.SaveChangesAsync(Ct);
        Assert.True(await service.DeleteAsync(deleteMe.Id));

        Assert.False(
            await service.UpdateAsync(
                new EditProductViewModel
                {
                    Id = "missing",
                    Name = "Missing",
                    Price = 1m
                },
                Ct));

        var taken = new Product("Taken", 3m);
        db.Products.Add(taken);
        await db.SaveChangesAsync(Ct);

        Assert.False(
            await service.UpdateAsync(
                new EditProductViewModel
                {
                    Id = created.Id,
                    Name = "Taken",
                    Price = 20m
                },
                Ct));

        List<ProductImage> existing =
            created.Images
                .OrderBy(image => image.SortOrder)
                .ToList();
        int retainedId = existing[0].Id;

        var update = new EditProductViewModel
        {
            Id = created.Id,
            Name = "Updated",
            Price = 25m,
            Category = "Weapons",
            Description = "Updated product",
            ThumbnailImageId = retainedId,
            Images =
            [
                new ProductImageInputModel
                {
                    Id = retainedId,
                    Url = " https://img.test/retained.png ",
                    SortOrder = 0,
                    AltText = " retained "
                },
                new ProductImageInputModel
                {
                    Id = 999999,
                    Url = "https://img.test/from-unknown.png",
                    SortOrder = 2
                },
                new ProductImageInputModel
                {
                    Url = "https://img.test/new.png",
                    SortOrder = 3
                }
            ]
        };

        Assert.True(await service.UpdateAsync(update, Ct));

        db.ChangeTracker.Clear();
        Product updated = await db.Products
            .Include(item => item.Images)
            .SingleAsync(
                item => item.Id == created.Id,
                Ct);
        Assert.Equal("Updated", updated.Name);
        Assert.Equal(3, updated.Images.Count);
        Assert.Equal(retainedId, updated.ThumbnailImageId);
        Assert.Contains(
            updated.Images,
            image => image.Url == "https://img.test/new.png");

        Assert.True(
            await service.UpdateAsync(
                new EditProductViewModel
                {
                    Id = updated.Id,
                    Name = updated.Name,
                    Price = updated.Price,
                    Category = updated.Category,
                    Description = updated.Description,
                    ThumbnailImageId = 999999,
                    ThumbnailIndex = 0,
                    Images = updated.Images
                        .OrderBy(image => image.SortOrder)
                        .Select(image => new ProductImageInputModel
                        {
                            Id = image.Id,
                            Url = image.Url,
                            SortOrder = image.SortOrder,
                            AltText = image.AltText
                        })
                        .ToList()
                },
                Ct));

        db.ChangeTracker.Clear();
        updated = await db.Products
            .Include(item => item.Images)
            .SingleAsync(
                item => item.Id == created.Id,
                Ct);

        Assert.True(
            await service.UpdateAsync(
                new EditProductViewModel
                {
                    Id = updated.Id,
                    Name = updated.Name,
                    Price = updated.Price,
                    Category = updated.Category,
                    Description = updated.Description,
                    Images = updated.Images
                        .OrderBy(image => image.SortOrder)
                        .Select(image => new ProductImageInputModel
                        {
                            Id = image.Id,
                            Url = image.Url,
                            SortOrder = image.SortOrder,
                            AltText = image.AltText
                        })
                        .ToList()
                },
                Ct));

        Assert.False(
            await service.AddImageAsync(
                "",
                "",
                null,
                Ct));

        Assert.True(
            await service.AddImageAsync(
                updated.Id,
                " https://img.test/added.png ",
                9,
                Ct));

        ProductImage added = await db.ProductImages
            .SingleAsync(
                image =>
                    image.ProductId == updated.Id &&
                    image.SortOrder == 9,
                Ct);
        Assert.Equal(
            "https://img.test/added.png",
            added.Url);

        Assert.False(
            await service.RemoveImageAsync(
                999999,
                Ct));
        Assert.True(
            await service.RemoveImageAsync(
                added.Id,
                Ct));
    }

    [Fact]
    public async Task ReviewServiceCoversEligibilityDuplicateOwnershipAndAdminDelete()
    {
        await using AppDbContext db =
            PageBuilderSqliteTestDatabase.CreateContext();

        var user = new User
        {
            Id = "buyer",
            UserName = "buyer",
            FullName = "Buyer"
        };
        var otherUser = new User
        {
            Id = "other",
            UserName = "other",
            FullName = "Other"
        };
        db.Users.AddRange(user, otherUser);

        var product = new Product("Reviewable", 5m);
        db.Products.Add(product);

        var cart = new Cart
        {
            UserId = user.Id,
            User = user
        };
        db.Carts.Add(cart);
        db.CartProducts.Add(new CartProduct
        {
            CartId = cart.Id,
            Cart = cart,
            ProductId = product.Id,
            Product = product,
            Quantity = 1
        });
        await db.SaveChangesAsync(Ct);

        var service = new ProductReviewService(db);
        var input = new AddReviewInput
        {
            ProductId = product.Id,
            Rating = 5,
            Content = " great "
        };

        Assert.False(
            await service.AddReviewAsync(
                input,
                otherUser.Id,
                Ct));

        Assert.True(
            await service.AddReviewAsync(
                input,
                user.Id,
                Ct));

        ProductReview stored =
            await db.ProductReviews.SingleAsync(Ct);
        Assert.Equal("great", stored.Content);

        Assert.False(
            await service.AddReviewAsync(
                input,
                user.Id,
                Ct));

        Assert.False(
            await service.DeleteReviewAsync(
                999999,
                user.Id,
                false,
                Ct));

        Assert.False(
            await service.DeleteReviewAsync(
                stored.Id,
                otherUser.Id,
                false,
                Ct));

        Assert.True(
            await service.DeleteReviewAsync(
                stored.Id,
                otherUser.Id,
                true,
                Ct));
    }
}
