using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Data
{
	public class AppDbContext : IdentityDbContext<User>
	{
		public AppDbContext(
			DbContextOptions<AppDbContext> options)
			: base(options)
		{
		}

		public DbSet<Item> Items => Set<Item>();
		public DbSet<Spell> Spells => Set<Spell>();
		public DbSet<Product> Products => Set<Product>();
		public DbSet<Cart> Carts => Set<Cart>();
		public DbSet<CartProduct> CartProducts => Set<CartProduct>();

		public DbSet<DiscussionPost> DiscussionPosts =>
			Set<DiscussionPost>();

		public DbSet<DiscussionComment> DiscussionComments =>
			Set<DiscussionComment>();

		public DbSet<DiscussionLike> DiscussionLikes =>
			Set<DiscussionLike>();

		public DbSet<DiscussionCommentLike> DiscussionCommentLikes =>
			Set<DiscussionCommentLike>();

		public DbSet<ProductReview> ProductReviews =>
			Set<ProductReview>();

		public DbSet<ProductImage> ProductImages =>
			Set<ProductImage>();

		public DbSet<TalentNodeState> TalentNodeStates =>
			Set<TalentNodeState>();

		public DbSet<TalentBuild> TalentBuilds =>
			Set<TalentBuild>();

		public DbSet<TalentBuildNode> TalentBuildNodes =>
			Set<TalentBuildNode>();

		public DbSet<ContentPage> ContentPages =>
			Set<ContentPage>();

		public DbSet<DataPreset> DataPresets =>
			Set<DataPreset>();

		public DbSet<Transaction> Transactions =>
			Set<Transaction>();

		public DbSet<PaymentMethod> PaymentMethods =>
			Set<PaymentMethod>();

		public DbSet<PromoCode> PromoCodes =>
			Set<PromoCode>();

		public DbSet<PromoRedemption> PromoRedemptions =>
			Set<PromoRedemption>();

		protected override void OnModelCreating(
			ModelBuilder builder)
		{
			base.OnModelCreating(builder);

			ConfigureItems(builder);
			ConfigureSpells(builder);
			ConfigureUsers(builder);
			ConfigureCarts(builder);
			ConfigureDiscussions(builder);
			ConfigureProducts(builder);
			ConfigureTalents(builder);
			ConfigurePageBuilder(builder);
			ConfigureTransactions(builder);
			ConfigurePaymentMethods(builder);
			ConfigurePromoCodes(builder);
		}

		public override int SaveChanges()
		{
			return SaveChanges(
				acceptAllChangesOnSuccess: true);
		}

		public override int SaveChanges(
			bool acceptAllChangesOnSuccess)
		{
			UpdateContentPageRowVersions();

			return base.SaveChanges(
				acceptAllChangesOnSuccess);
		}

		public override Task<int> SaveChangesAsync(
			CancellationToken cancellationToken = default)
		{
			return SaveChangesAsync(
				acceptAllChangesOnSuccess: true,
				cancellationToken);
		}

		public override Task<int> SaveChangesAsync(
			bool acceptAllChangesOnSuccess,
			CancellationToken cancellationToken = default)
		{
			UpdateContentPageRowVersions();

			return base.SaveChangesAsync(
				acceptAllChangesOnSuccess,
				cancellationToken);
		}

		private static void ConfigureItems(
			ModelBuilder builder)
		{
			builder.Entity<Item>(entity =>
			{
				entity.ToTable("Items");

				entity.HasKey(item => item.Id);

				entity.Property(item => item.Name)
					.IsRequired()
					.HasMaxLength(100);

				entity.Property(item => item.Icon)
					.HasMaxLength(100);

				entity.Property(item => item.SecondIcon)
					.HasMaxLength(100);

				entity.Property(item => item.Description)
					.HasMaxLength(2000);

				entity.Property(item => item.Url)
					.HasMaxLength(300);

				entity.Property(item => item.Quality)
					.HasMaxLength(50);

				entity.HasIndex(item => item.Name);
			});
		}

		private static void ConfigureSpells(
			ModelBuilder builder)
		{
			builder.Entity<Spell>(entity =>
			{
				entity.ToTable("Spells");

				entity.HasKey(spell => spell.Id);

				entity.Property(spell => spell.Name)
					.IsRequired()
					.HasMaxLength(100);

				entity.Property(spell => spell.Icon)
					.HasMaxLength(100);

				entity.Property(spell => spell.Description)
					.HasMaxLength(2000);

				entity.Property(spell => spell.Url)
					.HasMaxLength(300);

				entity.Property(spell => spell.Quality)
					.IsRequired()
					.HasMaxLength(50)
					.HasDefaultValue("spell");

				entity.HasIndex(spell => spell.Name);
			});
		}

		private static void ConfigureUsers(
			ModelBuilder builder)
		{
			builder.Entity<User>(entity =>
			{
				entity.Property(user => user.FullName)
					.IsRequired()
					.HasMaxLength(200);

				entity.Property(user => user.AvatarPath)
					.HasMaxLength(260)
					.HasDefaultValue(
						"/images/avatars/default01.png");

				entity.Property(user => user.StripeCustomerId)
					.HasMaxLength(128);

				entity.HasOne(user => user.Cart)
					.WithOne(cart => cart.User)
					.HasForeignKey<Cart>(cart => cart.UserId)
					.OnDelete(DeleteBehavior.Cascade);

				entity.HasMany(user => user.PaymentMethods)
					.WithOne(method => method.User)
					.HasForeignKey(method => method.UserId)
					.OnDelete(DeleteBehavior.Cascade);

				entity.HasMany(user => user.Transactions)
					.WithOne(transaction => transaction.User)
					.HasForeignKey(transaction =>
						transaction.UserId)
					.OnDelete(DeleteBehavior.Cascade);

				entity.HasMany(user => user.ProductReviews)
					.WithOne()
					.HasForeignKey(review => review.UserId)
					.OnDelete(DeleteBehavior.Cascade);

				entity.HasMany(user => user.PromoRedemptions)
					.WithOne(redemption => redemption.User)
					.HasForeignKey(redemption =>
						redemption.UserId)
					.OnDelete(DeleteBehavior.Cascade);
			});
		}

		private static void ConfigureCarts(
			ModelBuilder builder)
		{
			builder.Entity<Cart>(entity =>
			{
				entity.ToTable("Carts");

				entity.HasKey(cart => cart.Id);

				entity.Property(cart => cart.UserId)
					.IsRequired();

				entity.Property(cart => cart.IsArchived)
					.IsRequired();

				entity.Property(cart => cart.OrderDate)
					.HasMaxLength(100);

				entity.Property(cart => cart.UpdatedOn)
					.IsRequired();

				entity.HasIndex(cart => cart.UserId)
					.IsUnique();
			});

			builder.Entity<CartProduct>(entity =>
			{
				entity.ToTable("CartProducts");

				entity.HasKey(cartProduct => new
				{
					cartProduct.ProductId,
					cartProduct.CartId
				});

				entity.Property(cartProduct =>
						cartProduct.ProductId)
					.IsRequired();

				entity.Property(cartProduct =>
						cartProduct.Quantity)
					.IsRequired();

				entity.HasOne(cartProduct =>
						cartProduct.Cart)
					.WithMany(cart => cart.CartProducts)
					.HasForeignKey(cartProduct =>
						cartProduct.CartId)
					.OnDelete(DeleteBehavior.Cascade);

				entity.HasOne(cartProduct =>
						cartProduct.Product)
					.WithMany(product => product.Carts)
					.HasForeignKey(cartProduct =>
						cartProduct.ProductId)
					.OnDelete(DeleteBehavior.Cascade);
			});
		}

		private static void ConfigureDiscussions(
			ModelBuilder builder)
		{
			builder.Entity<DiscussionPost>(entity =>
			{
				entity.ToTable("DiscussionPosts");

				entity.HasKey(post => post.Id);

				entity.Property(post => post.Title)
					.IsRequired()
					.HasMaxLength(120);

				entity.Property(post => post.Content)
					.IsRequired();

				entity.Property(post => post.AuthorId)
					.IsRequired();

				entity.Property(post => post.CreatedOn)
					.IsRequired();

				entity.HasOne(post => post.Author)
					.WithMany()
					.HasForeignKey(post => post.AuthorId)
					.OnDelete(DeleteBehavior.Cascade);

				entity.HasIndex(post => post.CreatedOn);
				entity.HasIndex(post => post.AuthorId);
			});

			builder.Entity<DiscussionComment>(entity =>
			{
				entity.ToTable("DiscussionComments");

				entity.HasKey(comment => comment.Id);

				entity.Property(comment => comment.AuthorId)
					.IsRequired();

				entity.Property(comment => comment.Content)
					.IsRequired();

				entity.Property(comment => comment.CreatedOn)
					.IsRequired();

				entity.HasOne(comment => comment.Post)
					.WithMany(post => post.Comments)
					.HasForeignKey(comment => comment.PostId)
					.OnDelete(DeleteBehavior.Cascade);

				entity.HasOne(comment => comment.Author)
					.WithMany()
					.HasForeignKey(comment =>
						comment.AuthorId)
					.OnDelete(DeleteBehavior.Cascade);

				entity.HasIndex(comment => comment.PostId);
				entity.HasIndex(comment => comment.AuthorId);
			});

			builder.Entity<DiscussionLike>(entity =>
			{
				entity.ToTable("DiscussionLikes");

				entity.HasKey(like => like.Id);

				entity.Property(like => like.UserId)
					.IsRequired();

				entity.HasOne(like => like.Post)
					.WithMany(post =>
						post.LikesCollection)
					.HasForeignKey(like => like.PostId)
					.OnDelete(DeleteBehavior.Cascade);

				entity.HasOne(like => like.User)
					.WithMany()
					.HasForeignKey(like => like.UserId)
					.OnDelete(DeleteBehavior.Cascade);

				entity.HasIndex(like => new
				{
					like.PostId,
					like.UserId
				})
					.IsUnique();
			});

			builder.Entity<DiscussionCommentLike>(entity =>
			{
				entity.ToTable("DiscussionCommentLikes");

				entity.HasKey(like => like.Id);

				entity.Property(like => like.UserId)
					.IsRequired();

				entity.HasOne(like => like.Comment)
					.WithMany(comment =>
						comment.LikesCollection)
					.HasForeignKey(like =>
						like.CommentId)
					.OnDelete(DeleteBehavior.Cascade);

				entity.HasOne(like => like.User)
					.WithMany()
					.HasForeignKey(like => like.UserId)
					.OnDelete(DeleteBehavior.Cascade);

				entity.HasIndex(like => new
				{
					like.CommentId,
					like.UserId
				})
					.IsUnique();
			});
		}

		private static void ConfigureProducts(
			ModelBuilder builder)
		{
			builder.Entity<Product>(entity =>
			{
				entity.ToTable("Products");

				entity.HasKey(product => product.Id);

				entity.Property(product => product.Id)
					.IsRequired();

				entity.Property(product => product.Name)
					.IsRequired()
					.HasMaxLength(100);

				entity.Property(product => product.Price)
					.IsRequired()
					.HasColumnType("numeric(18,2)");

				entity.Property(product => product.Category)
					.IsRequired()
					.HasMaxLength(50)
					.HasDefaultValue("Other");

				entity.Property(product =>
						product.Description)
					.HasMaxLength(1000);

				entity.HasIndex(product => product.Category);
				entity.HasIndex(product => product.Name);

				entity.HasOne(product =>
						product.ThumbnailImage)
					.WithMany()
					.HasForeignKey(product =>
						product.ThumbnailImageId)
					.OnDelete(DeleteBehavior.SetNull);

				entity.HasIndex(product =>
						product.ThumbnailImageId)
					.HasDatabaseName(
						"IX_Products_ThumbnailImageId");
			});

			builder.Entity<ProductImage>(entity =>
			{
				entity.ToTable("ProductImages");

				entity.HasKey(image => image.Id);

				entity.Property(image => image.ProductId)
					.IsRequired();

				entity.Property(image => image.Url)
					.IsRequired()
					.HasMaxLength(2048);

				entity.Property(image => image.SortOrder)
					.IsRequired();

				entity.Property(image => image.AltText)
					.HasMaxLength(300);

				entity.Property(image => image.CreatedAt)
					.IsRequired();

				entity.HasIndex(image => new
				{
					image.ProductId,
					image.SortOrder
				})
					.IsUnique()
					.HasDatabaseName(
						"UX_ProductImages_Product_SortOrder");

				entity.HasOne(image => image.Product)
					.WithMany(product => product.Images)
					.HasForeignKey(image => image.ProductId)
					.OnDelete(DeleteBehavior.Cascade);
			});

			builder.Entity<ProductReview>(entity =>
			{
				entity.ToTable("ProductReviews");

				entity.HasKey(review => review.Id);

				entity.Property(review => review.ProductId)
					.IsRequired();

				entity.Property(review => review.UserId)
					.IsRequired();

				entity.Property(review => review.Content)
					.IsRequired()
					.HasMaxLength(2000);

				entity.Property(review => review.Rating)
					.IsRequired();

				entity.Property(review => review.CreatedAt)
					.IsRequired();

				entity.HasIndex(review => new
				{
					review.ProductId,
					review.UserId
				})
					.IsUnique();

				entity.HasOne(review => review.Product)
					.WithMany(product => product.Reviews)
					.HasForeignKey(review =>
						review.ProductId)
					.OnDelete(DeleteBehavior.Cascade);
			});
		}

		private static void ConfigureTalents(
			ModelBuilder builder)
		{
			builder.Entity<TalentNodeState>(entity =>
			{
				entity.ToTable("TalentNodeStates");

				entity.HasKey(state => state.Id);

				entity.Property(state => state.TreeKey)
					.IsRequired()
					.HasMaxLength(100);

				entity.Property(state => state.NodeId)
					.IsRequired()
					.HasMaxLength(100);

				entity.Property(state => state.IsActive)
					.IsRequired();

				entity.HasIndex(state => new
				{
					state.TreeKey,
					state.NodeId
				})
					.IsUnique();
			});

			builder.Entity<TalentBuild>(entity =>
			{
				entity.ToTable("TalentBuilds");

				entity.HasKey(build => build.Id);

				entity.Property(build => build.TreeKey)
					.IsRequired()
					.HasMaxLength(100);

				entity.Property(build => build.Name)
					.IsRequired()
					.HasMaxLength(100);

				entity.Property(build => build.IsDefault)
					.IsRequired();

				entity.Property(build => build.CreatedAt)
					.IsRequired();

				entity.HasIndex(build => new
				{
					build.TreeKey,
					build.Name
				})
					.IsUnique();
			});

			builder.Entity<TalentBuildNode>(entity =>
			{
				entity.ToTable("TalentBuildNodes");

				entity.HasKey(node => node.Id);

				entity.Property(node => node.NodeId)
					.IsRequired()
					.HasMaxLength(100);

				entity.Property(node => node.IsActive)
					.IsRequired();

				entity.HasIndex(node => new
				{
					node.BuildId,
					node.NodeId
				})
					.IsUnique();

				entity.HasOne(node => node.Build)
					.WithMany(build => build.Nodes)
					.HasForeignKey(node => node.BuildId)
					.OnDelete(DeleteBehavior.Cascade);
			});
		}

		private static void ConfigurePageBuilder(
			ModelBuilder builder)
		{
			builder.Entity<ContentPage>(entity =>
			{
				entity.ToTable("ContentPages");

				entity.HasKey(page => page.Id);

				entity.Property(page => page.Section)
					.IsRequired()
					.HasMaxLength(50);

				entity.Property(page => page.Slug)
					.IsRequired()
					.HasMaxLength(100);

				entity.Property(page => page.Title)
					.IsRequired()
					.HasMaxLength(200);

				entity.Property(page => page.IsPublished)
					.IsRequired();

				entity.Property(page => page.JsonLayout)
					.IsRequired();

				entity.Property(page => page.CreatedAt)
					.IsRequired();

				entity.Property(page => page.UpdatedAt)
					.IsRequired();

				entity.Property(page => page.UpdatedBy)
					.HasMaxLength(100);

				entity.Property(page => page.RowVersion)
					.IsRequired()
					.IsConcurrencyToken()
					.ValueGeneratedNever()
					.HasColumnType("bytea");

				entity.HasIndex(page => new
				{
					page.Section,
					page.Slug
				})
					.IsUnique();
			});

			builder.Entity<DataPreset>(entity =>
			{
				entity.ToTable("DataPresets");

				entity.HasKey(preset => preset.Id);

				entity.Property(preset => preset.Name)
					.IsRequired()
					.HasMaxLength(150);

				entity.Property(preset => preset.Entity)
					.IsRequired()
					.HasMaxLength(50);

				entity.Property(preset => preset.Section)
					.HasMaxLength(50);

				entity.Property(preset => preset.JsonQuery)
					.IsRequired();

				entity.Property(preset => preset.CreatedAt)
					.IsRequired();

				entity.Property(preset => preset.UpdatedAt)
					.IsRequired();

				entity.HasIndex(preset => new
				{
					preset.Entity,
					preset.Name
				});
			});
		}

		private static void ConfigureTransactions(
			ModelBuilder builder)
		{
			builder.Entity<Transaction>(entity =>
			{
				entity.ToTable("Transactions");

				entity.HasKey(transaction => transaction.Id);

				entity.Property(transaction =>
						transaction.UserId)
					.IsRequired();

				entity.Property(transaction =>
						transaction.CreatedAtUtc)
					.IsRequired();

				entity.Property(transaction =>
						transaction.PurchaseTitle)
					.IsRequired()
					.HasMaxLength(160);

				entity.Property(transaction =>
						transaction.Amount)
					.IsRequired()
					.HasColumnType("numeric(18,2)");

				entity.Property(transaction =>
						transaction.Currency)
					.IsRequired()
					.HasMaxLength(3)
					.HasDefaultValue("USD");

				entity.Property(transaction =>
						transaction.Status)
					.IsRequired();

				entity.Property(transaction =>
						transaction.Region)
					.IsRequired()
					.HasMaxLength(32)
					.HasDefaultValue("US");

				entity.Property(transaction =>
						transaction.ExternalId)
					.HasMaxLength(80);

				entity.Property(transaction =>
						transaction.Type)
					.IsRequired()
					.HasDefaultValue(
						TransactionType.Unknown);

				entity.HasIndex(transaction => new
				{
					transaction.UserId,
					transaction.CreatedAtUtc
				});

				entity.HasIndex(transaction =>
					transaction.ExternalId);
			});
		}

		private static void ConfigurePaymentMethods(
			ModelBuilder builder)
		{
			builder.Entity<PaymentMethod>(entity =>
			{
				entity.ToTable("PaymentMethods");

				entity.HasKey(method => method.Id);

				entity.Property(method => method.Id)
					.IsRequired()
					.HasMaxLength(64);

				entity.Property(method => method.UserId)
					.IsRequired()
					.HasMaxLength(64);

				entity.Property(method => method.Brand)
					.IsRequired()
					.HasMaxLength(32);

				entity.Property(method => method.Last4)
					.IsRequired()
					.HasMaxLength(4);

				entity.Property(method => method.Label)
					.HasMaxLength(64);

				entity.Property(method => method.ExternalId)
					.HasMaxLength(64);

				entity.Property(method => method.Provider)
					.HasMaxLength(32);

				entity.Property(method => method.IsDefault)
					.IsRequired();

				entity.Property(method =>
						method.CreatedAtUtc)
					.IsRequired();

				entity.HasIndex(method => new
				{
					method.UserId,
					method.IsDefault
				});

				entity.HasIndex(method =>
					method.ExternalId);
			});
		}

		private static void ConfigurePromoCodes(
			ModelBuilder builder)
		{
			builder.Entity<PromoCode>(entity =>
			{
				entity.ToTable("PromoCodes");

				entity.HasKey(code => code.Id);

				entity.Property(code => code.Id)
					.IsRequired()
					.HasMaxLength(64);

				entity.Property(code => code.Code)
					.IsRequired()
					.HasMaxLength(64);

				entity.Property(code => code.Type)
					.IsRequired();

				entity.Property(code => code.Value)
					.IsRequired()
					.HasColumnType("numeric(18,2)");

				entity.Property(code => code.Currency)
					.HasMaxLength(3);

				entity.Property(code => code.UsedCount)
					.IsRequired();

				entity.Property(code => code.IsActive)
					.IsRequired();

				entity.Property(code => code.CreatedAtUtc)
					.IsRequired();

				entity.Property(code => code.Notes)
					.HasMaxLength(256);

				entity.HasIndex(code => code.Code)
					.IsUnique();
			});

			builder.Entity<PromoRedemption>(entity =>
			{
				entity.ToTable("PromoRedemptions");

				entity.HasKey(redemption =>
					redemption.Id);

				entity.Property(redemption =>
						redemption.Id)
					.IsRequired()
					.HasMaxLength(64);

				entity.Property(redemption =>
						redemption.PromoCodeId)
					.IsRequired()
					.HasMaxLength(64);

				entity.Property(redemption =>
						redemption.UserId)
					.IsRequired();

				entity.Property(redemption =>
						redemption.RedeemedAtUtc)
					.IsRequired();

				entity.Property(redemption =>
						redemption.AmountCredited)
					.HasColumnType("numeric(18,2)");

				entity.Property(redemption =>
						redemption.Currency)
					.HasMaxLength(3);

				entity.HasOne(redemption =>
						redemption.PromoCode)
					.WithMany()
					.HasForeignKey(redemption =>
						redemption.PromoCodeId)
					.OnDelete(DeleteBehavior.Cascade);

				entity.HasIndex(redemption => new
				{
					redemption.PromoCodeId,
					redemption.UserId
				})
					.IsUnique();
			});
		}

		private void UpdateContentPageRowVersions()
		{
			foreach (var entry in ChangeTracker
						 .Entries<ContentPage>())
			{
				if (entry.State != EntityState.Added &&
					entry.State != EntityState.Modified)
				{
					continue;
				}

				entry.Entity.RowVersion =
					Guid.NewGuid().ToByteArray();
			}
		}
	}
}
