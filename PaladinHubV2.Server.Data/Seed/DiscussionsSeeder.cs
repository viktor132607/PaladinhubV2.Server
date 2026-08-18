using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Data.Seed.Contracts;

namespace PaladinHubV2.Server.Data.Seed
{
	public class DiscussionsSeeder : ISeeder
	{
		private readonly IServiceProvider _sp;
		public DiscussionsSeeder(IServiceProvider sp) => _sp = sp;

		public async Task SeedAsync()
		{
			using var scope = _sp.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
			var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

			var adminEmail = "iliev132607@gmail.com";
			var admin = await userManager.Users.FirstOrDefaultAsync(u => u.Email == adminEmail)
						?? throw new Exception("Admin user not found. Run UsersSeeder first.");

			var authorEmails = new[]
			{
				"mila.petkova@paladinhub.test",
				"ivan.dimitrov@paladinhub.test",
				"georgi.stoyanov@paladinhub.test",
				"elena.nikolova@paladinhub.test",
				"petar.ivanov@paladinhub.test",
				"raya.koleva@paladinhub.test",
				"dimitar.angelov@paladinhub.test",
				"vesela.marinova@paladinhub.test",
				"kalin.hristov@paladinhub.test",
				"sofia.georgieva@paladinhub.test"
			};

			var authors = new List<User>();
			foreach (var email in authorEmails)
			{
				var u = await userManager.FindByEmailAsync(email)
					?? throw new Exception($"Seed author not found: {email}. Run UsersSeeder first.");
				authors.Add(u);
			}

			var templates = new (string Title, string Content)[]
			{
				("Retribution opener rotation?", "How do you sequence abilities for the strongest burst on pull?"),
				("Prot BiS for Mythic+?", "Shield/trinket picks for +16 keys and above — what do you run?"),
				("Holy talents for raid", "Aura Mastery timings and beacon setup for better coordination."),
				("Solo world-content build", "Looking for more sustain vs elites — suggestions?"),
				("PvP Ret build", "Which talents do you take vs Shamans/Mages?"),
				("Macro list", "Share your favorite mouseover and focus macros."),
				("UI / WeakAuras", "Hunting for WAs for Crusading Strikes and Divine Toll."),
				("Gear progress check", "Here’s my armory — what would you upgrade first?"),
				("Mythic+ routes", "What routes feel best with the current affixes?"),
				("Testing the new tier set", "How do the 2pc/4pc feel — worth swapping?")
			};

			var authorIds = authors.Select(a => a.Id).ToHashSet();
			var existingPairs = await db.DiscussionPosts
				.Where(p => authorIds.Contains(p.AuthorId))
				.Select(p => new { p.AuthorId, p.Title })
				.ToListAsync();

			var existing = new HashSet<(string AuthorId, string Title)>(
				existingPairs.Select(x => (x.AuthorId, x.Title))
			);

			var rnd = new Random(42);
			var postsToAdd = new List<DiscussionPost>();

			for (int i = 0; i < templates.Length; i++)
			{
				var author = authors[i];
				var (title, content) = templates[i];

				if (existing.Contains((author.Id, title)))
					continue; 

				var post = new DiscussionPost
				{
					Title = title,
					Content = content,
					AuthorId = author.Id,
					CreatedOn = DateTime.UtcNow.AddMinutes(-(i + 1) * 13)
				};

				var commenter = authors[(i + 1) % authors.Count];

				post.Comments.Add(new DiscussionComment
				{
					Post = post,
					AuthorId = admin.Id,
					Content = "Good question! I’d run tests on a target dummy first."
				});
				post.Comments.Add(new DiscussionComment
				{
					Post = post,
					AuthorId = commenter.Id,
					Content = "+1, I use a similar approach and it works well."
				});

				var likeUserIds = new HashSet<string>();
				int likeCount = rnd.Next(2, 5);
				for (int k = 0; k < likeCount; k++)
				{
					var likerId = rnd.NextDouble() < 0.5
						? authors[rnd.Next(authors.Count)].Id
						: admin.Id;

					if (likeUserIds.Add(likerId))
						post.LikesCollection.Add(new DiscussionLike { Post = post, UserId = likerId });
				}
				post.Likes = post.LikesCollection.Count;

				existing.Add((author.Id, title));
				postsToAdd.Add(post);
			}

			if (postsToAdd.Count > 0)
			{
				await db.DiscussionPosts.AddRangeAsync(postsToAdd);
				await db.SaveChangesAsync();
			}
		}
	}
}
