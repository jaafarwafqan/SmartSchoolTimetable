using SmartSchoolTimetable.Application;
using SmartSchoolTimetable.Domain;
using SmartSchoolTimetable.Infrastructure;

namespace SmartSchoolTimetable.Tests;

public sealed class LocalAuthServiceTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task FailedLoginDelayRunsAfterTheOperationGateIsReleased()
    {
        using var gate = new SemaphoreSlim(1, 1);
        var delay = new BlockingLoginDelay();
        var hasher = new Pbkdf2CredentialHasher();
        var (passwordSalt, passwordHash) = hasher.HashPassword("Correct-Pass-1");
        var (recoverySalt, recoveryHash) = hasher.HashRecoveryCode("RECOVERYCODE");
        var owner = OwnerAccount.Create(
            "owner",
            "OWNER",
            passwordSalt,
            passwordHash,
            hasher.CurrentPasswordIterations,
            recoverySalt,
            recoveryHash,
            DateTimeOffset.UnixEpoch);
        var service = new LocalAuthService(
            new SingleOwnerRepository(owner),
            hasher,
            new LocalSessionStore(TimeProvider.System),
            delay,
            TimeProvider.System,
            defaultInactivityTimeout: null,
            gate);

        var failedLogin = service.LoginAsync("owner", "Wrong-Pass-1", CancellationToken.None);
        await delay.Entered.Task.WaitAsync(TestTimeout);

        Assert.Equal(1, gate.CurrentCount);
        var concurrentLogin = await service.LoginAsync("owner", "Correct-Pass-1", CancellationToken.None)
            .WaitAsync(TestTimeout);
        Assert.True(concurrentLogin.Succeeded);
        Assert.False(failedLogin.IsCompleted);

        delay.Release();
        var failed = await failedLogin.WaitAsync(TestTimeout);
        Assert.Equal(ErrorCodes.InvalidCredentials, failed.ErrorCode);
        Assert.Equal([LocalAuthService.FailedLoginDelay], delay.Requested);
    }

    private sealed class BlockingLoginDelay : ILoginDelay
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<TimeSpan> Requested { get; } = [];

        public Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            Requested.Add(delay);
            Entered.TrySetResult();
            return _release.Task.WaitAsync(cancellationToken);
        }

        public void Release() => _release.TrySetResult();
    }

    private sealed class SingleOwnerRepository(OwnerAccount owner) : IOwnerRepository
    {
        public Task<OwnerAccount?> GetOwnerAsync(CancellationToken cancellationToken) =>
            Task.FromResult<OwnerAccount?>(owner);

        public Task CreateOwnerAsync(OwnerAccount account, LocalAuditEntry auditEntry, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task SaveOwnerAsync(OwnerAccount account, LocalAuditEntry? auditEntry, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
