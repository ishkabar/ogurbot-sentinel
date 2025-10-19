using Microsoft.Extensions.Logging;
using Ogur.Sentinel.Abstractions.Leaves;
using Ogur.Sentinel.Core.Time;

namespace Ogur.Sentinel.Worker.Services;


public sealed class LeaveService
{
    private readonly ILogger<LeaveService> _logger;

    // in-memory store; infra persystencję dorzucimy później
    private readonly Dictionary<ulong, LeaveRecord> _records = new();

    public LeaveService(ILogger<LeaveService> logger) => _logger = logger;

    public void Set(LeaveRecord rec) => _records[rec.UserId] = rec;

    public bool Clear(ulong userId) => _records.Remove(userId);

    public (int days, string remaining) GetRemaining(ulong userId)
    {
        if (!_records.TryGetValue(userId, out var rec)) return (0, "0 s");
        var days = TimeUtil.DaysUntil(rec.ReturnAtUtc);
        var rem = TimeUtil.RemainingPretty(rec.ReturnAtUtc);
        return (days, rem);
    }

    public IReadOnlyCollection<LeaveRecord> All() => _records.Values;
}