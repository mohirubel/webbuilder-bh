using System.Collections.Concurrent;
using WebBuilders.Domains.Business.Abstractions;
using WebBuilders.Domains.Business.Models;

namespace WebBuilders.Domains.Data.Repositories;

/// <summary>Process-local order store, enough for the sandbox. Swap for a database-backed
/// <see cref="IOrderRepository"/> (EF Core, Dapper, ...) without touching the business layer.</summary>
internal sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly ConcurrentDictionary<string, Order> _orders = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _locks = new(StringComparer.Ordinal);

    public Task<Order?> GetByPaymentIntentAsync(string paymentIntentId, CancellationToken ct) =>
        Task.FromResult(_orders.TryGetValue(paymentIntentId, out var order) ? order : null);

    public Task<bool> TryLockAsync(string paymentIntentId, CancellationToken ct) =>
        Task.FromResult(_locks.TryAdd(paymentIntentId, 0));

    public Task ReleaseAsync(string paymentIntentId, CancellationToken ct)
    {
        _locks.TryRemove(paymentIntentId, out _);
        return Task.CompletedTask;
    }

    public Task SaveAsync(Order order, CancellationToken ct)
    {
        _orders[order.PaymentIntentId] = order;
        return Task.CompletedTask;
    }
}
