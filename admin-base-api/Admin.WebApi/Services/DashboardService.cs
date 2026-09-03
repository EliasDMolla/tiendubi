using Admin.Entities;
using Admin.Entities.Entities;
using Admin.WebApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Admin.WebApi.Services
{
    public interface IDashboardService
    {
        Task<DashboardSummaryDto> GetDashboardSummaryAsync(int photographerId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<EventSalesDto>> GetSalesByEventAsync(int photographerId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<SaleDetailDto>> GetRecentSaleDetailsAsync(int photographerId, int take = 50, CancellationToken cancellationToken = default);
        Task<int> ProcessClearedOrdersAsync(CancellationToken cancellationToken = default);
    }

    public class DashboardService : IDashboardService
    {
        private const string PaidStatus = "Paid";
        private const string PaidOutStatus = "PaidOut";

        private readonly Context _context;
        private readonly PaymentSettings _paymentSettings;

        public DashboardService(Context context, IOptions<PaymentSettings> paymentSettings)
        {
            _context = context;
            _paymentSettings = paymentSettings.Value;
        }

        public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(int photographerId, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var commissionFactor = 1m - (Math.Clamp(_paymentSettings.CommissionPercent, 0m, 100m) / 100m);

            var salesAggregate = await _context.PhotoSales
                .AsNoTracking()
                .Where(s => s.UserId == photographerId)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    TotalSalesThisMonth = g.Where(s => s.SoldAt >= monthStart).Sum(s => (decimal?)(s.TotalAmount * commissionFactor)) ?? 0m,
                    TotalSalesAllTime = g.Sum(s => (decimal?)(s.TotalAmount * commissionFactor)) ?? 0m,
                    PhotosSoldThisMonth = g.Where(s => s.SoldAt >= monthStart).Sum(s => (int?)s.Quantity) ?? 0,
                    TotalPhotosSold = g.Sum(s => (int?)s.Quantity) ?? 0
                })
                .FirstOrDefaultAsync(cancellationToken);

            var balance = await _context.PhotographerBalances
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.PhotographerId == photographerId, cancellationToken);

            var pendingTransferAmount = await _context.PhotoSales
                .AsNoTracking()
                .Where(s => s.UserId == photographerId && s.Status == "pending_confirmation")
                .SumAsync(s => (decimal?)(s.TotalAmount * commissionFactor), cancellationToken) ?? 0m;

            var nextAvailableAt = await _context.Orders
                .AsNoTracking()
                .Where(o => o.PhotographerId == photographerId && o.Status != PaidOutStatus && o.ClearedAt > now)
                .MinAsync(o => (DateTime?)o.ClearedAt, cancellationToken);

            var activeEventsCount = await _context.PhotographerEvents
                .AsNoTracking()
                .CountAsync(e => e.UserId == photographerId && e.IsPublished, cancellationToken);

            return new DashboardSummaryDto
            {
                TotalSalesThisMonth = salesAggregate?.TotalSalesThisMonth ?? 0m,
                TotalSalesAllTime = salesAggregate?.TotalSalesAllTime ?? 0m,
                PendingAmount = (balance?.PendingAmount ?? 0m) + pendingTransferAmount,
                AvailableAmount = balance?.AvailableAmount ?? 0m,
                NextAvailableAt = nextAvailableAt,
                TotalWithdrawn = balance?.TotalWithdrawn ?? 0m,
                PhotosSoldThisMonth = salesAggregate?.PhotosSoldThisMonth ?? 0,
                TotalPhotosSold = salesAggregate?.TotalPhotosSold ?? 0,
                ActiveEventsCount = activeEventsCount
            };
        }

        public async Task<IReadOnlyList<EventSalesDto>> GetSalesByEventAsync(int photographerId, CancellationToken cancellationToken = default)
        {
            var commissionFactor = 1m - (Math.Clamp(_paymentSettings.CommissionPercent, 0m, 100m) / 100m);

            var eventSales = await _context.PhotoSales
                .AsNoTracking()
                .Where(s => s.UserId == photographerId)
                .GroupBy(s => new { EventId = s.PhotographerEventId, EventName = s.PhotographerEvent.Name })
                .Select(g => new EventSalesDto
                {
                    EventId = g.Key.EventId,
                    EventName = g.Key.EventName,
                    TotalSales = g.Sum(s => s.TotalAmount * commissionFactor),
                    PhotosSold = g.Sum(s => s.Quantity),
                    PendingAmount = g.Where(s => s.Status == "pending_confirmation").Sum(s => (decimal?)(s.TotalAmount * commissionFactor)) ?? 0m,
                    AvailableAmount = g.Where(s => s.Status == "paid").Sum(s => (decimal?)(s.TotalAmount * commissionFactor)) ?? 0m
                })
                .OrderByDescending(x => x.TotalSales)
                .ToListAsync(cancellationToken);

            return eventSales;
        }

        public async Task<IReadOnlyList<SaleDetailDto>> GetRecentSaleDetailsAsync(int photographerId, int take = 50, CancellationToken cancellationToken = default)
        {
            var safeTake = Math.Clamp(take, 1, 200);
            var commissionFactor = 1m - (Math.Clamp(_paymentSettings.CommissionPercent, 0m, 100m) / 100m);

            var salesRaw = await _context.PhotoSales
                .AsNoTracking()
                .Where(s => s.UserId == photographerId)
                .OrderByDescending(s => s.SoldAt)
                .Take(safeTake)
                .Select(s => new
                {
                    s.Id,
                    s.SoldAt,
                    EventName = s.PhotographerEvent.Name,
                    s.BuyerName,
                    s.BuyerEmail,
                    s.PaymentMethod,
                    s.TotalAmount,
                    s.Quantity,
                    s.Status,
                    s.PhotographerEventId
                })
                .ToListAsync(cancellationToken);

            var details = new List<SaleDetailDto>(salesRaw.Count);

            foreach (var sale in salesRaw)
            {
                var dto = new SaleDetailDto
                {
                    SaleId = sale.Id,
                    SoldAt = sale.SoldAt,
                    EventName = sale.EventName,
                    BuyerName = sale.BuyerName,
                    BuyerEmail = sale.BuyerEmail,
                    PaymentMethod = sale.PaymentMethod,
                    TotalAmount = sale.TotalAmount * commissionFactor,
                    Quantity = sale.Quantity,
                    Status = sale.Status
                };

                var sessionQuery = _context.PhotoCheckoutSessions
                    .AsNoTracking()
                    .Where(s =>
                        s.PhotographerId == photographerId &&
                        s.EventId == sale.PhotographerEventId &&
                        s.TotalAmount == sale.TotalAmount);

                if (!string.IsNullOrWhiteSpace(sale.BuyerEmail))
                {
                    var buyerEmail = sale.BuyerEmail.Trim().ToLower();
                    sessionQuery = sessionQuery.Where(s => s.BuyerEmail.ToLower() == buyerEmail);
                }

                var session = await sessionQuery
                    .OrderByDescending(s => s.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);

                if (session != null)
                {
                    var photoIds = ParsePhotoIdsCsv(session.PhotoIdsCsv).Distinct().ToList();
                    if (photoIds.Count > 0)
                    {
                        var photoLabels = await _context.EventPhotos
                            .AsNoTracking()
                            .Where(p => photoIds.Contains(p.Id))
                            .Select(p => new SalePurchasedPhotoDto
                            {
                                PhotoId = p.Id,
                                Label = string.IsNullOrWhiteSpace(p.OriginalFileName) ? $"Foto #{p.Id}" : p.OriginalFileName
                            })
                            .ToListAsync(cancellationToken);

                        dto.PurchasedPhotos = photoLabels
                            .OrderBy(p => p.PhotoId)
                            .ToList();
                    }
                }

                details.Add(dto);
            }

            return details;
        }

        private static IEnumerable<int> ParsePhotoIdsCsv(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv))
                return Array.Empty<int>();

            return csv
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => int.TryParse(value, out var id) ? id : 0)
                .Where(id => id > 0);
        }

        public async Task<int> ProcessClearedOrdersAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;

            var affectedPhotographers = await _context.Orders
                .AsNoTracking()
                .Where(o => o.Status == PaidStatus && o.ClearedAt <= now)
                .Select(o => o.PhotographerId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (affectedPhotographers.Count == 0)
            {
                return 0;
            }

            var aggregates = await _context.Orders
                .AsNoTracking()
                .Where(o => affectedPhotographers.Contains(o.PhotographerId))
                .GroupBy(o => o.PhotographerId)
                .Select(g => new
                {
                    PhotographerId = g.Key,
                    PendingAmount = g.Where(o => o.Status == PaidStatus && o.ClearedAt > now).Sum(o => (decimal?)o.PhotographerNet) ?? 0m,
                    AvailableAmount = g.Where(o => o.Status == PaidStatus && o.ClearedAt <= now).Sum(o => (decimal?)o.PhotographerNet) ?? 0m,
                    TotalWithdrawn = g.Where(o => o.Status == PaidOutStatus).Sum(o => (decimal?)o.PhotographerNet) ?? 0m
                })
                .ToListAsync(cancellationToken);

            var balancesByPhotographer = await _context.PhotographerBalances
                .Where(b => affectedPhotographers.Contains(b.PhotographerId))
                .ToDictionaryAsync(b => b.PhotographerId, cancellationToken);

            foreach (var aggregate in aggregates)
            {
                if (!balancesByPhotographer.TryGetValue(aggregate.PhotographerId, out var balance))
                {
                    balance = new PhotographerBalance
                    {
                        PhotographerId = aggregate.PhotographerId
                    };
                    _context.PhotographerBalances.Add(balance);
                }

                balance.PendingAmount = aggregate.PendingAmount;
                balance.AvailableAmount = aggregate.AvailableAmount;
                balance.TotalWithdrawn = aggregate.TotalWithdrawn;
            }

            await _context.SaveChangesAsync(cancellationToken);
            return aggregates.Count;
        }
    }
}