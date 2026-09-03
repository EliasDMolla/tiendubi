using Admin.Entities.Entities;
using Microsoft.EntityFrameworkCore;

namespace Admin.Entities
{
    public class Context : DbContext
    {
        public Context(DbContextOptions<Context> options) : base(options) { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
                  if (!optionsBuilder.IsConfigured)
                  {
                        optionsBuilder.UseNpgsql(npgsqlOptions => npgsqlOptions.CommandTimeout(600));
                  }

            base.OnConfiguring(optionsBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // ========================================
            // User Configuration
            // ========================================
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(u => u.Id);

                entity.Property(u => u.Email)
                      .IsRequired()
                      .HasMaxLength(200);

                entity.HasIndex(u => u.Email)
                      .IsUnique();

                entity.HasIndex(u => u.PublicSlug)
                      .IsUnique();

                entity.Property(u => u.PasswordHash)
                      .IsRequired()
                      .HasMaxLength(500);

                entity.Property(u => u.PhoneNumber)
                      .HasMaxLength(50);

                entity.Property(u => u.FullName)
                      .HasMaxLength(200);

                entity.Property(u => u.PublicSlug)
                      .HasMaxLength(80);

                entity.Property(u => u.WithdrawalHolderName)
                      .HasMaxLength(200);

                entity.Property(u => u.WithdrawalBankName)
                      .HasMaxLength(200);

                entity.Property(u => u.WithdrawalAliasOrCbu)
                      .HasMaxLength(120);

                entity.Property(u => u.IsActive)
                      .HasDefaultValue(true)
                      .IsRequired();

                // Sistema de Roles
                entity.Property(u => u.Role)
                      .HasDefaultValue(Entities.UserRole.User)
                      .IsRequired();

                entity.Property(u => u.EmailVerified)
                      .HasDefaultValue(false)
                      .IsRequired();

                entity.Property(u => u.PhoneVerified)
                      .HasDefaultValue(false)
                      .IsRequired();

                // Sistema de planes
                entity.Property(u => u.PlanType)
                      .HasDefaultValue(Entities.PlanType.FREE)
                      .IsRequired();

                entity.Property(u => u.TrialUsed)
                      .HasDefaultValue(false)
                      .IsRequired();

                entity.Property(u => u.TrialStartDate)
                      .IsRequired(false);

                entity.Property(u => u.TrialEndDate)
                      .IsRequired(false);

                entity.Property(u => u.ProSubscriptionStartDate)
                      .IsRequired(false);

                entity.Property(u => u.ProSubscriptionEndDate)
                      .IsRequired(false);

                // Ignorar propiedades calculadas
                entity.Ignore(u => u.IsProActive);
                entity.Ignore(u => u.TrialDaysRemaining);

                // Campos legacy (mantener por compatibilidad)
                entity.Property(u => u.Plan)
                      .HasMaxLength(20)
                      .HasDefaultValue("FREE")
                      .IsRequired();

                entity.Property(u => u.SubscriptionStatus)
                      .HasMaxLength(20)
                      .HasDefaultValue("ACTIVO")
                      .IsRequired();

                entity.Property(u => u.GoogleId)
                      .HasMaxLength(200);

                entity.Property(u => u.FacebookId)
                      .HasMaxLength(200);

                entity.Property(u => u.CreatedAt)
                      .HasDefaultValueSql("NOW()")
                      .IsRequired();

                entity.Property(u => u.UpdatedAt)
                      .IsRequired(false);
                
                // Relación con UsageType
                entity.HasOne(u => u.UsageType)
                      .WithMany(ut => ut.Users)
                      .HasForeignKey(u => u.UsageTypeId)
                      .OnDelete(DeleteBehavior.Restrict);
                
                entity.Property(u => u.UsageTypeId)
                      .HasDefaultValue(1) // Por defecto "Personal"
                      .IsRequired();
            });

            // ========================================
            // RefreshToken Configuration
            // ========================================
            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.HasKey(rt => rt.Id);

                entity.Property(rt => rt.Token)
                      .IsRequired()
                      .HasMaxLength(500);

                entity.Property(rt => rt.ExpiresAt)
                      .IsRequired();

                entity.Property(rt => rt.CreatedAt)
                      .HasDefaultValueSql("NOW()")
                      .IsRequired();

                entity.Property(rt => rt.CreatedByIp)
                      .HasMaxLength(50);

                entity.Property(rt => rt.RevokedByIp)
                      .HasMaxLength(50);

                entity.Property(rt => rt.ReplacedByToken)
                      .HasMaxLength(500);

                entity.HasOne(rt => rt.User)
                      .WithMany(u => u.RefreshTokens)
                      .HasForeignKey(rt => rt.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ========================================
            // UsageType Configuration
            // ========================================
            modelBuilder.Entity<UsageType>(entity =>
            {
                entity.HasKey(ut => ut.Id);
                
                entity.Property(ut => ut.Name)
                      .IsRequired()
                      .HasMaxLength(50);
                
                entity.Property(ut => ut.Description)
                      .HasMaxLength(200);
                
                entity.Property(ut => ut.IsDefault)
                      .HasDefaultValue(false);
                
                entity.Property(ut => ut.CreatedAt)
                      .HasDefaultValueSql("NOW()")
                      .IsRequired();
                
                entity.HasIndex(ut => ut.Name)
                      .IsUnique();
            });

            // ========================================
            // AdminAction Configuration
            // ========================================
            modelBuilder.Entity<AdminAction>(entity =>
            {
                entity.HasKey(a => a.Id);
                
                entity.Property(a => a.ActionType)
                      .IsRequired();
                
                entity.Property(a => a.EntityType)
                      .IsRequired()
                      .HasMaxLength(50);
                
                entity.Property(a => a.Description)
                      .IsRequired()
                      .HasMaxLength(500);
                
                entity.Property(a => a.OldValue)
                      .HasMaxLength(2000);
                
                entity.Property(a => a.NewValue)
                      .HasMaxLength(2000);
                
                entity.Property(a => a.IpAddress)
                      .HasMaxLength(50);
                
                entity.Property(a => a.CreatedAt)
                      .HasDefaultValueSql("NOW()")
                      .IsRequired();
                
                entity.HasOne(a => a.AdminUser)
                      .WithMany(u => u.AdminActionsPerformed)
                      .HasForeignKey(a => a.AdminUserId)
                      .OnDelete(DeleteBehavior.Restrict);
                
                entity.HasOne(a => a.TargetUser)
                      .WithMany(u => u.AdminActionsReceived)
                      .HasForeignKey(a => a.TargetUserId)
                      .OnDelete(DeleteBehavior.Restrict);
                
                entity.HasIndex(a => a.AdminUserId);
                entity.HasIndex(a => a.TargetUserId);
                entity.HasIndex(a => a.CreatedAt);
                entity.HasIndex(a => a.ActionType);
            });

            // ========================================
            // PhotographerEvent Configuration
            // ========================================
            modelBuilder.Entity<PhotographerEvent>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Name)
                      .IsRequired()
                      .HasMaxLength(200);

                entity.Property(e => e.Description)
                      .HasMaxLength(1000);

                entity.Property(e => e.EventDate)
                      .IsRequired();

                entity.Property(e => e.PricePerPhoto)
                      .HasColumnType("numeric(18,2)")
                      .HasDefaultValue(0m)
                      .IsRequired();

                entity.Property(e => e.OriginalPrice)
                      .HasColumnType("numeric(18,2)")
                      .IsRequired(false);

                entity.Property(e => e.PriceType)
                      .HasMaxLength(20)
                      .HasDefaultValue("paid")
                      .IsRequired();

                entity.Property(e => e.ProductType)
                      .HasMaxLength(30)
                      .HasDefaultValue("digital_file")
                      .IsRequired();

                entity.Property(e => e.PaymentMethods)
                      .HasMaxLength(100)
                      .HasDefaultValue("mercadopago")
                      .IsRequired();

                entity.Property(e => e.BuyerInstructions)
                      .HasMaxLength(3000);

                entity.Property(e => e.DeliveryLink)
                      .HasMaxLength(1000);

                entity.Property(e => e.CoverImagePath)
                      .HasMaxLength(500);

                entity.Property(e => e.IsPublished)
                      .HasDefaultValue(false)
                      .IsRequired();

                entity.Property(e => e.CreatedAt)
                      .HasDefaultValueSql("NOW()")
                      .IsRequired();

                entity.Property(e => e.UpdatedAt)
                      .IsRequired(false);

                entity.HasOne(e => e.User)
                      .WithMany(u => u.PhotographerEvents)
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.EventDate);
                entity.HasIndex(e => new { e.UserId, e.Name });
            });

            // ========================================
            // ProductAsset Configuration
            // ========================================
            modelBuilder.Entity<ProductAsset>(entity =>
            {
                entity.ToTable("ProductAssets");
                entity.HasKey(a => a.Id);

                entity.Property(a => a.Kind)
                      .IsRequired()
                      .HasMaxLength(30);

                entity.Property(a => a.OriginalFileName)
                      .IsRequired()
                      .HasMaxLength(260);

                entity.Property(a => a.ObjectKey)
                      .IsRequired()
                      .HasMaxLength(500);

                entity.Property(a => a.ContentType)
                      .IsRequired()
                      .HasMaxLength(120);

                entity.Property(a => a.SizeBytes)
                      .IsRequired();

                entity.Property(a => a.CreatedAt)
                      .HasDefaultValueSql("NOW()")
                      .IsRequired();

                entity.Property(a => a.UpdatedAt)
                      .IsRequired(false);

                entity.HasOne(a => a.PhotographerEvent)
                      .WithMany(e => e.ProductAssets)
                      .HasForeignKey(a => a.PhotographerEventId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(a => a.PhotographerEventId);
            });

            // ========================================
            // EventPhoto Configuration
            // ========================================
            modelBuilder.Entity<EventPhoto>(entity =>
            {
                entity.HasKey(p => p.Id);

                entity.Property(p => p.OriginalFileName)
                      .IsRequired()
                      .HasMaxLength(260);

                entity.Property(p => p.StoredFileName)
                      .IsRequired()
                      .HasMaxLength(260);

                entity.Property(p => p.RelativePath)
                      .IsRequired()
                      .HasMaxLength(500);

                entity.Property(p => p.OriginalPath)
                      .IsRequired()
                      .HasMaxLength(500);

                entity.Property(p => p.ThumbnailPath)
                      .HasMaxLength(500);

                entity.Property(p => p.WatermarkedPath)
                      .HasMaxLength(500);

                entity.Property(p => p.Tags)
                      .HasMaxLength(500);

                entity.Property(p => p.IsProcessed)
                      .HasDefaultValue(false)
                      .IsRequired();

                entity.Property(p => p.ProcessingFailed)
                      .HasDefaultValue(false)
                      .IsRequired();

                entity.Property(p => p.ProcessingError)
                      .HasMaxLength(500);

                entity.Property(p => p.SizeBytes)
                      .IsRequired();

                entity.Property(p => p.WatermarkApplied)
                      .HasDefaultValue(false)
                      .IsRequired();

                entity.Property(p => p.CreatedAt)
                      .HasDefaultValueSql("NOW()")
                      .IsRequired();

                entity.Property(p => p.UpdatedAt)
                      .IsRequired(false);

                entity.HasOne(p => p.PhotographerEvent)
                      .WithMany(e => e.Photos)
                      .HasForeignKey(p => p.PhotographerEventId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(p => p.PhotographerEventId);
            });

            // ========================================
            // PhotoSale Configuration
            // ========================================
            modelBuilder.Entity<PhotoSale>(entity =>
            {
                entity.HasKey(s => s.Id);

                entity.Property(s => s.Quantity)
                      .IsRequired();

                entity.Property(s => s.TotalAmount)
                      .HasColumnType("numeric(18,2)")
                      .IsRequired();

                entity.Property(s => s.BuyerName)
                      .IsRequired()
                      .HasMaxLength(150);

                entity.Property(s => s.BuyerEmail)
                      .HasMaxLength(200);

                entity.Property(s => s.PaymentMethod)
                      .IsRequired()
                      .HasMaxLength(40)
                      .HasDefaultValue("manual");

                entity.Property(s => s.Status)
                      .IsRequired()
                      .HasMaxLength(30)
                      .HasDefaultValue("paid");

                entity.Property(s => s.SoldAt)
                      .IsRequired();

                entity.Property(s => s.CreatedAt)
                      .HasDefaultValueSql("NOW()")
                      .IsRequired();

                entity.Property(s => s.UpdatedAt)
                      .IsRequired(false);

                entity.HasOne(s => s.User)
                      .WithMany(u => u.PhotoSales)
                      .HasForeignKey(s => s.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(s => s.PhotographerEvent)
                      .WithMany(e => e.Sales)
                      .HasForeignKey(s => s.PhotographerEventId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(s => s.UserId);
                entity.HasIndex(s => s.PhotographerEventId);
                entity.HasIndex(s => s.SoldAt);
            });

            // ========================================
            // Order Configuration
            // ========================================
            modelBuilder.Entity<Order>(entity =>
            {
                entity.ToTable("Orders");
                entity.HasKey(o => o.Id);

                        entity.Property(o => o.PhotoId)
                                .IsRequired(false);

                entity.Property(o => o.TotalAmount)
                      .HasColumnType("numeric(18,2)")
                      .IsRequired();

                entity.Property(o => o.PlatformCommission)
                      .HasColumnType("numeric(18,2)")
                      .IsRequired();

                entity.Property(o => o.MercadoPagoFee)
                      .HasColumnType("numeric(18,2)")
                      .IsRequired();

                entity.Property(o => o.PhotographerNet)
                      .HasColumnType("numeric(18,2)")
                      .IsRequired();

                entity.Property(o => o.Status)
                      .HasMaxLength(20)
                      .IsRequired();

                entity.Property(o => o.CreatedAt)
                      .HasDefaultValueSql("NOW()")
                      .IsRequired();

                entity.Property(o => o.ClearedAt)
                      .IsRequired();

                entity.Property(o => o.PaidOutAt)
                      .IsRequired(false);

                entity.Property(o => o.UpdatedAt)
                      .IsRequired(false);

                entity.HasOne(o => o.Photographer)
                      .WithMany(u => u.Orders)
                      .HasForeignKey(o => o.PhotographerId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(o => o.Event)
                      .WithMany(e => e.Orders)
                      .HasForeignKey(o => o.EventId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(o => o.Photo)
                      .WithMany(p => p.Orders)
                      .HasForeignKey(o => o.PhotoId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(o => new { o.PhotographerId, o.CreatedAt });
                entity.HasIndex(o => new { o.PhotographerId, o.ClearedAt });
                entity.HasIndex(o => o.Status);
                entity.HasIndex(o => new { o.PhotographerId, o.EventId });
            });

            // ========================================
            // PhotographerBalance Configuration
            // ========================================
            modelBuilder.Entity<PhotographerBalance>(entity =>
            {
                entity.ToTable("PhotographerBalance");
                entity.HasKey(b => b.PhotographerId);

                entity.Property(b => b.PendingAmount)
                      .HasColumnType("numeric(18,2)")
                      .HasDefaultValue(0m)
                      .IsRequired();

                entity.Property(b => b.AvailableAmount)
                      .HasColumnType("numeric(18,2)")
                      .HasDefaultValue(0m)
                      .IsRequired();

                entity.Property(b => b.TotalWithdrawn)
                      .HasColumnType("numeric(18,2)")
                      .HasDefaultValue(0m)
                      .IsRequired();

                entity.HasOne(b => b.Photographer)
                      .WithOne(u => u.PhotographerBalance)
                      .HasForeignKey<PhotographerBalance>(b => b.PhotographerId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(b => b.PhotographerId)
                      .IsUnique();
            });

            // ========================================
            // Liquidation Configuration
            // ========================================
            modelBuilder.Entity<Liquidation>(entity =>
            {
                entity.ToTable("Liquidations");
                entity.HasKey(l => l.Id);

                entity.Property(l => l.Amount)
                      .HasColumnType("numeric(18,2)")
                      .IsRequired();

                entity.Property(l => l.FromDate)
                      .IsRequired();

                entity.Property(l => l.ToDate)
                      .IsRequired();

                entity.Property(l => l.CreatedAt)
                      .HasDefaultValueSql("NOW()")
                      .IsRequired();

                entity.HasOne(l => l.Photographer)
                      .WithMany(u => u.Liquidations)
                      .HasForeignKey(l => l.PhotographerId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(l => l.PhotographerId);
            });

            // ========================================
            // PhotoCheckoutSession Configuration
            // ========================================
            modelBuilder.Entity<PhotoCheckoutSession>(entity =>
            {
                entity.ToTable("PhotoCheckoutSessions");
                entity.HasKey(s => s.Id);

                entity.Property(s => s.ExternalReference)
                      .IsRequired()
                      .HasMaxLength(120);

                entity.Property(s => s.PhotoIdsCsv)
                      .IsRequired()
                      .HasMaxLength(4000);

                entity.Property(s => s.BuyerEmail)
                      .IsRequired()
                      .HasMaxLength(200);

                entity.Property(s => s.BuyerName)
                      .HasMaxLength(150);

                entity.Property(s => s.DiscountCode)
                      .HasMaxLength(80);

                entity.Property(s => s.SubtotalAmount)
                      .HasColumnType("numeric(18,2)")
                      .IsRequired();

                entity.Property(s => s.DiscountAmount)
                      .HasColumnType("numeric(18,2)")
                      .IsRequired();

                entity.Property(s => s.TotalAmount)
                      .HasColumnType("numeric(18,2)")
                      .IsRequired();

                entity.Property(s => s.PreferenceId)
                      .HasMaxLength(120);

                entity.Property(s => s.Status)
                      .IsRequired()
                      .HasMaxLength(30)
                      .HasDefaultValue("Created");

                entity.Property(s => s.DeliveryEmailStatus)
                      .IsRequired()
                      .HasMaxLength(30)
                      .HasDefaultValue("NotSent");

                entity.Property(s => s.DeliveryEmailError)
                      .HasMaxLength(1000);

                entity.Property(s => s.DeliveryEmailAttempts)
                      .HasDefaultValue(0)
                      .IsRequired();

                entity.Property(s => s.CreatedAt)
                      .HasDefaultValueSql("NOW()")
                      .IsRequired();

                entity.HasOne(s => s.Photographer)
                      .WithMany(u => u.PhotoCheckoutSessions)
                      .HasForeignKey(s => s.PhotographerId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(s => s.Event)
                      .WithMany(e => e.PhotoCheckoutSessions)
                      .HasForeignKey(s => s.EventId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(s => s.ExternalReference)
                      .IsUnique();
                entity.HasIndex(s => s.PreferenceId);
                entity.HasIndex(s => new { s.PhotographerId, s.CreatedAt });
            });

            // ========================================
            // PhotographerMercadoPagoAccount Configuration
            // ========================================
            modelBuilder.Entity<PhotographerMercadoPagoAccount>(entity =>
            {
                entity.ToTable("PhotographerMercadoPagoAccounts");
                entity.HasKey(a => a.Id);

                entity.Property(a => a.AccessToken)
                      .IsRequired()
                      .HasMaxLength(4000);

                entity.Property(a => a.RefreshToken)
                      .IsRequired()
                      .HasMaxLength(4000);

                entity.Property(a => a.PublicKey)
                      .HasMaxLength(200);

                entity.Property(a => a.MercadoPagoUserId)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(a => a.TokenExpiration)
                      .IsRequired();

                entity.Property(a => a.IsActive)
                      .HasDefaultValue(true)
                      .IsRequired();

                entity.Property(a => a.CreatedAt)
                      .HasDefaultValueSql("NOW()")
                      .IsRequired();

                entity.Property(a => a.UpdatedAt)
                      .IsRequired(false);

                entity.HasOne(a => a.Photographer)
                      .WithOne(u => u.PhotographerMercadoPagoAccount)
                      .HasForeignKey<PhotographerMercadoPagoAccount>(a => a.PhotographerId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(a => a.PhotographerId)
                      .IsUnique();

                entity.HasIndex(a => a.MercadoPagoUserId);
            });

            // ========================================
            // Seed Data
            // ========================================
            
            // Seed UsageTypes
            modelBuilder.Entity<UsageType>().HasData(
                new UsageType
                {
                    Id = 1,
                    Name = "Personal",
                    Description = "Uso personal",
                    IsDefault = true,
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new UsageType
                {
                    Id = 2,
                    Name = "Empresarial",
                    Description = "Uso empresarial",
                    IsDefault = false,
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new UsageType
                {
                    Id = 3,
                    Name = "Inmobiliario",
                    Description = "Gestión inmobiliaria",
                    IsDefault = false,
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            );

            // Seed Admin User
            // Usuario: admin
            // Password: admin (hashed con BCrypt)
            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = 1,
                    Email = "admin",
                    PasswordHash = "$2a$11$7XjvvgZqKwH5xJiGLJFLseMZ7YMNhJCqh0Sxkx6KHQxH8xP9xGgGa", // admin
                    FullName = "Administrador",
                    PhoneNumber = "",
                    IsActive = true,
                    Role = Entities.UserRole.SuperAdmin,
                    EmailVerified = true,
                    PhoneVerified = false,
                    PlanType = Entities.PlanType.PRO,
                    TrialUsed = false,
#pragma warning disable CS0618
                    Plan = "PRO",
                    SubscriptionStatus = "ACTIVO",
#pragma warning restore CS0618
                    UsageTypeId = 1,
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            );

            base.OnModelCreating(modelBuilder);
        }

        public DbSet<User> Users { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<UsageType> UsageTypes { get; set; }
        public DbSet<AdminAction> AdminActions { get; set; }
            public DbSet<PhotographerEvent> PhotographerEvents { get; set; }
            public DbSet<EventPhoto> EventPhotos { get; set; }
            public DbSet<ProductAsset> ProductAssets { get; set; }
            public DbSet<PhotoSale> PhotoSales { get; set; }
                  public DbSet<Order> Orders { get; set; }
                  public DbSet<PhotographerBalance> PhotographerBalances { get; set; }
                  public DbSet<Liquidation> Liquidations { get; set; }
                  public DbSet<PhotoCheckoutSession> PhotoCheckoutSessions { get; set; }
                          public DbSet<PhotographerMercadoPagoAccount> PhotographerMercadoPagoAccounts { get; set; }
    }
}
