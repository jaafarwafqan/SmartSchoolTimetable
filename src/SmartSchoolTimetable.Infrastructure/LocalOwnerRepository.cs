using Microsoft.EntityFrameworkCore;
using SmartSchoolTimetable.Application;
using SmartSchoolTimetable.Domain;

namespace SmartSchoolTimetable.Infrastructure;

public sealed class LocalOwnerRepository(LocalDbContext dbContext) : IOwnerRepository
{
    public Task<OwnerAccount?> GetOwnerAsync(CancellationToken cancellationToken) =>
        dbContext.Owners.SingleOrDefaultAsync(account => account.OwnerSlot == "owner", cancellationToken);

    public async Task CreateOwnerAsync(
        OwnerAccount owner,
        LocalAuditEntry auditEntry,
        CancellationToken cancellationToken)
    {
        dbContext.Owners.Add(owner);
        dbContext.AuditEntries.Add(auditEntry);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveOwnerAsync(
        OwnerAccount owner,
        LocalAuditEntry? auditEntry,
        CancellationToken cancellationToken)
    {
        dbContext.Owners.Update(owner);
        if (auditEntry is not null)
            dbContext.AuditEntries.Add(auditEntry);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
