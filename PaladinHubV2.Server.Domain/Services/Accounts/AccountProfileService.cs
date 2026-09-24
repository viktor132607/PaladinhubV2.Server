using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Accounts;

public sealed class AccountProfileService : IAccountProfileService
{
    private readonly AppDbContext _db;

    public AccountProfileService(AppDbContext db)
    {
        _db = db;
    }

    public async Task MarkPhoneVerifiedAsync(
        User user,
        CancellationToken cancellationToken = default)
    {
        user.PhoneNumberConfirmed = true;
        _db.Update(user);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
