using Admin.Entities;
using Admin.Entities.Entities;
using Admin.WebApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Admin.WebApi.Services
{
    public interface IAdminService
    {
        // Dashboard Metrics
        Task<AdminDashboardDto> GetDashboardMetricsAsync();
        
        // User Management
        Task<AdminUsersPagedDto> GetUsersAsync(int page, int pageSize, string? search, string? planFilter, string? roleFilter, string? statusFilter, string? sortBy, string? sortDir);
        Task<AdminUserDetailDto?> GetUserDetailAsync(int userId);
        Task<bool> ToggleUserStatusAsync(int adminUserId, int targetUserId, bool activate, string ipAddress);
        Task<bool> ChangeUserRoleAsync(int adminUserId, int targetUserId, UserRole newRole, string ipAddress);
        Task<bool> ChangeUserPlanAsync(int adminUserId, int targetUserId, PlanType newPlan, string ipAddress);
        
        // User Deletion
        Task<(bool Success, string Message)> DeleteUserAsync(int adminUserId, int targetUserId, string ipAddress);
        
        // Audit
        Task<List<AdminActionDto>> GetRecentActionsAsync(int count = 20);

        // Owner finance
        Task<OwnerGlobalSalesSummaryDto> GetOwnerGlobalSalesSummaryAsync(CancellationToken cancellationToken = default);
        Task<OwnerTransferApprovalListDto> GetPendingTransferApprovalsAsync(CancellationToken cancellationToken = default);
        Task<(bool Success, string Message)> ApproveTransferSaleAsync(int adminUserId, string externalReference, int clearanceHours, string ipAddress, CancellationToken cancellationToken = default);
        Task<OwnerPhotoDeliveryFailuresDto> GetFailedPhotoDeliveriesAsync(CancellationToken cancellationToken = default);
        Task<(bool Success, string Message)> RetryPhotoDeliveryAsync(string externalReference, CancellationToken cancellationToken = default);
        Task<OwnerAccreditationSummaryDto> GetOwnerAccreditationsAsync(CancellationToken cancellationToken = default);
        Task<(bool Success, string Message)> MarkPhotographerAccreditationPaidOutAsync(int adminUserId, int photographerId, string? note, string ipAddress, CancellationToken cancellationToken = default);
    }

    public class AdminService : IAdminService
    {
        private const string PaidOrderStatus = "Paid";

        private readonly Context _context;
        private readonly ILogger<AdminService> _logger;
        private readonly IEmailService _emailService;
        private readonly IPhotoDeliveryService _photoDeliveryService;
        private readonly PaymentSettings _paymentSettings;

        public AdminService(Context context, ILogger<AdminService> logger, IEmailService emailService, IPhotoDeliveryService photoDeliveryService, IOptions<PaymentSettings> paymentSettings)
        {
            _context = context;
            _logger = logger;
            _emailService = emailService;
            _photoDeliveryService = photoDeliveryService;
            _paymentSettings = paymentSettings.Value;
        }

        // ========================================
        // Dashboard Metrics
        // ========================================
        
        public async Task<AdminDashboardDto> GetDashboardMetricsAsync()
        {
            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var startOfPrevMonth = startOfMonth.AddMonths(-1);
            
            // Total users
            var totalUsers = await _context.Users.CountAsync();
            var totalUsersPrevMonth = await _context.Users.CountAsync(u => u.CreatedAt < startOfMonth);
            
            // New users this month
            var newUsersThisMonth = await _context.Users.CountAsync(u => u.CreatedAt >= startOfMonth);
            var newUsersPrevMonth = await _context.Users.CountAsync(u => u.CreatedAt >= startOfPrevMonth && u.CreatedAt < startOfMonth);
            
            // Plan distribution
            var planDistribution = await _context.Users
                .GroupBy(u => u.PlanType)
                .Select(g => new PlanDistributionDto
                {
                    Plan = g.Key.ToString(),
                    Count = g.Count()
                })
                .ToListAsync();
            
            // Recent registrations (last 7 days)
            var sevenDaysAgo = now.AddDays(-7);
            var recentRegistrations = await _context.Users
                .Where(u => u.CreatedAt >= sevenDaysAgo)
                .OrderByDescending(u => u.CreatedAt)
                .Take(10)
                .Select(u => new RecentUserDto
                {
                    Id = u.Id,
                    FullName = u.FullName ?? u.Email,
                    Email = u.Email,
                    PlanType = u.PlanType.ToString(),
                    CreatedAt = u.CreatedAt
                })
                .ToListAsync();
            
            // Monthly growth data (last 6 months)
            var sixMonthsAgo = now.AddMonths(-6);
            var monthlyGrowth = await _context.Users
                .Where(u => u.CreatedAt >= sixMonthsAgo)
                .GroupBy(u => new { u.CreatedAt.Year, u.CreatedAt.Month })
                .Select(g => new MonthlyGrowthDto
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    NewUsers = g.Count()
                })
                .OrderBy(g => g.Year)
                .ThenBy(g => g.Month)
                .ToListAsync();

            var totalStorageUsedBytes = await _context.EventPhotos
                .SumAsync(p => (long?)p.SizeBytes) ?? 0;
            
            // Calculate growth percentages
            var userGrowth = totalUsersPrevMonth > 0 
                ? Math.Round((double)(totalUsers - totalUsersPrevMonth) / totalUsersPrevMonth * 100, 1) 
                : 0;
            
            var newUsersGrowth = newUsersPrevMonth > 0 
                ? Math.Round((double)(newUsersThisMonth - newUsersPrevMonth) / newUsersPrevMonth * 100, 1) 
                : 0;

            return new AdminDashboardDto
            {
                TotalUsers = totalUsers,
                UserGrowthPercent = userGrowth,
                NewUsersThisMonth = newUsersThisMonth,
                NewUsersGrowthPercent = newUsersGrowth,
                TotalStorageUsedBytes = totalStorageUsedBytes,
                PlanDistribution = planDistribution,
                RecentRegistrations = recentRegistrations,
                MonthlyGrowth = monthlyGrowth
            };
        }

        // ========================================
        // User Management
        // ========================================
        
        public async Task<AdminUsersPagedDto> GetUsersAsync(int page, int pageSize, string? search, string? planFilter, string? roleFilter, string? statusFilter, string? sortBy, string? sortDir)
        {
            var query = _context.Users.AsQueryable();
            
            // Search filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(u => 
                    u.Email.ToLower().Contains(searchLower) ||
                    (u.FullName != null && u.FullName.ToLower().Contains(searchLower)));
            }
            
            // Plan filter
            if (!string.IsNullOrWhiteSpace(planFilter) && Enum.TryParse<PlanType>(planFilter, true, out var plan))
            {
                query = query.Where(u => u.PlanType == plan);
            }
            
            // Role filter
            if (!string.IsNullOrWhiteSpace(roleFilter) && Enum.TryParse<UserRole>(roleFilter, true, out var role))
            {
                query = query.Where(u => u.Role == role);
            }
            
            // Status filter
            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                if (statusFilter.Equals("active", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(u => u.IsActive);
                else if (statusFilter.Equals("inactive", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(u => !u.IsActive);
            }
            
            // Total count before sorting/paging
            var totalCount = await query.CountAsync();
            
            // Sorting
            query = (sortBy?.ToLower(), sortDir?.ToLower()) switch
            {
                ("email", "asc") => query.OrderBy(u => u.Email),
                ("email", _) => query.OrderByDescending(u => u.Email),
                ("fullname", "asc") => query.OrderBy(u => u.FullName),
                ("fullname", _) => query.OrderByDescending(u => u.FullName),
                ("plantype", "asc") => query.OrderBy(u => u.PlanType),
                ("plantype", _) => query.OrderByDescending(u => u.PlanType),
                ("lastlogin", "asc") => query.OrderBy(u => u.LastLogin),
                ("lastlogin", _) => query.OrderByDescending(u => u.LastLogin),
                ("createdat", "asc") => query.OrderBy(u => u.CreatedAt),
                _ => query.OrderByDescending(u => u.CreatedAt)
            };
            
            // Pagination
            var users = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new AdminUserListDto
                {
                    Id = u.Id,
                    Email = u.Email,
                    FullName = u.FullName,
                    IsActive = u.IsActive,
                    Role = u.Role.ToString(),
                    PlanType = u.PlanType.ToString(),
                    IsProActive = (u.PlanType == PlanType.PRO && u.ProSubscriptionEndDate > DateTime.UtcNow) ||
                                  (u.PlanType == PlanType.PRO_TRIAL && u.TrialEndDate > DateTime.UtcNow),
                    StorageUsedBytes = _context.EventPhotos
                        .Where(p => p.PhotographerEvent.UserId == u.Id)
                        .Sum(p => (long?)p.SizeBytes) ?? 0,
                    CreatedAt = u.CreatedAt,
                    LastLogin = u.LastLogin
                })
                .ToListAsync();
            
            return new AdminUsersPagedDto
            {
                Users = users,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            };
        }

        public async Task<AdminUserDetailDto?> GetUserDetailAsync(int userId)
        {
            var user = await _context.Users
                .Include(u => u.UsageType)
                .FirstOrDefaultAsync(u => u.Id == userId);
            
            if (user == null) return null;

            var now = DateTime.UtcNow;
            
            // Get admin actions on this user
            var recentActions = await _context.AdminActions
                .Where(a => a.TargetUserId == userId)
                .OrderByDescending(a => a.CreatedAt)
                .Take(10)
                .Select(a => new AdminActionDto
                {
                    Id = a.Id,
                    AdminUserName = a.AdminUser.FullName ?? a.AdminUser.Email,
                    ActionType = a.ActionType.ToString(),
                    Description = a.Description,
                    OldValue = a.OldValue,
                    NewValue = a.NewValue,
                    CreatedAt = a.CreatedAt
                })
                .ToListAsync();

            return new AdminUserDetailDto
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber,
                IsActive = user.IsActive,
                EmailVerified = user.EmailVerified,
                Role = user.Role.ToString(),
                PlanType = user.PlanType.ToString(),
                IsProActive = user.IsProActive,
                TrialUsed = user.TrialUsed,
                TrialStartDate = user.TrialStartDate,
                TrialEndDate = user.TrialEndDate,
                TrialDaysRemaining = user.TrialDaysRemaining,
                ProSubscriptionStartDate = user.ProSubscriptionStartDate,
                ProSubscriptionEndDate = user.ProSubscriptionEndDate,
                CreatedAt = user.CreatedAt,
                LastLogin = user.LastLogin,
                UsageTypeName = user.UsageType?.Name ?? "Personal",
                StorageUsedBytes = await _context.EventPhotos
                    .Where(p => p.PhotographerEvent.UserId == user.Id)
                    .SumAsync(p => (long?)p.SizeBytes) ?? 0,
                
                // Related data
                RecentAdminActions = recentActions
            };
        }

        public async Task<bool> ToggleUserStatusAsync(int adminUserId, int targetUserId, bool activate, string ipAddress)
        {
            var user = await _context.Users.FindAsync(targetUserId);
            if (user == null) return false;
            
            var oldStatus = user.IsActive;
            user.IsActive = activate;
            
            // Log admin action
            _context.AdminActions.Add(new AdminAction
            {
                AdminUserId = adminUserId,
                TargetUserId = targetUserId,
                ActionType = activate ? AdminActionType.UserActivated : AdminActionType.UserDeactivated,
                EntityType = "User",
                EntityId = targetUserId,
                Description = activate 
                    ? $"Usuario {user.Email} activado" 
                    : $"Usuario {user.Email} desactivado",
                OldValue = JsonSerializer.Serialize(new { IsActive = oldStatus }),
                NewValue = JsonSerializer.Serialize(new { IsActive = activate }),
                IpAddress = ipAddress
            });
            
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ChangeUserRoleAsync(int adminUserId, int targetUserId, UserRole newRole, string ipAddress)
        {
            var user = await _context.Users.FindAsync(targetUserId);
            if (user == null) return false;
            
            var oldRole = user.Role;
            user.Role = newRole;
            
            _context.AdminActions.Add(new AdminAction
            {
                AdminUserId = adminUserId,
                TargetUserId = targetUserId,
                ActionType = AdminActionType.RoleChanged,
                EntityType = "User",
                EntityId = targetUserId,
                Description = $"Rol de {user.Email} cambiado de {oldRole} a {newRole}",
                OldValue = JsonSerializer.Serialize(new { Role = oldRole.ToString() }),
                NewValue = JsonSerializer.Serialize(new { Role = newRole.ToString() }),
                IpAddress = ipAddress
            });
            
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ChangeUserPlanAsync(int adminUserId, int targetUserId, PlanType newPlan, string ipAddress)
        {
            var user = await _context.Users.FindAsync(targetUserId);
            if (user == null) return false;
            
            var oldPlan = user.PlanType;
            user.PlanType = newPlan;
            
            // Set subscription dates based on plan
            if (newPlan == PlanType.PRO)
            {
                user.ProSubscriptionStartDate = DateTime.UtcNow;
                user.ProSubscriptionEndDate = DateTime.UtcNow.AddYears(1); // 1 year manual subscription
                user.Plan = "PRO";
            }
            else if (newPlan == PlanType.FREE)
            {
                user.ProSubscriptionStartDate = null;
                user.ProSubscriptionEndDate = null;
                user.Plan = "FREE";
            }
            
            var actionType = (int)newPlan > (int)oldPlan 
                ? AdminActionType.PlanUpgraded 
                : AdminActionType.PlanDowngraded;
            
            _context.AdminActions.Add(new AdminAction
            {
                AdminUserId = adminUserId,
                TargetUserId = targetUserId,
                ActionType = actionType,
                EntityType = "User",
                EntityId = targetUserId,
                Description = $"Plan de {user.Email} cambiado de {oldPlan} a {newPlan}",
                OldValue = JsonSerializer.Serialize(new { PlanType = oldPlan.ToString() }),
                NewValue = JsonSerializer.Serialize(new { PlanType = newPlan.ToString() }),
                IpAddress = ipAddress
            });
            
            await _context.SaveChangesAsync();

            // Send email notification
            try
            {
                var userName = user.FullName ?? user.Email;
                var isUpgrade = (int)newPlan > (int)oldPlan;
                await _emailService.SendPlanChangeEmailAsync(
                    user.Email, userName, oldPlan.ToString(), newPlan.ToString(), isUpgrade);
                _logger.LogInformation("Email de cambio de plan enviado a {Email}", user.Email);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo enviar email de cambio de plan a {Email}", user.Email);
            }

            return true;
        }

        // ========================================
        // User Deletion
        // ========================================

        public async Task<(bool Success, string Message)> DeleteUserAsync(int adminUserId, int targetUserId, string ipAddress)
        {
            var user = await _context.Users.FindAsync(targetUserId);
            if (user == null) return (false, "Usuario no encontrado");

            if (user.Role == UserRole.SuperAdmin)
                return (false, "No se puede eliminar a un SuperAdmin");

            if (targetUserId == adminUserId)
                return (false, "No podés eliminarte a vos mismo");

            var userEmail = user.Email;
            var userName = user.FullName ?? user.Email;

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Delete refresh tokens
                var refreshTokens = await _context.RefreshTokens.Where(r => r.UserId == targetUserId).ToListAsync();
                if (refreshTokens.Any()) _context.RefreshTokens.RemoveRange(refreshTokens);

                // Update admin actions: set TargetUserId to null for actions targeting this user
                var targetActions = await _context.AdminActions.Where(a => a.TargetUserId == targetUserId).ToListAsync();
                foreach (var action in targetActions)
                    action.TargetUserId = null;

                // Delete admin actions performed BY this user (if they were admin)
                var adminActions = await _context.AdminActions.Where(a => a.AdminUserId == targetUserId).ToListAsync();
                if (adminActions.Any()) _context.AdminActions.RemoveRange(adminActions);

                // Delete the user
                _context.Users.Remove(user);

                // Log the deletion (as the admin performing it)
                _context.AdminActions.Add(new AdminAction
                {
                    AdminUserId = adminUserId,
                    TargetUserId = null, // User no longer exists
                    ActionType = AdminActionType.UserDeleted,
                    EntityType = "User",
                    EntityId = targetUserId,
                    Description = $"Usuario {userEmail} ({userName}) eliminado permanentemente",
                    OldValue = JsonSerializer.Serialize(new { user.Email, user.FullName, PlanType = user.PlanType.ToString(), Role = user.Role.ToString() }),
                    NewValue = null,
                    IpAddress = ipAddress
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogWarning("Usuario {Email} (ID: {UserId}) eliminado permanentemente por admin {AdminId}", userEmail, targetUserId, adminUserId);
                return (true, $"Usuario {userEmail} eliminado permanentemente");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error eliminando usuario {UserId}", targetUserId);
                return (false, "Error eliminando el usuario. Intente nuevamente.");
            }
        }

        // ========================================
        // Audit
        // ========================================
        
        public async Task<List<AdminActionDto>> GetRecentActionsAsync(int count = 20)
        {
            return await _context.AdminActions
                .Include(a => a.AdminUser)
                .Include(a => a.TargetUser)
                .OrderByDescending(a => a.CreatedAt)
                .Take(count)
                .Select(a => new AdminActionDto
                {
                    Id = a.Id,
                    AdminUserName = a.AdminUser.FullName ?? a.AdminUser.Email,
                    TargetUserName = a.TargetUser != null ? (a.TargetUser.FullName ?? a.TargetUser.Email) : null,
                    ActionType = a.ActionType.ToString(),
                    Description = a.Description,
                    OldValue = a.OldValue,
                    NewValue = a.NewValue,
                    CreatedAt = a.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<OwnerGlobalSalesSummaryDto> GetOwnerGlobalSalesSummaryAsync(CancellationToken cancellationToken = default)
        {
            var studios = await _context.Orders
                .AsNoTracking()
                .GroupBy(o => new
                {
                    o.PhotographerId,
                    Name = o.Photographer.FullName,
                    o.Photographer.Email
                })
                .Select(g => new OwnerStudioSalesDto
                {
                    PhotographerId = g.Key.PhotographerId,
                    StudioName = string.IsNullOrWhiteSpace(g.Key.Name) ? g.Key.Email : g.Key.Name!,
                    StudioEmail = g.Key.Email,
                    OrdersCount = g.Count(),
                    GrossTotal = g.Sum(x => x.TotalAmount),
                    PlatformCommissionTotal = g.Sum(x => x.PlatformCommission),
                    NetTotal = g.Sum(x => x.PhotographerNet),
                    LastSaleAt = g.Max(x => x.CreatedAt)
                })
                .OrderByDescending(s => s.GrossTotal)
                .ToListAsync(cancellationToken);

            return new OwnerGlobalSalesSummaryDto
            {
                GrossTotal = studios.Sum(s => s.GrossTotal),
                PlatformCommissionTotal = studios.Sum(s => s.PlatformCommissionTotal),
                NetTotal = studios.Sum(s => s.NetTotal),
                OrdersCount = studios.Sum(s => s.OrdersCount),
                Studios = studios
            };
        }

        public async Task<OwnerTransferApprovalListDto> GetPendingTransferApprovalsAsync(CancellationToken cancellationToken = default)
        {
            var items = await _context.PhotoCheckoutSessions
                .AsNoTracking()
                .Where(s => s.Status == "TransferReceiptSent" && s.ExternalReference.StartsWith("transfer:"))
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => new OwnerTransferApprovalItemDto
                {
                    ExternalReference = s.ExternalReference,
                    PhotographerId = s.PhotographerId,
                    StudioName = string.IsNullOrWhiteSpace(s.Photographer.FullName) ? s.Photographer.Email : s.Photographer.FullName!,
                    StudioEmail = s.Photographer.Email,
                    EventId = s.EventId,
                    EventName = s.Event.Name,
                    BuyerName = string.IsNullOrWhiteSpace(s.BuyerName) ? "Comprador" : s.BuyerName!,
                    BuyerEmail = s.BuyerEmail,
                    PhotoCount = ParsePhotoIdsCsv(s.PhotoIdsCsv).Distinct().Count(),
                    TotalAmount = s.TotalAmount,
                    SubmittedAt = s.CreatedAt,
                    Status = s.Status
                })
                .ToListAsync(cancellationToken);

            return new OwnerTransferApprovalListDto
            {
                TotalCount = items.Count,
                Items = items
            };
        }

        public async Task<(bool Success, string Message)> ApproveTransferSaleAsync(int adminUserId, string externalReference, int clearanceHours, string ipAddress, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(externalReference))
                return (false, "Referencia inválida");

            var normalizedReference = externalReference.Trim();
            var session = await _context.PhotoCheckoutSessions
                .FirstOrDefaultAsync(s => s.ExternalReference == normalizedReference, cancellationToken);

            if (session == null)
                return (false, "No se encontró la sesión de transferencia");

            if (string.Equals(session.Status, "Paid", StringComparison.OrdinalIgnoreCase))
                return (false, "La transferencia ya fue aprobada");

            if (!string.Equals(session.Status, "TransferReceiptSent", StringComparison.OrdinalIgnoreCase))
                return (false, "La transferencia aún no tiene comprobante enviado");

            var photoIds = ParsePhotoIdsCsv(session.PhotoIdsCsv)
                .Distinct()
                .ToList();

            if (photoIds.Count == 0)
                return (false, "No hay fotos válidas asociadas a la transferencia");

            var now = DateTime.UtcNow;
            var safeClearanceHours = Math.Clamp(clearanceHours, 0, 720);
            var clearedAt = now.AddHours(safeClearanceHours);
            var commissionPercent = Math.Clamp(_paymentSettings.CommissionPercent, 0m, 100m);
            var unitTotals = SplitAmount(session.TotalAmount, photoIds.Count);

            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                for (var index = 0; index < photoIds.Count; index++)
                {
                    var totalAmount = unitTotals[index];
                    var mercadoPagoFee = 0m;
                    var platformCommission = Math.Round(totalAmount * (commissionPercent / 100m), 2, MidpointRounding.AwayFromZero);
                    var photographerNet = Math.Max(0m, totalAmount - platformCommission - mercadoPagoFee);

                    _context.Orders.Add(new Order
                    {
                        PhotographerId = session.PhotographerId,
                        EventId = session.EventId,
                        PhotoId = photoIds[index],
                        TotalAmount = totalAmount,
                        PlatformCommission = platformCommission,
                        MercadoPagoFee = mercadoPagoFee,
                        PhotographerNet = photographerNet,
                        Status = PaidOrderStatus,
                        CreatedAt = now,
                        ClearedAt = clearedAt
                    });
                }

                var pendingSale = await _context.PhotoSales
                    .Where(s =>
                        s.UserId == session.PhotographerId &&
                        s.PhotographerEventId == session.EventId &&
                        s.PaymentMethod == "transfer" &&
                        s.Status == "pending_confirmation" &&
                        s.TotalAmount == session.TotalAmount &&
                        s.BuyerEmail == session.BuyerEmail)
                    .OrderByDescending(s => s.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);

                if (pendingSale != null)
                {
                    pendingSale.Status = "paid";
                    pendingSale.UpdatedAt = now;
                }

                var totalNet = unitTotals
                    .Select(total =>
                    {
                        var platformCommission = Math.Round(total * (commissionPercent / 100m), 2, MidpointRounding.AwayFromZero);
                        return Math.Max(0m, total - platformCommission);
                    })
                    .Sum();

                var balance = await _context.PhotographerBalances
                    .FirstOrDefaultAsync(b => b.PhotographerId == session.PhotographerId, cancellationToken);

                if (balance == null)
                {
                    balance = new PhotographerBalance
                    {
                        PhotographerId = session.PhotographerId,
                        PendingAmount = totalNet,
                        AvailableAmount = 0m,
                        TotalWithdrawn = 0m
                    };
                    _context.PhotographerBalances.Add(balance);
                }
                else
                {
                    balance.PendingAmount += totalNet;
                }

                session.Status = "Paid";
                session.PaidAt = now;

                _context.AdminActions.Add(new AdminAction
                {
                    AdminUserId = adminUserId,
                    TargetUserId = session.PhotographerId,
                    ActionType = AdminActionType.ManualPaymentCreated,
                    EntityType = "PhotoCheckoutSession",
                    Description = $"Transferencia aprobada manualmente: {session.ExternalReference}",
                    OldValue = "{\"status\":\"TransferReceiptSent\"}",
                    NewValue = "{\"status\":\"Paid\"}",
                    IpAddress = ipAddress
                });

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                if (!string.IsNullOrWhiteSpace(session.BuyerEmail))
                {
                    var buyerName = string.IsNullOrWhiteSpace(session.BuyerName) ? "Comprador" : session.BuyerName!;
                    try
                    {
                        await _emailService.SendPurchaseProcessingEmailAsync(session.BuyerEmail, buyerName, session.ExternalReference);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "No se pudo enviar email de confirmación de pago tras aprobar transferencia. ExternalReference={ExternalReference}",
                            session.ExternalReference);
                    }
                }

                var (deliverySuccess, deliveryMessage) = await _photoDeliveryService.SendPurchasedPhotosAsync(session.Id, cancellationToken);
                if (!deliverySuccess)
                {
                    _logger.LogWarning("Transferencia aprobada pero falló email de entrega. ExternalReference={ExternalReference}, Message={Message}", normalizedReference, deliveryMessage);
                    return (true, $"Transferencia aprobada correctamente. Email de entrega pendiente/fallido: {deliveryMessage}");
                }

                return (true, "Transferencia aprobada correctamente. Email de entrega enviado.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Error aprobando transferencia manual {ExternalReference}", normalizedReference);
                return (false, "No se pudo aprobar la transferencia");
            }
        }

        public async Task<OwnerPhotoDeliveryFailuresDto> GetFailedPhotoDeliveriesAsync(CancellationToken cancellationToken = default)
        {
            var items = await _context.PhotoCheckoutSessions
                .AsNoTracking()
                .Where(s =>
                    (s.Status == "Paid" || s.Status == "PaymentProcessingError") &&
                    (s.DeliveryEmailStatus == "Failed" || s.DeliveryEmailStatus == "NotSent"))
                .OrderByDescending(s => s.DeliveryEmailLastAttemptAt ?? s.PaidAt ?? s.CreatedAt)
                .Select(s => new OwnerPhotoDeliveryFailureItemDto
                {
                    ExternalReference = s.ExternalReference,
                    Status = s.Status,
                    PhotographerId = s.PhotographerId,
                    StudioName = string.IsNullOrWhiteSpace(s.Photographer.FullName) ? s.Photographer.Email : s.Photographer.FullName!,
                    StudioEmail = s.Photographer.Email,
                    EventId = s.EventId,
                    EventName = s.Event.Name,
                    BuyerName = string.IsNullOrWhiteSpace(s.BuyerName) ? "Comprador" : s.BuyerName!,
                    BuyerEmail = s.BuyerEmail,
                    PhotoCount = ParsePhotoIdsCsv(s.PhotoIdsCsv).Distinct().Count(),
                    DeliveryEmailStatus = s.DeliveryEmailStatus,
                    DeliveryEmailAttempts = s.DeliveryEmailAttempts,
                    DeliveryEmailLastAttemptAt = s.DeliveryEmailLastAttemptAt,
                    DeliveryEmailSentAt = s.DeliveryEmailSentAt,
                    DeliveryEmailError = s.DeliveryEmailError,
                    PaidAt = s.PaidAt,
                    CreatedAt = s.CreatedAt
                })
                .ToListAsync(cancellationToken);

            return new OwnerPhotoDeliveryFailuresDto
            {
                TotalCount = items.Count,
                Items = items
            };
        }

        public async Task<(bool Success, string Message)> RetryPhotoDeliveryAsync(string externalReference, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(externalReference))
                return (false, "Referencia inválida");

            var normalizedReference = externalReference.Trim();
            var session = await _context.PhotoCheckoutSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.ExternalReference == normalizedReference, cancellationToken);

            if (session == null)
                return (false, "No se encontró la compra para esa referencia");

            if (!string.Equals(session.Status, "Paid", StringComparison.OrdinalIgnoreCase))
                return (false, "La compra aún no está pagada");

            var (success, message) = await _photoDeliveryService.SendPurchasedPhotosAsync(session.Id, cancellationToken);
            return (success, message);
        }

        public async Task<OwnerAccreditationSummaryDto> GetOwnerAccreditationsAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;

            var rows = await _context.Orders
                .AsNoTracking()
                .GroupBy(o => new
                {
                    o.PhotographerId,
                    Name = o.Photographer.FullName,
                    o.Photographer.Email
                })
                .Select(g => new OwnerAccreditationPhotographerDto
                {
                    PhotographerId = g.Key.PhotographerId,
                    StudioName = string.IsNullOrWhiteSpace(g.Key.Name) ? g.Key.Email : g.Key.Name!,
                    StudioEmail = g.Key.Email,
                    WithdrawalHolderName = g.Select(x => x.Photographer.WithdrawalHolderName).FirstOrDefault(),
                    WithdrawalBankName = g.Select(x => x.Photographer.WithdrawalBankName).FirstOrDefault(),
                    WithdrawalAliasOrCbu = g.Select(x => x.Photographer.WithdrawalAliasOrCbu).FirstOrDefault(),
                    PendingAmount = g.Where(x => x.Status != "PaidOut" && x.ClearedAt > now).Sum(x => x.PhotographerNet),
                    AvailableAmount = g.Where(x => x.Status != "PaidOut" && x.ClearedAt <= now).Sum(x => x.PhotographerNet),
                    TotalWithdrawn = g.Where(x => x.Status == "PaidOut").Sum(x => x.PhotographerNet),
                    ToAccreditNow = g.Where(x => x.Status != "PaidOut" && x.ClearedAt <= now).Sum(x => x.PhotographerNet)
                })
                .OrderByDescending(x => x.ToAccreditNow)
                .ToListAsync(cancellationToken);

            return new OwnerAccreditationSummaryDto
            {
                TotalPending = rows.Sum(x => x.PendingAmount),
                TotalAvailable = rows.Sum(x => x.AvailableAmount),
                TotalWithdrawn = rows.Sum(x => x.TotalWithdrawn),
                TotalToAccreditNow = rows.Sum(x => x.ToAccreditNow),
                PhotographersCount = rows.Count,
                Photographers = rows
            };
        }

        public async Task<(bool Success, string Message)> MarkPhotographerAccreditationPaidOutAsync(int adminUserId, int photographerId, string? note, string ipAddress, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var availableOrders = await _context.Orders
                .Where(o => o.PhotographerId == photographerId && o.Status != "PaidOut" && o.ClearedAt <= now)
                .ToListAsync(cancellationToken);

            if (availableOrders.Count == 0)
                return (false, "No hay monto disponible para acreditar en este fotógrafo");

            var amount = availableOrders.Sum(o => o.PhotographerNet);
            var fromDate = availableOrders.Min(o => o.CreatedAt);

            using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                foreach (var order in availableOrders)
                {
                    order.Status = "PaidOut";
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
                        PendingAmount = g.Where(o => o.Status != "PaidOut" && o.ClearedAt > now).Sum(o => (decimal?)o.PhotographerNet) ?? 0m,
                        AvailableAmount = g.Where(o => o.Status != "PaidOut" && o.ClearedAt <= now).Sum(o => (decimal?)o.PhotographerNet) ?? 0m,
                        TotalWithdrawn = g.Where(o => o.Status == "PaidOut").Sum(o => (decimal?)o.PhotographerNet) ?? 0m
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

                _context.AdminActions.Add(new AdminAction
                {
                    AdminUserId = adminUserId,
                    TargetUserId = photographerId,
                    ActionType = AdminActionType.ManualPaymentCreated,
                    EntityType = "Liquidation",
                    Description = string.IsNullOrWhiteSpace(note)
                        ? $"Acreditación marcada como pagada. Monto: {amount:0.00}"
                        : $"Acreditación marcada como pagada. Monto: {amount:0.00}. Nota: {note}",
                    NewValue = JsonSerializer.Serialize(new { Amount = amount, PaidOutAt = now }),
                    IpAddress = ipAddress
                });

                await _context.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
                return (true, "Acreditación registrada correctamente");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Error marcando acreditación pagada para fotógrafo {PhotographerId}", photographerId);
                return (false, "No se pudo registrar la acreditación");
            }
        }

        private static List<int> ParsePhotoIdsCsv(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv))
                return new List<int>();

            return csv
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => int.TryParse(value, out var idValue) ? idValue : 0)
                .Where(idValue => idValue > 0)
                .ToList();
        }

        private static List<decimal> SplitAmount(decimal total, int parts)
        {
            if (parts <= 0)
                return new List<decimal>();

            var results = new List<decimal>(parts);
            var running = 0m;

            for (var i = 0; i < parts; i++)
            {
                var value = Math.Round(total / parts, 2, MidpointRounding.AwayFromZero);
                if (i == parts - 1)
                {
                    value = Math.Round(total - running, 2, MidpointRounding.AwayFromZero);
                }

                running += value;
                results.Add(value);
            }

            return results;
        }
    }
}
