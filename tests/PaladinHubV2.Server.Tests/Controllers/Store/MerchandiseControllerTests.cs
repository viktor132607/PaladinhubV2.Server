using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using PaladinHub.Models;
using PaladinHub.Models.Products;
using PaladinHubV2.Server.API.Controllers.Store;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Domain.Services.Products;

namespace PaladinHubV2.Server.Tests.Controllers.Store;

public sealed class MerchandiseControllerTests
{
    [Fact]
    public async Task Merchandise_NormalizesInvalidPagingRatingAndSortBeforeQuery()
    {
        using AppDbContext db = CreateContext();
        var products = CreateProductService();
        var controller = new MerchandiseController(products.Object, db);
        var options = new ProductQueryOptions
        {
            Page = 0,
            PageSize = 0,
            MinRating = 0,
            SortBy = (ProductSortBy)999
        };

        IActionResult result = await controller.Merchandise(options, TestContext.Current.CancellationToken);

        var ok = Assert.IsType<OkObjectResult>(result);
        var model = Assert.IsType<MerchandisePageViewModel>(ok.Value);
        Assert.Same(options, model.Query);
        Assert.Equal(1, options.Page);
        Assert.Equal(20, options.PageSize);
        Assert.Equal(1, options.MinRating);
        Assert.Equal(ProductSortBy.Relevance, options.SortBy);
        products.Verify(service => service.QueryAsync(
            It.Is<ProductQueryOptions>(query => ReferenceEquals(query, options)),
            TestContext.Current.CancellationToken), Times.Once);
    }

    [Fact]
    public async Task Index_CapsUpperBoundsAndPreservesValidSort()
    {
        using AppDbContext db = CreateContext();
        var products = CreateProductService();
        var controller = new MerchandiseController(products.Object, db);
        var options = new ProductQueryOptions
        {
            Page = 3,
            PageSize = 500,
            MinRating = 9,
            SortBy = ProductSortBy.Price,
            Desc = true
        };

        IActionResult result = await controller.Index(options, TestContext.Current.CancellationToken);

        var ok = Assert.IsType<OkObjectResult>(result);
        var model = Assert.IsType<MerchandisePageViewModel>(ok.Value);
        Assert.Equal(3, model.Query.Page);
        Assert.Equal(200, model.Query.PageSize);
        Assert.Equal(5, model.Query.MinRating);
        Assert.Equal(ProductSortBy.Price, model.Query.SortBy);
        Assert.True(model.Query.Desc);
        Assert.Equal(new[] { "Armor", "Weapons" }, model.AllCategories);
    }

    [Fact]
    public async Task LegacyMerchandise_ReturnsProductServiceCollection()
    {
        using AppDbContext db = CreateContext();
        var products = CreateProductService();
        ICollection<ProductViewModel> expected = new List<ProductViewModel>
        {
            new() { Id = "product-1", Name = "Product One", Price = 10m }
        };
        products.Setup(service => service.GetAll()).ReturnsAsync(expected);
        var controller = new MerchandiseController(products.Object, db);

        IActionResult result = await controller.LegacyMerchandise();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(expected, ok.Value);
        products.Verify(service => service.GetAll(), Times.Once);
    }

    [Fact]
    public async Task IndexLoggedIn_ReturnsProductServiceCollection()
    {
        using AppDbContext db = CreateContext();
        var products = CreateProductService();
        ICollection<ProductViewModel> expected = new List<ProductViewModel>
        {
            new() { Id = "product-2", Name = "Product Two", Price = 20m }
        };
        products.Setup(service => service.GetAll()).ReturnsAsync(expected);
        var controller = new MerchandiseController(products.Object, db);

        IActionResult result = await controller.IndexLoggedIn();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(expected, ok.Value);
        products.Verify(service => service.GetAll(), Times.Once);
    }

    private static Mock<IProductService> CreateProductService()
    {
        var service = new Mock<IProductService>();
        service.Setup(value => value.QueryAsync(
                It.IsAny<ProductQueryOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductQueryOptions options, CancellationToken _) =>
                new PagedResult<ProductListItem>
                {
                    Page = options.Page,
                    PageSize = options.PageSize,
                    TotalItems = 0
                });
        service.Setup(value => value.GetAllCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Armor", "Weapons" });
        return service;
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"merchandise-controller-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }
}
