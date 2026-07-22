using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using POS.Application.Interfaces;
using POS.Domain.Common;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence;

public sealed class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    /// <summary>
    /// Valor usado en el global query filter para <see cref="ITenantEntity"/>.
    /// Debe re-sincronizarse con <see cref="ICurrentUserService.TenantId"/> (claims u override) antes de operaciones.
    /// </summary>
    private string _currentTenantId;

    private readonly ICurrentUserService _currentUser;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentUserService currentUser)
        : base(options)
    {
        _currentUser = currentUser;
        _currentTenantId = currentUser.TenantId?.Trim() ?? string.Empty;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<PlatformAuditEvent> PlatformAuditEvents => Set<PlatformAuditEvent>();

    public DbSet<TenantEntitlement> TenantEntitlements => Set<TenantEntitlement>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Sale> Sales => Set<Sale>();

    public DbSet<SaleDetail> SaleDetails => Set<SaleDetail>();

    public DbSet<SalePayment> SalePayments => Set<SalePayment>();

    public DbSet<FiscalDocument> FiscalDocuments => Set<FiscalDocument>();

    public DbSet<TenantFiscalProfile> TenantFiscalProfiles => Set<TenantFiscalProfile>();

    public DbSet<Provider> Providers => Set<Provider>();

    public DbSet<Purchase> Purchases => Set<Purchase>();

    public DbSet<PurchaseDetail> PurchaseDetails => Set<PurchaseDetail>();

    public DbSet<CashSession> CashSessions => Set<CashSession>();

    public DbSet<Expense> Expenses => Set<Expense>();

    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();

    public DbSet<StockLot> StockLots => Set<StockLot>();

    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigureApplicationUser(modelBuilder);
        ConfigureTenant(modelBuilder);
        ConfigurePlatformAuditEvent(modelBuilder);
        ConfigureTenantEntitlement(modelBuilder);
        ConfigureProduct(modelBuilder);
        modelBuilder.Entity<Product>().HasQueryFilter(p => p.TenantId == _currentTenantId);
        ConfigureSale(modelBuilder);
        ConfigureFiscal(modelBuilder);
        ConfigureProviderAndPurchase(modelBuilder);
        ConfigureCashAndExpenses(modelBuilder);
        ConfigureInventory(modelBuilder);
        ConfigureCustomer(modelBuilder);
        ConfigureTenantQueryFilters(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        SyncEfQueryTenant();
        ApplyTenantAndCreatedAudit();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        SyncEfQueryTenant();
        ApplyTenantAndCreatedAudit();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    private void SyncEfQueryTenant()
    {
        _currentTenantId = _currentUser.TenantId?.Trim() ?? string.Empty;
    }

    private void ApplyTenantAndCreatedAudit()
    {
        var tenantId = _currentUser.TenantId?.Trim() ?? string.Empty;

        foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
        {
            if (entry.State != EntityState.Added || !string.IsNullOrWhiteSpace(entry.Entity.TenantId))
                continue;

            if (string.IsNullOrEmpty(tenantId))
                throw new InvalidOperationException(
                    "No se puede guardar: hay entidades de negocio sin TenantId y ICurrentUserService.TenantId está vacío. Configure el tenant antes de SaveChanges.");

            entry.Entity.TenantId = tenantId;
        }

        var utcNow = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Added)
                continue;

            var createdProp = entry.Metadata.FindProperty("CreatedAt");
            if (createdProp is null || !createdProp.ClrType.Equals(typeof(DateTime)))
                continue;

            entry.Property("CreatedAt").CurrentValue = utcNow;
        }
    }

    private static void ConfigureApplicationUser(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(u => u.TenantId).IsRequired().HasMaxLength(128);
            entity.Property(u => u.FullName).HasMaxLength(512);
            entity.Property(u => u.BusinessType).IsRequired().HasMaxLength(64);
            entity.Property(u => u.AccountKind).IsRequired();
            entity.Property(u => u.BlockedByPlatform).IsRequired();
            entity.Property(u => u.BlockedByTenant).IsRequired();
        });
    }

    private static void ConfigureProduct(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("Products");
            entity.HasKey(e => e.Id);
            entity.Ignore(e => e.FinalPrice);

            entity.Property(e => e.TenantId).HasMaxLength(128);
            entity.Property(e => e.Name).HasMaxLength(512);
            entity.Property(e => e.SKU).HasMaxLength(128);
            entity.Property(e => e.Barcode).HasMaxLength(128);
            entity.Property(e => e.ExtendedDataJson).HasColumnType("nvarchar(max)");
            entity.HasIndex(e => new { e.TenantId, e.SKU }).IsUnique();
            entity.Property(e => e.NetPrice).HasPrecision(18, 4);
            entity.Property(e => e.TaxRate).HasPrecision(18, 4);
            entity.Property(e => e.LastCost).HasPrecision(18, 4);
            entity.Property(e => e.Stock).HasPrecision(18, 3);
        });
    }

    private static void ConfigureSale(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Sale>(entity =>
        {
            entity.ToTable("Sales");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.TotalNet).HasPrecision(18, 2);
            entity.Property(e => e.TotalTax).HasPrecision(18, 2);
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
            // Reportes por tenant; rango de fechas frecuente: índice compuesto + columnas puntuales.
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.Date);
            entity.HasIndex(e => new { e.TenantId, e.Date });
            entity.Property(e => e.CreatedByUserId).HasMaxLength(128);
            entity.Property(e => e.CreatedByUserName).HasMaxLength(512);
            entity
                .HasOne(e => e.CashSession)
                .WithMany(s => s.Sales)
                .HasForeignKey(e => e.CashSessionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SaleDetail>(entity =>
        {
            entity.ToTable("SaleDetails");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.ProductName).IsRequired().HasMaxLength(512);
            entity.Property(e => e.ProductExtendedDataJson).HasColumnType("nvarchar(max)");
            entity.Property(e => e.LineNetSubtotal).HasPrecision(18, 2);
            entity.Property(e => e.LineTaxAmount).HasPrecision(18, 2);
            entity.Property(e => e.UnitNetPrice).HasPrecision(18, 2);
            entity.Property(e => e.TaxRate).HasPrecision(18, 4);
            entity.Property(e => e.Quantity).HasPrecision(18, 3);

            entity
                .HasOne(e => e.Sale)
                .WithMany(s => s.Details)
                .HasForeignKey(e => e.SaleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(e => e.Product)
                .WithMany()
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SalePayment>(entity =>
        {
            entity.ToTable("SalePayments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Method).HasConversion<int>();
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.HasIndex(e => new { e.TenantId, e.SaleId });
            entity
                .HasOne(e => e.Sale)
                .WithMany(s => s.Payments)
                .HasForeignKey(e => e.SaleId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureFiscal(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TenantFiscalProfile>(entity =>
        {
            entity.ToTable("TenantFiscalProfiles");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.TaxId).IsRequired().HasMaxLength(32);
            entity.Property(e => e.CertificateRef).HasMaxLength(512);
            entity.Property(e => e.PrivateKeyRef).HasMaxLength(512);
            entity.HasIndex(e => e.TenantId).IsUnique();
        });

        modelBuilder.Entity<FiscalDocument>(entity =>
        {
            entity.ToTable("FiscalDocuments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.DocumentType).HasConversion<int>();
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.Cae).HasMaxLength(32);
            entity.Property(e => e.LastErrorCode).HasMaxLength(128);
            entity.Property(e => e.LastErrorMessage).HasMaxLength(1500);
            entity.Property(e => e.CorrelationId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.BuyerTaxId).HasMaxLength(32);
            entity.Property(e => e.BuyerName).HasMaxLength(256);
            entity.Property(e => e.AuthorizedAmount).HasPrecision(18, 2);
            entity.HasIndex(e => new { e.TenantId, e.SaleId, e.DocumentType }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.PointOfSale, e.DocumentType, e.VoucherNumber }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.Status, e.NextRetryAtUtc });

            entity
                .HasOne(e => e.Sale)
                .WithMany(s => s.FiscalDocuments)
                .HasForeignKey(e => e.SaleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity
                .HasOne(e => e.OriginalFiscalDocument)
                .WithMany(e => e.CreditNotes)
                .HasForeignKey(e => e.OriginalFiscalDocumentId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureProviderAndPurchase(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Provider>(entity =>
        {
            entity.ToTable("Providers");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(512);
            entity.Property(e => e.TaxId).IsRequired().HasMaxLength(32);
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.Phone).HasMaxLength(64);
            entity.HasIndex(e => e.TenantId);
        });

        modelBuilder.Entity<Purchase>(entity =>
        {
            entity.ToTable("Purchases");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.InvoiceNumber).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Total).HasPrecision(18, 2);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.Date);
            entity
                .HasOne(e => e.Provider)
                .WithMany(p => p.Purchases)
                .HasForeignKey(e => e.ProviderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity
                .HasOne(e => e.CashSession)
                .WithMany(s => s.Purchases)
                .HasForeignKey(e => e.CashSessionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PurchaseDetail>(entity =>
        {
            entity.ToTable("PurchaseDetails");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.ProductName).IsRequired().HasMaxLength(512);
            entity.Property(e => e.ProductSku).IsRequired().HasMaxLength(128);
            entity.Property(e => e.UnitCost).HasPrecision(18, 4);
            entity.Property(e => e.Subtotal).HasPrecision(18, 2);
            entity.Property(e => e.Quantity).HasPrecision(18, 3);
            entity.Property(e => e.LotNumberSnapshot).HasMaxLength(128);
            entity
                .HasOne(e => e.Purchase)
                .WithMany(p => p.Details)
                .HasForeignKey(e => e.PurchaseId)
                .OnDelete(DeleteBehavior.Cascade);
            entity
                .HasOne(e => e.Product)
                .WithMany()
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureCashAndExpenses(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ExpenseCategory>(entity =>
        {
            entity.ToTable("ExpenseCategories");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(256);
            entity.HasIndex(e => e.TenantId);
        });

        modelBuilder.Entity<Expense>(entity =>
        {
            entity.ToTable("Expenses");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(1024);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity
                .HasOne(e => e.Category)
                .WithMany(c => c.Expenses)
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity
                .HasOne(e => e.CashSession)
                .WithMany(s => s.Expenses)
                .HasForeignKey(e => e.CashSessionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CashSession>(entity =>
        {
            entity.ToTable("CashSessions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.InitialAmount).HasPrecision(18, 2);
            entity.Property(e => e.ExpectedAmount).HasPrecision(18, 2);
            entity.Property(e => e.CountedAmount).HasPrecision(18, 2);
            entity.Property(e => e.Difference).HasPrecision(18, 2);
            entity.Property(e => e.UserId).HasMaxLength(128);
            entity
                .HasIndex(e => e.TenantId)
                .IsUnique()
                .HasFilter("[State] = 0");
        });
    }

    private static void ConfigureInventory(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StockLot>(entity =>
        {
            entity.ToTable("StockLots");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.LotNumber).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Quantity).HasPrecision(18, 3);
            entity.HasIndex(e => new { e.TenantId, e.ProductId, e.LotNumber }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.ProductId, e.ExpirationDate });
            entity
                .HasOne(e => e.Product)
                .WithMany()
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.ToTable("StockMovements");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Type).HasConversion<int>();
            entity.Property(e => e.QuantityDelta).HasPrecision(18, 3);
            entity.Property(e => e.QuantityAfter).HasPrecision(18, 3);
            entity.Property(e => e.LotNumberSnapshot).HasMaxLength(128);
            entity.Property(e => e.Reason).HasMaxLength(512);
            entity.Property(e => e.CreatedByUserId).IsRequired().HasMaxLength(128);
            entity.HasIndex(e => new { e.TenantId, e.ProductId, e.CreatedAt });
            entity
                .HasOne(e => e.Product)
                .WithMany()
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            entity
                .HasOne(e => e.StockLot)
                .WithMany()
                .HasForeignKey(e => e.StockLotId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureCustomer(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customers");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(512);
            entity.Property(e => e.TaxId).IsRequired().HasMaxLength(32);
            entity.Property(e => e.Email).HasMaxLength(320);
            entity.Property(e => e.Phone).HasMaxLength(64);
            entity.Property(e => e.Address).HasMaxLength(512);
            entity.HasIndex(e => new { e.TenantId, e.TaxId }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.Name });
        });
    }

    private void ConfigureTenantQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.IsKeyless)
                continue;

            var clr = entityType.ClrType;
            if (clr is null || !clr.IsClass || clr.IsAbstract || !typeof(ITenantEntity).IsAssignableFrom(clr))
                continue;

            // ApplicationUser: login por email sin filtrar por tenant en EF (el discrimidor es la fila + TenantId en app).
            // Product: **no** usar filtro global aquí; los handlers deben acotar explícitamente por tenant (ver docs/PLATFORM-QUERY-FILTERS.md).
            if (clr == typeof(ApplicationUser) || clr == typeof(Product))
                continue;

            var method = typeof(ApplicationDbContext)
                .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(m => m.Name == nameof(SetTenantFilter) && m.IsGenericMethodDefinition);

            method.MakeGenericMethod(clr).Invoke(this, [modelBuilder]);
        }
    }

    private void SetTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantEntity
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => e.TenantId == _currentTenantId);
    }

    private static void ConfigureTenant(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("Tenants");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(512);
            entity.Property(e => e.ContactEmail).HasMaxLength(320);
            entity.Property(e => e.BusinessType).IsRequired().HasMaxLength(64);
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.UpdatedAt);
            entity.Property(e => e.SuspendedAt);
            entity.Property(e => e.ClosedAt);
        });
    }

    private static void ConfigureTenantEntitlement(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TenantEntitlement>(entity =>
        {
            entity.ToTable("TenantEntitlements");
            entity.HasKey(e => e.TenantId);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.UpdatedAtUtc).IsRequired();

            entity
                .HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigurePlatformAuditEvent(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlatformAuditEvent>(entity =>
        {
            entity.ToTable("PlatformAuditEvents");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.Action).IsRequired().HasMaxLength(128);
            entity.Property(e => e.ActorUserId).HasMaxLength(128);
            entity.Property(e => e.ActorEmail).HasMaxLength(320);
            entity.Property(e => e.ResourceType).HasMaxLength(128);
            entity.Property(e => e.ResourceId).HasMaxLength(128);
            entity.Property(e => e.AffectedTenantId).HasMaxLength(128);
            entity.Property(e => e.Details).HasMaxLength(2000);
            entity.Property(e => e.Justification).HasMaxLength(2000);
            entity.Property(e => e.CorrelationId).HasMaxLength(128);
            entity.Property(e => e.IpAddress).HasMaxLength(128);

            entity.HasIndex(e => e.CreatedAtUtc);
            entity.HasIndex(e => e.AffectedTenantId);
            entity.HasIndex(e => e.ActorUserId);
        });
    }
}
