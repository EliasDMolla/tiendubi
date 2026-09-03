using Admin.Entities;
using Admin.Entities.Entities;
using Microsoft.EntityFrameworkCore;

namespace Admin.WebApi.Repositories
{
    public interface IPhotographerMercadoPagoAccountRepository
    {
        Task<PhotographerMercadoPagoAccount?> GetByPhotographerIdAsync(int photographerId, CancellationToken cancellationToken = default);
        Task UpsertAsync(PhotographerMercadoPagoAccount account, CancellationToken cancellationToken = default);
    }

    public class PhotographerMercadoPagoAccountRepository : IPhotographerMercadoPagoAccountRepository
    {
        private readonly Context _context;

        public PhotographerMercadoPagoAccountRepository(Context context)
        {
            _context = context;
        }

        public Task<PhotographerMercadoPagoAccount?> GetByPhotographerIdAsync(int photographerId, CancellationToken cancellationToken = default)
        {
            return _context.PhotographerMercadoPagoAccounts
                .FirstOrDefaultAsync(a => a.PhotographerId == photographerId, cancellationToken);
        }

        public async Task UpsertAsync(PhotographerMercadoPagoAccount account, CancellationToken cancellationToken = default)
        {
            var existing = await _context.PhotographerMercadoPagoAccounts
                .FirstOrDefaultAsync(a => a.PhotographerId == account.PhotographerId, cancellationToken);

            if (existing == null)
            {
                await _context.PhotographerMercadoPagoAccounts.AddAsync(account, cancellationToken);
            }
            else
            {
                existing.AccessToken = account.AccessToken;
                existing.RefreshToken = account.RefreshToken;
                existing.PublicKey = account.PublicKey;
                existing.MercadoPagoUserId = account.MercadoPagoUserId;
                existing.TokenExpiration = account.TokenExpiration;
                existing.IsActive = account.IsActive;
                existing.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
