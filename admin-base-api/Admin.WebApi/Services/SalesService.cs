using Admin.Entities;
using Admin.Entities.Entities;
using Admin.WebApi.Models;
using Microsoft.EntityFrameworkCore;

namespace Admin.WebApi.Services
{
    public interface ISalesService
    {
        Task<SalesSummaryDto> GetSalesSummaryAsync(int photographerId, CancellationToken cancellationToken = default);
        Task<PagedResult<SaleItemDto>> GetSalesListAsync(int photographerId, SalesListQuery query, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<LiquidationDto>> GetLiquidationsAsync(int photographerId, CancellationToken cancellationToken = default);
        Task<WithdrawalResultDto> WithdrawAvailableAsync(int photographerId, CancellationToken cancellationToken = default);
    }

    public class SalesService : ISalesService
    {
        private const string PaidOutStatus = "PaidOut";

        private readonly Context _context;

        public SalesService(Context context)
        {
            _context = context;
        }

        public async Task<SalesSummaryDto> GetSalesSummaryAsync(int photographerId, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            var aggregate = await _context.Orders
                .AsNoTracking()
                .Where(o => o.PhotographerId == photographerId)
                .GroupBy(_ => 1)
                .Select(g => new SalesSummaryDto
                {
                    TotalSalesThisMonth = g.Where(o => o.CreatedAt >= monthStart).Sum(o => (decimal?)o.TotalAmount) ?? 0m,
                    TotalSalesAllTime = g.Sum(o => (decimal?)o.TotalAmount) ?? 0m,
                    PendingAmount = g.Where(o => o.Status != PaidOutStatus && o.ClearedAt > now).Sum(o => (decimal?)o.PhotographerNet) ?? 0m,
                    AvailableAmount = g.Where(o => o.Status != PaidOutStatus && o.ClearedAt <= now).Sum(o => (decimal?)o.PhotographerNet) ?? 0m,
                    TotalWithdrawn = g.Where(o => o.Status == PaidOutStatus).Sum(o => (decimal?)o.PhotographerNet) ?? 0m,
                    SalesCountThisMonth = g.Count(o => o.CreatedAt >= monthStart),
                    TotalSalesCount = g.Count()
                })
                .FirstOrDefaultAsync(cancellationToken);

            return aggregate ?? new SalesSummaryDto();
        }

        public async Task<PagedResult<SaleItemDto>> GetSalesListAsync(int photographerId, SalesListQuery query, CancellationToken cancellationToken = default)
        {
            var safePage = query.Page < 1 ? 1 : query.Page;
            var safePageSize = query.PageSize < 1 ? 20 : Math.Min(query.PageSize, 100);
            var now = DateTime.UtcNow;

            var baseQuery = _context.Orders
                .AsNoTracking()
                .Where(o => o.PhotographerId == photographerId);

            if (query.EventId.HasValue && query.EventId.Value > 0)
            {
                baseQuery = baseQuery.Where(o => o.EventId == query.EventId.Value);
            }

            if (query.FromDate.HasValue)
            {
                baseQuery = baseQuery.Where(o => o.CreatedAt >= query.FromDate.Value);
            }

            if (query.ToDate.HasValue)
            {
                baseQuery = baseQuery.Where(o => o.CreatedAt <= query.ToDate.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                var normalized = query.Status.Trim().ToUpperInvariant();
                if (normalized == "PAIDOUT")
                {
                    baseQuery = baseQuery.Where(o => o.Status == PaidOutStatus);
                }
                else if (normalized == "PENDING")
                {
                    baseQuery = baseQuery.Where(o => o.Status != PaidOutStatus && o.ClearedAt > now);
                }
                else if (normalized == "AVAILABLE")
                {
                    baseQuery = baseQuery.Where(o => o.Status != PaidOutStatus && o.ClearedAt <= now);
                }
            }

            var totalCount = await baseQuery.CountAsync(cancellationToken);

            var items = await baseQuery
                .OrderByDescending(o => o.CreatedAt)
                .Skip((safePage - 1) * safePageSize)
                .Take(safePageSize)
                .Select(o => new SaleItemDto
                {
                    OrderId = o.Id,
                    Date = o.CreatedAt,
                    EventId = o.EventId,
                    EventName = o.Event.Name,
                    PhotoTitle = o.Photo != null ? o.Photo.OriginalFileName : null,
                    TotalAmount = o.TotalAmount,
                    PlatformCommission = o.PlatformCommission,
                    PhotographerNet = o.PhotographerNet,
                    Status = o.Status == PaidOutStatus
                        ? "PaidOut"
                        : o.ClearedAt > now
                            ? "Pending"
                            : "Available"
                })
                .ToListAsync(cancellationToken);

            return new PagedResult<SaleItemDto>
            {
                Items = items,
                Page = safePage,
                PageSize = safePageSize,
                TotalCount = totalCount,
                TotalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)safePageSize)
            };
        }

        public async Task<IReadOnlyList<LiquidationDto>> GetLiquidationsAsync(int photographerId, CancellationToken cancellationToken = default)
        {
            var liquidations = await _context.Liquidations
                .AsNoTracking()
                .Where(l => l.PhotographerId == photographerId)
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => new LiquidationDto
                {
                    LiquidationId = l.Id,
                    LiquidationDate = l.CreatedAt,
                    Amount = l.Amount,
                    FromDate = l.FromDate,
                    ToDate = l.ToDate,
                    OrdersCount = _context.Orders.Count(o =>
                        o.PhotographerId == photographerId &&
                        o.Status == PaidOutStatus &&
                        o.PaidOutAt != null &&
                        o.PaidOutAt >= l.FromDate &&
                        o.PaidOutAt <= l.ToDate)
                })
                .ToListAsync(cancellationToken);

            return liquidations;
        }

        public async Task<WithdrawalResultDto> WithdrawAvailableAsync(int photographerId, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var availableOrders = await _context.Orders
                .Where(o => o.PhotographerId == photographerId && o.Status != PaidOutStatus && o.ClearedAt <= now)
                .ToListAsync(cancellationToken);

            if (availableOrders.Count == 0)
            {
                return new WithdrawalResultDto
                {
                    Success = false,
                    Message = "No tienes monto disponible para retirar.",
                    Amount = 0m,
                    ProcessedAt = now
                };
            }

            var amount = availableOrders.Sum(o => o.PhotographerNet);
            var fromDate = availableOrders.Min(o => o.CreatedAt);

            await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                foreach (var order in availableOrders)
                {
                    order.Status = PaidOutStatus;
                    order.PaidOutAt = now;
                }

                _context.Liquidations.Add(new Liquidation
                {
                    PhotographerId = photographerId,
                    Amount = amount,
                    FromDate = fromDate,
                    ToDate = now,
                    CreatedAt = now
                });

                var aggregates = await _context.Orders
                    .Where(o => o.PhotographerId == photographerId)
                    .GroupBy(o => o.PhotographerId)
                    .Select(g => new
                    {
                        PendingAmount = g.Where(o => o.Status != PaidOutStatus && o.ClearedAt > now).Sum(o => (decimal?)o.PhotographerNet) ?? 0m,
                        AvailableAmount = g.Where(o => o.Status != PaidOutStatus && o.ClearedAt <= now).Sum(o => (decimal?)o.PhotographerNet) ?? 0m,
                        TotalWithdrawn = g.Where(o => o.Status == PaidOutStatus).Sum(o => (decimal?)o.PhotographerNet) ?? 0m
                    })
                    .FirstAsync(cancellationToken);

                var balance = await _context.PhotographerBalances.FirstOrDefaultAsync(b => b.PhotographerId == photographerId, cancellationToken);
                if (balance == null)
                {
                    balance = new PhotographerBalance
                    {
                        PhotographerId = photographerId
                    };
                    _context.PhotographerBalances.Add(balance);
                }

                balance.PendingAmount = aggregates.PendingAmount;
                balance.AvailableAmount = aggregates.AvailableAmount;
                balance.TotalWithdrawn = aggregates.TotalWithdrawn;

                await _context.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);

                return new WithdrawalResultDto
                {
                    Success = true,
                    Message = "Retiro registrado correctamente.",
                    Amount = amount,
                    ProcessedAt = now
                };
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                return new WithdrawalResultDto
                {
                    Success = false,
                    Message = "No se pudo registrar el retiro.",
                    Amount = 0m,
                    ProcessedAt = now
                };
            }
        }
    }
}
