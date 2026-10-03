using SmartSchoolTimetable.Domain;

namespace SmartSchoolTimetable.Application;

public interface IOwnerRepository
{
    Task<OwnerAccount?> GetOwnerAsync(CancellationToken cancellationToken);
    Task CreateOwnerAsync(OwnerAccount owner, LocalAuditEntry auditEntry, CancellationToken cancellationToken);
    Task SaveOwnerAsync(OwnerAccount owner, LocalAuditEntry? auditEntry, CancellationToken cancellationToken);
}
