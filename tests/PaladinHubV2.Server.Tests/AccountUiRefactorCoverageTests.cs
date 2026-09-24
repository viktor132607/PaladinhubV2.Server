using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PaladinHub.Models.Account;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Accounts;
using PaladinHubV2.Server.Domain.Services.Wallet;
using PaladinHubV2.Server.Tests.Support;

namespace PaladinHubV2.Server.Tests;

public sealed class AccountUiRefactorCoverageTests
{
    private static readonly CancellationToken Ct =
        TestContext.Current.CancellationToken;

    [Fact]
    public async Task FacadeForwardsTheCompleteLegacyContract()
    {
        var identity = new Mock<IAccountIdentityService>();
        var overview = new Mock<IAccountOverviewService>();
        var avatars = new Mock<IAccountAvatarService>();
        var profile = new Mock<IAccountProfileService>();
        var security = new Mock<IAccountSecurityScorer>();
        var region = new Mock<IAccountRegionService>();

        var user = new User { Id = "user-1", FullName = "User" };
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, user.Id)],
                "test"));
        var account = new MyAccountViewModel();
        var file = FormFile("avatar.png", [1, 2, 3]);
        var avatarResult =
            AccountAvatarResult.Success("/uploads/avatars/user-1/a.png");

        identity.Setup(x => x.GetMeAsync(principal))
            .ReturnsAsync(user);
        identity.Setup(x => x.GetUserId(principal))
            .Returns(user.Id);
        overview.Setup(x => x.BuildMyAccountAsync(
                user,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        overview.Setup(x => x.BuildOverviewAsync(
                user,
                2,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        overview.Setup(x => x.GetBalanceAsync(user.Id))
            .ReturnsAsync(12.5m);
        profile.Setup(x => x.MarkPhoneVerifiedAsync(
                user,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        avatars.Setup(x => x.UploadAvatarAsync(
                user,
                file,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(avatarResult);
        avatars.Setup(x => x.SetUploadedAvatarAsync(
                user,
                "/a.png",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(avatarResult);
        avatars.Setup(x => x.DeleteUploadAsync(
                user,
                "/a.png",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccountAvatarResult.Success());
        avatars.Setup(x => x.SetDefaultAvatarAsync(
                user,
                "default.png",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(avatarResult);
        avatars.Setup(x => x.GetUserUploadedAvatars(user.Id))
            .Returns(["/one.png"]);
        security.Setup(x => x.Compute(user))
            .Returns((80, ["tip"]));
        region.Setup(x => x.ReadRegionCookie())
            .Returns("EU");
        region.Setup(x => x.GetCurrencyForRegion("EU"))
            .Returns("USD");
        region.Setup(x => x.RegionDisplay("EU"))
            .Returns("United States");

        var service = new AccountUiService(
            identity.Object,
            overview.Object,
            avatars.Object,
            profile.Object,
            security.Object,
            region.Object);

        Assert.Same(user, await service.GetMe(principal));
        Assert.Equal(user.Id, service.GetUserId(principal));
        Assert.Same(
            account,
            await service.BuildMyAccountAsync(user, Ct));
        Assert.Same(
            account,
            await service.BuildOverviewAsync(user, 2, Ct));

        await service.MarkPhoneVerifiedAsync(user, Ct);

        Assert.Same(
            avatarResult,
            await service.UploadAvatarAsync(user, file, Ct));
        Assert.Same(
            avatarResult,
            await service.SetUploadedAvatarAsync(
                user,
                "/a.png",
                Ct));
        Assert.True(
            (await service.DeleteUploadAsync(
                user,
                "/a.png",
                Ct)).Ok);
        Assert.Same(
            avatarResult,
            await service.SetDefaultAvatarAsync(
                user,
                "default.png",
                Ct));

        var score = service.ComputeSecurityScore(user);
        Assert.Equal(80, score.score);
        Assert.Equal(["tip"], score.tips);
        Assert.Equal(12.5m, await service.GetBalance(user.Id));
        Assert.Equal("EU", service.ReadRegionCookie());
        Assert.Equal("USD", service.GetCurrencyForRegion("EU"));
        Assert.Equal("United States", service.RegionDisplay("EU"));
        Assert.Equal(
            ["/one.png"],
            service.GetUserUploadedAvatars(user.Id));

        service.RegisterUserUploadedAvatar(user.Id, "/one.png");
        service.UnregisterUserUploadedAvatar(user.Id, "/one.png");

        avatars.Verify(x =>
            x.RegisterUserUploadedAvatar(user.Id, "/one.png"),
            Times.Once);
        avatars.Verify(x =>
            x.UnregisterUserUploadedAvatar(user.Id, "/one.png"),
            Times.Once);
    }

    [Fact]
    public async Task IdentityServiceHandlesMissingAndAuthenticatedPrincipal()
    {
        var manager = CreateUserManager();
        var user = new User { Id = "user-1", FullName = "User" };
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, user.Id)],
                "test"));

        manager.Setup(x => x.GetUserAsync(principal))
            .ReturnsAsync(user);

        var service = new AccountIdentityService(manager.Object);

        Assert.Null(await service.GetMeAsync(null!));
        Assert.Same(user, await service.GetMeAsync(principal));
        Assert.Null(service.GetUserId(null!));
        Assert.Equal(user.Id, service.GetUserId(principal));
    }

    [Fact]
    public void SecurityScorerCoversWeakAndStrongAccounts()
    {
        var service = new AccountSecurityScorer();

        var weak = new User
        {
            Email = "weak@example.test",
            EmailConfirmed = false,
            TwoFactorEnabled = false,
            PasswordHash = null
        };

        var weakResult = service.Compute(weak);
        Assert.Equal(10, weakResult.score);
        Assert.Equal(3, weakResult.tips.Length);
        Assert.Contains("Verify your email.", weakResult.tips);
        Assert.Contains(
            "Enable two-factor authentication.",
            weakResult.tips);
        Assert.Contains(
            "Set a strong account password.",
            weakResult.tips);

        var strong = new User
        {
            Email = "strong@example.test",
            EmailConfirmed = true,
            TwoFactorEnabled = true,
            PasswordHash = "hash"
        };

        var strongResult = service.Compute(strong);
        Assert.Equal(100, strongResult.score);
        Assert.Empty(strongResult.tips);

        var noEmail = new User
        {
            TwoFactorEnabled = true,
            PasswordHash = "hash"
        };
        Assert.Equal(70, service.Compute(noEmail).score);
    }

    [Fact]
    public void RegionServiceReadsCookieAndProvidesLegacyPresentation()
    {
        var accessor = new HttpContextAccessor();
        var service = new AccountRegionService(accessor);

        Assert.Equal("US", service.ReadRegionCookie());

        var context = new DefaultHttpContext();
        accessor.HttpContext = context;
        Assert.Equal("US", service.ReadRegionCookie());

        context.Request.Headers.Cookie = "region=EU";
        Assert.Equal("EU", service.ReadRegionCookie());

        Assert.Equal("USD", service.GetCurrencyForRegion("EU"));
        Assert.Equal("United States", service.RegionDisplay("EU"));
    }

    [Fact]
    public async Task ProfileServiceMarksPhoneVerified()
    {
        await using AppDbContext db =
            PageBuilderSqliteTestDatabase.CreateContext();

        var user = new User
        {
            Id = "profile-user",
            UserName = "profile-user",
            FullName = "Profile User",
            PhoneNumberConfirmed = false
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(Ct);

        var service = new AccountProfileService(db);
        await service.MarkPhoneVerifiedAsync(user, Ct);

        db.ChangeTracker.Clear();
        User stored = await db.Users.SingleAsync(
            item => item.Id == user.Id,
            Ct);
        Assert.True(stored.PhoneNumberConfirmed);
    }

    [Fact]
    public async Task OverviewServiceBuildsRecentAndPagedAccountViews()
    {
        await using AppDbContext db =
            PageBuilderSqliteTestDatabase.CreateContext();

        var user = new User
        {
            Id = "overview-user",
            UserName = "overview-user",
            FullName = "Overview User"
        };
        var other = new User
        {
            Id = "other-user",
            UserName = "other-user",
            FullName = "Other User"
        };
        db.Users.AddRange(user, other);

        DateTime start = DateTime.UtcNow.AddDays(-10);
        for (int index = 0; index < 7; index++)
        {
            db.Transactions.Add(new Transaction
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                User = user,
                CreatedAtUtc = start.AddDays(index),
                PurchaseTitle = $"Purchase {index}",
                Amount = index + 1,
                Currency = "USD",
                Status = TransactionStatus.Complete
            });
        }

        db.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = other.Id,
            User = other,
            CreatedAtUtc = DateTime.UtcNow,
            PurchaseTitle = "Other purchase",
            Amount = 99m,
            Currency = "USD",
            Status = TransactionStatus.Complete
        });
        await db.SaveChangesAsync(Ct);

        var wallet = new Mock<IWalletService>();
        wallet.Setup(x => x.GetBalanceAsync(user.Id))
            .ReturnsAsync(42m);

        var security = new Mock<IAccountSecurityScorer>();
        security.Setup(x => x.Compute(user))
            .Returns((75, ["Enable something."]));

        var avatars = new Mock<IAccountAvatarService>();
        avatars.Setup(x => x.GetUserUploadedAvatars(user.Id))
            .Returns(["/a.png", "/b.png"]);

        var service = new AccountOverviewService(
            wallet.Object,
            db,
            security.Object,
            avatars.Object);

        MyAccountViewModel myAccount =
            await service.BuildMyAccountAsync(user, Ct);

        Assert.Equal("USD", myAccount.Currency);
        Assert.Equal(42m, myAccount.Balance);
        Assert.Equal(5, myAccount.RecentPurchases.Count);
        Assert.Equal(1, myAccount.Page);
        Assert.Equal(1, myAccount.TotalPages);
        Assert.Equal(75, myAccount.SecurityScore);
        Assert.Equal(["Enable something."], myAccount.SecurityTips);
        Assert.Equal(["/a.png", "/b.png"], myAccount.Uploads);
        Assert.Equal(
            "Purchase 6",
            myAccount.RecentPurchases[0].PurchaseTitle);

        MyAccountViewModel firstPage =
            await service.BuildOverviewAsync(user, 0, Ct);
        Assert.Equal(1, firstPage.Page);
        Assert.Equal(2, firstPage.TotalPages);
        Assert.Equal(5, firstPage.RecentPurchases.Count);

        MyAccountViewModel lastPage =
            await service.BuildOverviewAsync(user, 99, Ct);
        Assert.Equal(2, lastPage.Page);
        Assert.Equal(2, lastPage.RecentPurchases.Count);

        Assert.Equal(
            42m,
            await service.GetBalanceAsync(user.Id));
    }

    [Fact]
    public async Task AvatarServiceValidatesStoresSelectsDeletesAndDefaults()
    {
        await using AppDbContext db =
            PageBuilderSqliteTestDatabase.CreateContext();

        var user = new User
        {
            Id = "avatar-user",
            UserName = "avatar-user",
            FullName = "Avatar User"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(Ct);

        var store = new Mock<IAccountAvatarStore>();
        var service = new AccountAvatarService(db, store.Object);

        AccountAvatarResult missing =
            await service.UploadAvatarAsync(user, null, Ct);
        Assert.Equal(AccountAvatarFailure.NoFile, missing.Failure);

        var tooLarge = new Mock<IFormFile>();
        tooLarge.SetupGet(x => x.Length)
            .Returns(5L * 1024 * 1024 + 1);
        tooLarge.SetupGet(x => x.FileName)
            .Returns("large.png");
        Assert.Equal(
            AccountAvatarFailure.NoFile,
            (await service.UploadAvatarAsync(
                user,
                tooLarge.Object,
                Ct)).Failure);

        Assert.Equal(
            AccountAvatarFailure.NoFile,
            (await service.UploadAvatarAsync(
                user,
                FormFile("empty.png", []),
                Ct)).Failure);

        Assert.Equal(
            AccountAvatarFailure.UnsupportedFormat,
            (await service.UploadAvatarAsync(
                user,
                FormFile("avatar.gif", [1]),
                Ct)).Failure);

        string storedPath =
            "/uploads/avatars/avatar-user/stored.png";
        store.Setup(x => x.SaveAsync(
                user.Id,
                ".png",
                It.IsAny<IFormFile>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedPath);

        AccountAvatarResult uploaded =
            await service.UploadAvatarAsync(
                user,
                FormFile("avatar.PNG", [1, 2]),
                Ct);
        Assert.True(uploaded.Ok);
        Assert.Equal(storedPath, uploaded.Path);

        store.Setup(x => x.TryResolveOwnedUpload(
                user.Id,
                "invalid",
                out It.Ref<string>.IsAny))
            .Returns(false);

        Assert.Equal(
            AccountAvatarFailure.InvalidPath,
            (await service.SetUploadedAvatarAsync(
                user,
                "invalid",
                Ct)).Failure);
        Assert.Equal(
            AccountAvatarFailure.InvalidPath,
            (await service.DeleteUploadAsync(
                user,
                "invalid",
                Ct)).Failure);

        string fullPath = "/tmp/avatar.png";
        store.Setup(x => x.TryResolveOwnedUpload(
                user.Id,
                storedPath,
                out fullPath))
            .Returns(true);
        store.Setup(x => x.Exists(fullPath))
            .Returns(false);

        Assert.Equal(
            AccountAvatarFailure.NotFound,
            (await service.SetUploadedAvatarAsync(
                user,
                storedPath,
                Ct)).Failure);
        Assert.Equal(
            AccountAvatarFailure.NotFound,
            (await service.DeleteUploadAsync(
                user,
                storedPath,
                Ct)).Failure);

        store.Setup(x => x.Exists(fullPath))
            .Returns(true);

        AccountAvatarResult selected =
            await service.SetUploadedAvatarAsync(
                user,
                storedPath,
                Ct);
        Assert.True(selected.Ok);
        Assert.Equal(storedPath, user.AvatarPath);

        AccountAvatarResult deleted =
            await service.DeleteUploadAsync(
                user,
                storedPath,
                Ct);
        Assert.True(deleted.Ok);
        Assert.Null(user.AvatarPath);
        store.Verify(x => x.Delete(fullPath), Times.Once);

        string otherPath =
            "/uploads/avatars/avatar-user/other.png";
        string otherFullPath = "/tmp/other.png";
        store.Setup(x => x.TryResolveOwnedUpload(
                user.Id,
                otherPath,
                out otherFullPath))
            .Returns(true);
        store.Setup(x => x.Exists(otherFullPath))
            .Returns(true);

        user.AvatarPath = "/images/avatars/default.png";
        Assert.True(
            (await service.DeleteUploadAsync(
                user,
                otherPath,
                Ct)).Ok);

        Assert.Equal(
            AccountAvatarFailure.InvalidDefaultAvatar,
            (await service.SetDefaultAvatarAsync(
                user,
                null,
                Ct)).Failure);
        Assert.Equal(
            AccountAvatarFailure.InvalidDefaultAvatar,
            (await service.SetDefaultAvatarAsync(
                user,
                "../bad.png",
                Ct)).Failure);

        AccountAvatarResult defaultResult =
            await service.SetDefaultAvatarAsync(
                user,
                "default02.png",
                Ct);
        Assert.True(defaultResult.Ok);
        Assert.Equal(
            "/images/avatars/default02.png",
            defaultResult.Path);

        store.Setup(x => x.List(user.Id))
            .Returns(["/x.png"]);
        Assert.Equal(
            ["/x.png"],
            service.GetUserUploadedAvatars(user.Id));

        service.RegisterUserUploadedAvatar(user.Id, "/x.png");
        service.UnregisterUserUploadedAvatar(user.Id, "/x.png");
    }

    [Fact]
    public async Task PhysicalAvatarStoreCoversPersistenceResolutionListingAndDeletion()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "paladinhub-account-ui-" + Guid.NewGuid().ToString("N"));

        try
        {
            Assert.NotNull(new PhysicalAccountAvatarStore());
            var store = new PhysicalAccountAvatarStore(root);
            var file = FormFile("avatar.png", [1, 2, 3, 4]);

            Assert.Empty(store.List(""));
            Assert.Empty(store.List("user-1"));

            string webPath =
                await store.SaveAsync(
                    "user-1",
                    ".png",
                    file,
                    Ct);

            Assert.StartsWith(
                "/uploads/avatars/user-1/",
                webPath);

            Assert.False(
                store.TryResolveOwnedUpload(
                    "user-1",
                    null,
                    out _));
            Assert.False(
                store.TryResolveOwnedUpload(
                    "user-1",
                    "/uploads/avatars/other/file.png",
                    out _));
            Assert.False(
                store.TryResolveOwnedUpload(
                    "user-1",
                    "/uploads/avatars/user-1/",
                    out _));
            Assert.False(
                store.TryResolveOwnedUpload(
                    "user-1",
                    "/uploads/avatars/user-1/\0bad.png",
                    out _));

            Assert.True(
                store.TryResolveOwnedUpload(
                    "user-1",
                    webPath,
                    out string fullPath));
            Assert.True(store.Exists(fullPath));

            Assert.Single(store.List("user-1"));

            store.Delete(fullPath);
            Assert.False(store.Exists(fullPath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static FormFile FormFile(
        string fileName,
        byte[] bytes)
    {
        return new FormFile(
            new MemoryStream(bytes),
            0,
            bytes.Length,
            "file",
            fileName);
    }

    private static Mock<UserManager<User>> CreateUserManager() =>
        new(
            Mock.Of<IUserStore<User>>(),
            Options.Create(new IdentityOptions()),
            Mock.Of<IPasswordHasher<User>>(),
            Array.Empty<IUserValidator<User>>(),
            Array.Empty<IPasswordValidator<User>>(),
            Mock.Of<ILookupNormalizer>(),
            new IdentityErrorDescriber(),
            Mock.Of<IServiceProvider>(),
            NullLogger<UserManager<User>>.Instance);
}
