using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RetailShop.Domain.Common;
using RetailShop.Domain.Contacts;
using RetailShop.Domain.Inventory;
using RetailShop.Infrastructure.Auditing;
using RetailShop.Infrastructure.Identity;
using RetailShop.Domain.Products;
using RetailShop.Domain.Purchases;
using RetailShop.Domain.Sales;
using RetailShop.Domain.Quotations;
using RetailShop.Domain.CustomerAccounts;
using RetailShop.Domain.Settings;
using RetailShop.Domain.Returns;
using RetailShop.Domain.Warranty;
using RetailShop.Domain.OnlineOrders;
using RetailShop.Domain.DataExchange;
using RetailShop.Domain.Notifications;

namespace RetailShop.Infrastructure.Persistence;

public sealed class RetailShopDbContext(DbContextOptions<RetailShopDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    public DbSet<PermissionRecord> Permissions => Set<PermissionRecord>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<LoginLog> LoginLogs => Set<LoginLog>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<SubCategory> SubCategories => Set<SubCategory>();

    public DbSet<Brand> Brands => Set<Brand>();

    public DbSet<ProductModel> ProductModels => Set<ProductModel>();

    public DbSet<Unit> Units => Set<Unit>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<ProductImage> ProductImages => Set<ProductImage>();

    public DbSet<ProductBarcode> ProductBarcodes => Set<ProductBarcode>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<ShopSetting> ShopSettings => Set<ShopSetting>();

    public DbSet<InvoiceSetting> InvoiceSettings => Set<InvoiceSetting>();

    public DbSet<TaxSetting> TaxSettings => Set<TaxSetting>();

    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();

    public DbSet<StockBalance> StockBalances => Set<StockBalance>();

    public DbSet<StockTransaction> StockTransactions => Set<StockTransaction>();

    public DbSet<StockAdjustment> StockAdjustments => Set<StockAdjustment>();

    public DbSet<StockAdjustmentDetail> StockAdjustmentDetails =>
        Set<StockAdjustmentDetail>();

    public DbSet<Purchase> Purchases => Set<Purchase>();

    public DbSet<PurchaseDetail> PurchaseDetails => Set<PurchaseDetail>();

    public DbSet<PurchasePayment> PurchasePayments => Set<PurchasePayment>();

    public DbSet<PurchaseReturn> PurchaseReturns => Set<PurchaseReturn>();

    public DbSet<PurchaseReturnDetail> PurchaseReturnDetails =>
        Set<PurchaseReturnDetail>();

    public DbSet<SupplierLedgerEntry> SupplierLedgerEntries =>
        Set<SupplierLedgerEntry>();

    public DbSet<Sale> Sales => Set<Sale>();

    public DbSet<SaleDetail> SaleDetails => Set<SaleDetail>();

    public DbSet<SalePayment> SalePayments => Set<SalePayment>();

    public DbSet<CustomerLedgerEntry> CustomerLedgerEntries =>
        Set<CustomerLedgerEntry>();

    public DbSet<Quotation> Quotations => Set<Quotation>();

    public DbSet<QuotationDetail> QuotationDetails => Set<QuotationDetail>();

    public DbSet<CustomerReceipt> CustomerReceipts => Set<CustomerReceipt>();

    public DbSet<CustomerReceiptAllocation> CustomerReceiptAllocations =>
        Set<CustomerReceiptAllocation>();

    public DbSet<ComplaintReason> ComplaintReasons => Set<ComplaintReason>();

    public DbSet<SalesReturn> SalesReturns => Set<SalesReturn>();

    public DbSet<SalesReturnDetail> SalesReturnDetails => Set<SalesReturnDetail>();

    public DbSet<ReturnApproval> ReturnApprovals => Set<ReturnApproval>();

    public DbSet<ProductSerial> ProductSerials => Set<ProductSerial>();

    public DbSet<WarrantyClaim> WarrantyClaims => Set<WarrantyClaim>();

    public DbSet<WarrantyClaimHistory> WarrantyClaimHistories =>
        Set<WarrantyClaimHistory>();

    public DbSet<OnlineOrder> OnlineOrders => Set<OnlineOrder>();

    public DbSet<OnlineOrderDetail> OnlineOrderDetails => Set<OnlineOrderDetail>();

    public DbSet<OnlineOrderHistory> OnlineOrderHistories =>
        Set<OnlineOrderHistory>();

    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();

    public DbSet<ExportLog> ExportLogs => Set<ExportLog>();

    public DbSet<NotificationMessage> NotificationMessages =>
        Set<NotificationMessage>();

    public DbSet<BackgroundJobRun> BackgroundJobRuns => Set<BackgroundJobRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("Users");
            entity.Property(user => user.FullName).HasMaxLength(200).IsRequired();
            entity.HasIndex(user => user.Email);
        });
        modelBuilder.Entity<ApplicationRole>().ToTable("Roles");
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");

        modelBuilder.Entity<PermissionRecord>(entity =>
        {
            entity.ToTable("Permissions");
            entity.HasKey(permission => permission.Id);
            entity.Property(permission => permission.Name).HasMaxLength(150).IsRequired();
            entity.Property(permission => permission.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(permission => permission.Group).HasMaxLength(100).IsRequired();
            entity.HasIndex(permission => permission.Name).IsUnique();
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.ToTable("RolePermissions");
            entity.HasKey(rolePermission =>
                new { rolePermission.RoleId, rolePermission.PermissionId });
            entity
                .HasOne(rolePermission => rolePermission.Role)
                .WithMany()
                .HasForeignKey(rolePermission => rolePermission.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity
                .HasOne(rolePermission => rolePermission.Permission)
                .WithMany(permission => permission.RolePermissions)
                .HasForeignKey(rolePermission => rolePermission.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshTokens");
            entity.HasKey(token => token.Id);
            entity.Property(token => token.TokenHash).HasMaxLength(64).IsRequired();
            entity.Property(token => token.CreatedByIp).HasMaxLength(64).IsRequired();
            entity.Property(token => token.RevokedByIp).HasMaxLength(64);
            entity.Property(token => token.ReplacedByTokenHash).HasMaxLength(64);
            entity.Property(token => token.RevocationReason).HasMaxLength(200);
            entity.HasIndex(token => token.TokenHash).IsUnique();
            entity
                .HasOne(token => token.User)
                .WithMany(user => user.RefreshTokens)
                .HasForeignKey(token => token.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LoginLog>(entity =>
        {
            entity.ToTable("LoginLogs");
            entity.HasKey(log => log.Id);
            entity.Property(log => log.Email).HasMaxLength(256).IsRequired();
            entity.Property(log => log.FailureReason).HasMaxLength(200);
            entity.Property(log => log.IpAddress).HasMaxLength(64).IsRequired();
            entity.Property(log => log.UserAgent).HasMaxLength(500);
            entity.HasIndex(log => log.OccurredOn);
            entity.HasIndex(log => log.UserId);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.HasKey(log => log.Id);
            entity.Property(log => log.Action).HasMaxLength(100).IsRequired();
            entity.Property(log => log.EntityType).HasMaxLength(150).IsRequired();
            entity.Property(log => log.EntityId).HasMaxLength(100);
            entity.Property(log => log.Description).HasMaxLength(2000);
            entity.HasIndex(log => log.OccurredOn);
            entity.HasIndex(log => log.PerformedBy);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("Categories");
            entity.Property(category => category.Name).HasMaxLength(150).IsRequired();
            entity.Property(category => category.NormalizedName).HasMaxLength(150).IsRequired();
            entity.HasIndex(category => category.NormalizedName)
                .IsUnique()
                .HasFilter("\"IsDeleted\" = false");
            entity.HasQueryFilter(category => !category.IsDeleted);
        });

        modelBuilder.Entity<SubCategory>(entity =>
        {
            entity.ToTable("SubCategories");
            entity.Property(category => category.Name).HasMaxLength(150).IsRequired();
            entity.Property(category => category.NormalizedName).HasMaxLength(150).IsRequired();
            entity.HasIndex(category => new { category.CategoryId, category.NormalizedName })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = false");
            entity
                .HasOne(category => category.Category)
                .WithMany(category => category.SubCategories)
                .HasForeignKey(category => category.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(category => !category.IsDeleted);
        });

        modelBuilder.Entity<Brand>(entity =>
        {
            entity.ToTable("Brands");
            entity.Property(brand => brand.Name).HasMaxLength(150).IsRequired();
            entity.Property(brand => brand.NormalizedName).HasMaxLength(150).IsRequired();
            entity.HasIndex(brand => brand.NormalizedName)
                .IsUnique()
                .HasFilter("\"IsDeleted\" = false");
            entity.HasQueryFilter(brand => !brand.IsDeleted);
        });

        modelBuilder.Entity<ProductModel>(entity =>
        {
            entity.ToTable("ProductModels");
            entity.Property(model => model.Name).HasMaxLength(150).IsRequired();
            entity.Property(model => model.NormalizedName).HasMaxLength(150).IsRequired();
            entity.HasIndex(model => new { model.BrandId, model.NormalizedName })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = false");
            entity
                .HasOne(model => model.Brand)
                .WithMany(brand => brand.Models)
                .HasForeignKey(model => model.BrandId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(model => !model.IsDeleted);
        });

        modelBuilder.Entity<Unit>(entity =>
        {
            entity.ToTable("Units");
            entity.Property(unit => unit.Name).HasMaxLength(100).IsRequired();
            entity.Property(unit => unit.NormalizedName).HasMaxLength(100).IsRequired();
            entity.Property(unit => unit.Symbol).HasMaxLength(20).IsRequired();
            entity.HasIndex(unit => unit.NormalizedName)
                .IsUnique()
                .HasFilter("\"IsDeleted\" = false");
            entity.HasQueryFilter(unit => !unit.IsDeleted);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("Products");
            entity.Property(product => product.ProductCode).HasMaxLength(50).IsRequired();
            entity.Property(product => product.Name).HasMaxLength(250).IsRequired();
            entity.Property(product => product.VariantName).HasMaxLength(150);
            entity.Property(product => product.Description).HasMaxLength(2000);
            entity.Property(product => product.PurchasePrice).HasPrecision(18, 2);
            entity.Property(product => product.SalePrice).HasPrecision(18, 2);
            entity.Property(product => product.AverageCost).HasPrecision(18, 4);
            entity.Property(product => product.MinimumStockLevel).HasPrecision(18, 3);
            entity.HasIndex(product => product.ProductCode).IsUnique();
            entity.HasIndex(product => product.Name);
            entity
                .HasOne(product => product.Category)
                .WithMany()
                .HasForeignKey(product => product.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity
                .HasOne(product => product.SubCategory)
                .WithMany()
                .HasForeignKey(product => product.SubCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity
                .HasOne(product => product.Brand)
                .WithMany()
                .HasForeignKey(product => product.BrandId)
                .OnDelete(DeleteBehavior.Restrict);
            entity
                .HasOne(product => product.ProductModel)
                .WithMany()
                .HasForeignKey(product => product.ProductModelId)
                .OnDelete(DeleteBehavior.Restrict);
            entity
                .HasOne(product => product.Unit)
                .WithMany()
                .HasForeignKey(product => product.UnitId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(product => !product.IsDeleted);
        });

        modelBuilder.Entity<ProductImage>(entity =>
        {
            entity.ToTable("ProductImages");
            entity.Property(image => image.Url).HasMaxLength(1000).IsRequired();
            entity.Property(image => image.AltText).HasMaxLength(250);
            entity.HasIndex(image => new { image.ProductId, image.IsPrimary });
            entity
                .HasOne(image => image.Product)
                .WithMany(product => product.Images)
                .HasForeignKey(image => image.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(image => !image.IsDeleted);
        });

        modelBuilder.Entity<ProductBarcode>(entity =>
        {
            entity.ToTable("ProductBarcodes");
            entity.Property(barcode => barcode.Value).HasMaxLength(50).IsRequired();
            entity.HasIndex(barcode => barcode.Value).IsUnique();
            entity
                .HasOne(barcode => barcode.Product)
                .WithMany(product => product.Barcodes)
                .HasForeignKey(barcode => barcode.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(barcode => !barcode.IsDeleted);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customers");
            entity.Property(customer => customer.CustomerCode).HasMaxLength(30).IsRequired();
            entity.Property(customer => customer.Name).HasMaxLength(200).IsRequired();
            entity.Property(customer => customer.Phone).HasMaxLength(30).IsRequired();
            entity.Property(customer => customer.Email).HasMaxLength(256);
            entity.Property(customer => customer.Address).HasMaxLength(500);
            entity.Property(customer => customer.Notes).HasMaxLength(1000);
            entity.Property(customer => customer.CreditLimit).HasPrecision(18, 2);
            entity.HasIndex(customer => customer.CustomerCode).IsUnique();
            entity.HasIndex(customer => customer.Name);
            entity.HasIndex(customer => customer.Phone);
            entity.HasQueryFilter(customer => !customer.IsDeleted);
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.ToTable("Suppliers");
            entity.Property(supplier => supplier.SupplierCode).HasMaxLength(30).IsRequired();
            entity.Property(supplier => supplier.Name).HasMaxLength(200).IsRequired();
            entity.Property(supplier => supplier.ContactPerson).HasMaxLength(200);
            entity.Property(supplier => supplier.Phone).HasMaxLength(30).IsRequired();
            entity.Property(supplier => supplier.Email).HasMaxLength(256);
            entity.Property(supplier => supplier.Address).HasMaxLength(500);
            entity.Property(supplier => supplier.Notes).HasMaxLength(1000);
            entity.HasIndex(supplier => supplier.SupplierCode).IsUnique();
            entity.HasIndex(supplier => supplier.Name);
            entity.HasIndex(supplier => supplier.Phone);
            entity.HasQueryFilter(supplier => !supplier.IsDeleted);
        });

        modelBuilder.Entity<ShopSetting>(entity =>
        {
            entity.ToTable("ShopSettings");
            entity.Property(setting => setting.ShopName).HasMaxLength(200).IsRequired();
            entity.Property(setting => setting.Address).HasMaxLength(500);
            entity.Property(setting => setting.Phone).HasMaxLength(30);
            entity.Property(setting => setting.Email).HasMaxLength(256);
            entity.Property(setting => setting.LogoUrl).HasMaxLength(1000);
            entity.Property(setting => setting.TaxRegistrationNumber).HasMaxLength(100);
            entity.Property(setting => setting.ReceiptFooter).HasMaxLength(500).IsRequired();
            entity.HasQueryFilter(setting => !setting.IsDeleted);
        });

        modelBuilder.Entity<InvoiceSetting>(entity =>
        {
            entity.ToTable("InvoiceSettings");
            entity.Property(setting => setting.InvoicePrefix).HasMaxLength(20).IsRequired();
            entity.Property(setting => setting.TermsAndConditions).HasMaxLength(2000);
            entity.Property(setting => setting.ReturnPolicy).HasMaxLength(1000);
            entity.HasQueryFilter(setting => !setting.IsDeleted);
        });

        modelBuilder.Entity<TaxSetting>(entity =>
        {
            entity.ToTable("TaxSettings");
            entity.Property(setting => setting.TaxName).HasMaxLength(50).IsRequired();
            entity.Property(setting => setting.DefaultRate).HasPrecision(5, 2);
            entity.HasQueryFilter(setting => !setting.IsDeleted);
        });

        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.ToTable("SystemSettings");
            entity.Property(setting => setting.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(setting => setting.TimeZone).HasMaxLength(100).IsRequired();
            entity.Property(setting => setting.DateFormat).HasMaxLength(50).IsRequired();
            entity.HasQueryFilter(setting => !setting.IsDeleted);
        });

        modelBuilder.Entity<PaymentMethod>(entity =>
        {
            entity.ToTable("PaymentMethods");
            entity.Property(method => method.Name).HasMaxLength(100).IsRequired();
            entity.Property(method => method.NormalizedName).HasMaxLength(100).IsRequired();
            entity.Property(method => method.Code).HasMaxLength(30).IsRequired();
            entity.Property(method => method.Type).HasMaxLength(50).IsRequired();
            entity.HasIndex(method => method.NormalizedName)
                .IsUnique()
                .HasFilter("\"IsDeleted\" = false");
            entity.HasIndex(method => method.Code)
                .IsUnique()
                .HasFilter("\"IsDeleted\" = false");
            entity.HasQueryFilter(method => !method.IsDeleted);
        });

        modelBuilder.Entity<Sale>(entity =>
        {
            entity.ToTable("Sales");
            entity.Property(sale => sale.InvoiceNumber).HasMaxLength(30).IsRequired();
            entity.Property(sale => sale.Status)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(sale => sale.PaymentStatus)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(sale => sale.Subtotal).HasPrecision(18, 2);
            entity.Property(sale => sale.DiscountAmount).HasPrecision(18, 2);
            entity.Property(sale => sale.VatAmount).HasPrecision(18, 2);
            entity.Property(sale => sale.GrandTotal).HasPrecision(18, 2);
            entity.Property(sale => sale.PaidAmount).HasPrecision(18, 2);
            entity.Ignore(sale => sale.DueAmount);
            entity.Property(sale => sale.Notes).HasMaxLength(1000);
            entity.Property(sale => sale.CancellationReason).HasMaxLength(1000);
            entity.HasIndex(sale => sale.InvoiceNumber).IsUnique();
            entity.HasIndex(sale => sale.SaleDate);
            entity.HasIndex(sale => new { sale.CustomerId, sale.SaleDate });
            entity.HasIndex(sale => new { sale.Status, sale.PaymentStatus });
            entity
                .HasOne(sale => sale.Customer)
                .WithMany()
                .HasForeignKey(sale => sale.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(sale =>
                !sale.IsDeleted &&
                (sale.CustomerId == null || !sale.Customer!.IsDeleted));
        });

        modelBuilder.Entity<SaleDetail>(entity =>
        {
            entity.ToTable("SaleDetails");
            entity.Property(detail => detail.Quantity).HasPrecision(18, 3);
            entity.Property(detail => detail.UnitPrice).HasPrecision(18, 2);
            entity.Property(detail => detail.DiscountAmount).HasPrecision(18, 2);
            entity.Property(detail => detail.VatAmount).HasPrecision(18, 2);
            entity.Property(detail => detail.CostPrice).HasPrecision(18, 4);
            entity.Ignore(detail => detail.GrossAmount);
            entity.Ignore(detail => detail.LineTotal);
            entity.Ignore(detail => detail.CostTotal);
            entity.Ignore(detail => detail.Profit);
            entity.HasIndex(detail => new { detail.SaleId, detail.ProductId }).IsUnique();
            entity
                .HasOne(detail => detail.Sale)
                .WithMany(sale => sale.Details)
                .HasForeignKey(detail => detail.SaleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity
                .HasOne(detail => detail.Product)
                .WithMany()
                .HasForeignKey(detail => detail.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(detail =>
                !detail.Sale.IsDeleted && !detail.Product.IsDeleted);
        });

        modelBuilder.Entity<SalePayment>(entity =>
        {
            entity.ToTable("SalePayments");
            entity.Property(payment => payment.Amount).HasPrecision(18, 2);
            entity.Property(payment => payment.ReferenceNumber).HasMaxLength(100);
            entity.Property(payment => payment.Notes).HasMaxLength(500);
            entity.HasIndex(payment => new { payment.SaleId, payment.PaidOn });
            entity
                .HasOne(payment => payment.Sale)
                .WithMany(sale => sale.Payments)
                .HasForeignKey(payment => payment.SaleId)
                .OnDelete(DeleteBehavior.Restrict);
            entity
                .HasOne(payment => payment.PaymentMethod)
                .WithMany()
                .HasForeignKey(payment => payment.PaymentMethodId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(payment => !payment.Sale.IsDeleted);
        });

        modelBuilder.Entity<CustomerLedgerEntry>(entity =>
        {
            entity.ToTable("CustomerLedgerEntries");
            entity.Property(entry => entry.EntryType)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(entry => entry.Debit).HasPrecision(18, 2);
            entity.Property(entry => entry.Credit).HasPrecision(18, 2);
            entity.Property(entry => entry.ReferenceType).HasMaxLength(50).IsRequired();
            entity.Property(entry => entry.ReferenceNumber).HasMaxLength(100).IsRequired();
            entity.Property(entry => entry.Notes).HasMaxLength(500);
            entity.HasIndex(entry => new { entry.CustomerId, entry.EntryDate });
            entity.HasIndex(entry => new { entry.ReferenceType, entry.ReferenceId });
            entity
                .HasOne(entry => entry.Customer)
                .WithMany()
                .HasForeignKey(entry => entry.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(entry => !entry.Customer.IsDeleted);
        });

        modelBuilder.Entity<CustomerReceipt>(entity =>
        {
            entity.ToTable("CustomerReceipts");
            entity.Property(receipt => receipt.ReceiptNumber)
                .HasMaxLength(30)
                .IsRequired();
            entity.Property(receipt => receipt.Amount).HasPrecision(18, 2);
            entity.Property(receipt => receipt.AllocatedAmount).HasPrecision(18, 2);
            entity.Ignore(receipt => receipt.AccountAppliedAmount);
            entity.Property(receipt => receipt.ReferenceNumber).HasMaxLength(100);
            entity.Property(receipt => receipt.Notes).HasMaxLength(500);
            entity.HasIndex(receipt => receipt.ReceiptNumber).IsUnique();
            entity.HasIndex(receipt => new
            {
                receipt.CustomerId,
                receipt.ReceivedOn
            });
            entity
                .HasOne(receipt => receipt.Customer)
                .WithMany()
                .HasForeignKey(receipt => receipt.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity
                .HasOne(receipt => receipt.PaymentMethod)
                .WithMany()
                .HasForeignKey(receipt => receipt.PaymentMethodId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(receipt => !receipt.Customer.IsDeleted);
        });

        modelBuilder.Entity<CustomerReceiptAllocation>(entity =>
        {
            entity.ToTable("CustomerReceiptAllocations");
            entity.Property(allocation => allocation.Amount).HasPrecision(18, 2);
            entity.HasIndex(allocation => new
            {
                allocation.CustomerReceiptId,
                allocation.SaleId
            }).IsUnique();
            entity
                .HasOne(allocation => allocation.CustomerReceipt)
                .WithMany(receipt => receipt.Allocations)
                .HasForeignKey(allocation => allocation.CustomerReceiptId)
                .OnDelete(DeleteBehavior.Cascade);
            entity
                .HasOne(allocation => allocation.Sale)
                .WithMany()
                .HasForeignKey(allocation => allocation.SaleId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(allocation =>
                !allocation.CustomerReceipt.Customer.IsDeleted &&
                !allocation.Sale.IsDeleted);
        });

        modelBuilder.Entity<Quotation>(entity =>
        {
            entity.ToTable("Quotations");
            entity.Property(quotation => quotation.QuotationNumber)
                .HasMaxLength(30)
                .IsRequired();
            entity.Property(quotation => quotation.Status)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(quotation => quotation.Subtotal).HasPrecision(18, 2);
            entity.Property(quotation => quotation.DiscountAmount).HasPrecision(18, 2);
            entity.Property(quotation => quotation.VatAmount).HasPrecision(18, 2);
            entity.Property(quotation => quotation.GrandTotal).HasPrecision(18, 2);
            entity.Property(quotation => quotation.Notes).HasMaxLength(1000);
            entity.Property(quotation => quotation.Terms).HasMaxLength(2000);
            entity.Property(quotation => quotation.RejectionReason).HasMaxLength(1000);
            entity.HasIndex(quotation => quotation.QuotationNumber).IsUnique();
            entity.HasIndex(quotation => new
            {
                quotation.Status,
                quotation.ValidUntil
            });
            entity.HasIndex(quotation => new
            {
                quotation.CustomerId,
                quotation.QuotationDate
            });
            entity.HasIndex(quotation => quotation.ConvertedSaleId)
                .IsUnique()
                .HasFilter("\"ConvertedSaleId\" IS NOT NULL");
            entity
                .HasOne(quotation => quotation.Customer)
                .WithMany()
                .HasForeignKey(quotation => quotation.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity
                .HasOne(quotation => quotation.ConvertedSale)
                .WithOne()
                .HasForeignKey<Quotation>(quotation => quotation.ConvertedSaleId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(quotation =>
                !quotation.IsDeleted && !quotation.Customer.IsDeleted);
        });

        modelBuilder.Entity<QuotationDetail>(entity =>
        {
            entity.ToTable("QuotationDetails");
            entity.Property(detail => detail.Quantity).HasPrecision(18, 3);
            entity.Property(detail => detail.UnitPrice).HasPrecision(18, 2);
            entity.Property(detail => detail.DiscountAmount).HasPrecision(18, 2);
            entity.Property(detail => detail.VatAmount).HasPrecision(18, 2);
            entity.Ignore(detail => detail.GrossAmount);
            entity.Ignore(detail => detail.LineTotal);
            entity.HasIndex(detail => new
            {
                detail.QuotationId,
                detail.ProductId
            }).IsUnique();
            entity
                .HasOne(detail => detail.Quotation)
                .WithMany(quotation => quotation.Details)
                .HasForeignKey(detail => detail.QuotationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity
                .HasOne(detail => detail.Product)
                .WithMany()
                .HasForeignKey(detail => detail.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(detail =>
                !detail.Quotation.IsDeleted && !detail.Product.IsDeleted);
        });

        modelBuilder.Entity<Purchase>(entity =>
        {
            entity.ToTable("Purchases");
            entity.Property(purchase => purchase.PurchaseNumber)
                .HasMaxLength(30)
                .IsRequired();
            entity.Property(purchase => purchase.SupplierInvoiceNumber).HasMaxLength(100);
            entity.Property(purchase => purchase.Status)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(purchase => purchase.PaymentStatus)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(purchase => purchase.Subtotal).HasPrecision(18, 2);
            entity.Property(purchase => purchase.DiscountAmount).HasPrecision(18, 2);
            entity.Property(purchase => purchase.VatAmount).HasPrecision(18, 2);
            entity.Property(purchase => purchase.GrandTotal).HasPrecision(18, 2);
            entity.Property(purchase => purchase.ReturnedAmount).HasPrecision(18, 2);
            entity.Property(purchase => purchase.PaidAmount).HasPrecision(18, 2);
            entity.Ignore(purchase => purchase.DueAmount);
            entity.Property(purchase => purchase.Notes).HasMaxLength(1000);
            entity.HasIndex(purchase => purchase.PurchaseNumber).IsUnique();
            entity.HasIndex(purchase => new { purchase.SupplierId, purchase.PurchaseDate });
            entity.HasIndex(purchase => new { purchase.Status, purchase.PaymentStatus });
            entity.HasIndex(purchase => new
                {
                    purchase.SupplierId,
                    purchase.SupplierInvoiceNumber
                })
                .IsUnique()
                .HasFilter("\"SupplierInvoiceNumber\" IS NOT NULL AND \"IsDeleted\" = false");
            entity
                .HasOne(purchase => purchase.Supplier)
                .WithMany()
                .HasForeignKey(purchase => purchase.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(purchase => !purchase.IsDeleted);
        });

        modelBuilder.Entity<PurchaseDetail>(entity =>
        {
            entity.ToTable("PurchaseDetails");
            entity.Property(detail => detail.Quantity).HasPrecision(18, 3);
            entity.Property(detail => detail.ReturnedQuantity).HasPrecision(18, 3);
            entity.Property(detail => detail.UnitCost).HasPrecision(18, 4);
            entity.Property(detail => detail.DiscountAmount).HasPrecision(18, 2);
            entity.Property(detail => detail.VatAmount).HasPrecision(18, 2);
            entity.Ignore(detail => detail.GrossAmount);
            entity.Ignore(detail => detail.LineTotal);
            entity.Ignore(detail => detail.NetUnitCost);
            entity.HasIndex(detail => new { detail.PurchaseId, detail.ProductId }).IsUnique();
            entity
                .HasOne(detail => detail.Purchase)
                .WithMany(purchase => purchase.Details)
                .HasForeignKey(detail => detail.PurchaseId)
                .OnDelete(DeleteBehavior.Cascade);
            entity
                .HasOne(detail => detail.Product)
                .WithMany()
                .HasForeignKey(detail => detail.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(detail =>
                !detail.Purchase.IsDeleted && !detail.Product.IsDeleted);
        });

        modelBuilder.Entity<PurchasePayment>(entity =>
        {
            entity.ToTable("PurchasePayments");
            entity.Property(payment => payment.Amount).HasPrecision(18, 2);
            entity.Property(payment => payment.ReferenceNumber).HasMaxLength(100);
            entity.Property(payment => payment.Notes).HasMaxLength(500);
            entity.HasIndex(payment => new { payment.PurchaseId, payment.PaidOn });
            entity
                .HasOne(payment => payment.Purchase)
                .WithMany(purchase => purchase.Payments)
                .HasForeignKey(payment => payment.PurchaseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity
                .HasOne(payment => payment.PaymentMethod)
                .WithMany()
                .HasForeignKey(payment => payment.PaymentMethodId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(payment => !payment.Purchase.IsDeleted);
        });

        modelBuilder.Entity<PurchaseReturn>(entity =>
        {
            entity.ToTable("PurchaseReturns");
            entity.Property(item => item.ReturnNumber).HasMaxLength(30).IsRequired();
            entity.Property(item => item.Reason).HasMaxLength(500).IsRequired();
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.Property(item => item.TotalAmount).HasPrecision(18, 2);
            entity.HasIndex(item => item.ReturnNumber).IsUnique();
            entity.HasIndex(item => new { item.PurchaseId, item.ReturnDate });
            entity
                .HasOne(item => item.Purchase)
                .WithMany(purchase => purchase.Returns)
                .HasForeignKey(item => item.PurchaseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(item => !item.Purchase.IsDeleted);
        });

        modelBuilder.Entity<PurchaseReturnDetail>(entity =>
        {
            entity.ToTable("PurchaseReturnDetails");
            entity.Property(item => item.Quantity).HasPrecision(18, 3);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity
                .HasOne(item => item.PurchaseReturn)
                .WithMany(purchaseReturn => purchaseReturn.Details)
                .HasForeignKey(item => item.PurchaseReturnId)
                .OnDelete(DeleteBehavior.Cascade);
            entity
                .HasOne(item => item.PurchaseDetail)
                .WithMany()
                .HasForeignKey(item => item.PurchaseDetailId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(item =>
                !item.PurchaseReturn.Purchase.IsDeleted &&
                !item.PurchaseDetail.Purchase.IsDeleted);
        });

        modelBuilder.Entity<SupplierLedgerEntry>(entity =>
        {
            entity.ToTable("SupplierLedgerEntries");
            entity.Property(item => item.EntryType)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(item => item.Debit).HasPrecision(18, 2);
            entity.Property(item => item.Credit).HasPrecision(18, 2);
            entity.Property(item => item.ReferenceType).HasMaxLength(50).IsRequired();
            entity.Property(item => item.ReferenceNumber).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Notes).HasMaxLength(500);
            entity.HasIndex(item => new { item.SupplierId, item.EntryDate });
            entity.HasIndex(item => new { item.ReferenceType, item.ReferenceId });
            entity
                .HasOne(item => item.Supplier)
                .WithMany()
                .HasForeignKey(item => item.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(item => !item.Supplier.IsDeleted);
        });

        modelBuilder.Entity<StockBalance>(entity =>
        {
            entity.ToTable("StockBalances");
            entity.Property(balance => balance.AvailableQuantity).HasPrecision(18, 3);
            entity.Property(balance => balance.ReservedQuantity).HasPrecision(18, 3);
            entity.Property(balance => balance.DamagedQuantity).HasPrecision(18, 3);
            entity.Property(balance => balance.WarrantyQuantity).HasPrecision(18, 3);
            entity.Property(balance => balance.SupplierClaimQuantity).HasPrecision(18, 3);
            entity.Property(balance => balance.Version).IsConcurrencyToken();
            entity.HasIndex(balance => balance.ProductId).IsUnique();
            entity
                .HasOne(balance => balance.Product)
                .WithOne()
                .HasForeignKey<StockBalance>(balance => balance.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(balance => !balance.Product.IsDeleted);
        });

        modelBuilder.Entity<StockTransaction>(entity =>
        {
            entity.ToTable("StockTransactions");
            entity.Property(transaction => transaction.TransactionType)
                .HasConversion<string>()
                .HasMaxLength(50);
            entity.Property(transaction => transaction.Bucket)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(transaction => transaction.QuantityIn).HasPrecision(18, 3);
            entity.Property(transaction => transaction.QuantityOut).HasPrecision(18, 3);
            entity.Property(transaction => transaction.BalanceQuantity).HasPrecision(18, 3);
            entity.Property(transaction => transaction.CostPrice).HasPrecision(18, 4);
            entity.Property(transaction => transaction.AverageCostAfterTransaction)
                .HasPrecision(18, 4);
            entity.Property(transaction => transaction.ReferenceType)
                .HasMaxLength(100)
                .IsRequired();
            entity.Property(transaction => transaction.Remarks).HasMaxLength(500);
            entity.HasIndex(transaction => transaction.TransactionDate);
            entity.HasIndex(transaction => transaction.ProductId);
            entity.HasIndex(transaction => new
            {
                transaction.ReferenceType,
                transaction.ReferenceId
            });
            entity
                .HasOne(transaction => transaction.Product)
                .WithMany()
                .HasForeignKey(transaction => transaction.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(transaction => !transaction.Product.IsDeleted);
        });

        modelBuilder.Entity<StockAdjustment>(entity =>
        {
            entity.ToTable("StockAdjustments");
            entity.Property(adjustment => adjustment.AdjustmentNumber)
                .HasMaxLength(30)
                .IsRequired();
            entity.Property(adjustment => adjustment.Reason).HasMaxLength(500).IsRequired();
            entity.Property(adjustment => adjustment.Notes).HasMaxLength(1000);
            entity.Property(adjustment => adjustment.Status)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(adjustment => adjustment.ReviewNotes).HasMaxLength(1000);
            entity.Property(adjustment => adjustment.ReversalReason).HasMaxLength(1000);
            entity.HasIndex(adjustment => adjustment.AdjustmentNumber).IsUnique();
            entity.HasIndex(adjustment => new
            {
                adjustment.Status,
                adjustment.RequestedOn
            });
        });

        modelBuilder.Entity<StockAdjustmentDetail>(entity =>
        {
            entity.ToTable("StockAdjustmentDetails");
            entity.Property(detail => detail.Bucket)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(detail => detail.Direction)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(detail => detail.Quantity).HasPrecision(18, 3);
            entity.Property(detail => detail.UnitCost).HasPrecision(18, 4);
            entity.Property(detail => detail.AppliedCost).HasPrecision(18, 4);
            entity
                .HasOne(detail => detail.StockAdjustment)
                .WithMany(adjustment => adjustment.Details)
                .HasForeignKey(detail => detail.StockAdjustmentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity
                .HasOne(detail => detail.Product)
                .WithMany()
                .HasForeignKey(detail => detail.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(detail => !detail.Product.IsDeleted);
        });

        modelBuilder.Entity<ComplaintReason>(entity =>
        {
            entity.ToTable("ComplaintReasons");
            entity.Property(item => item.Name).HasMaxLength(150).IsRequired();
            entity.HasIndex(item => item.Name).IsUnique();
            entity.HasData(
                new
                {
                    Id = Guid.Parse("019f32f0-0000-7000-8000-000000000001"),
                    Name = "Defective or not working",
                    DisplayOrder = 1,
                    IsActive = true
                },
                new
                {
                    Id = Guid.Parse("019f32f0-0000-7000-8000-000000000002"),
                    Name = "Wrong item supplied",
                    DisplayOrder = 2,
                    IsActive = true
                },
                new
                {
                    Id = Guid.Parse("019f32f0-0000-7000-8000-000000000003"),
                    Name = "Damaged after sale",
                    DisplayOrder = 3,
                    IsActive = true
                },
                new
                {
                    Id = Guid.Parse("019f32f0-0000-7000-8000-000000000004"),
                    Name = "Customer changed mind",
                    DisplayOrder = 4,
                    IsActive = true
                });
        });

        modelBuilder.Entity<SalesReturn>(entity =>
        {
            entity.ToTable("SalesReturns");
            entity.Property(item => item.ReturnNumber).HasMaxLength(30).IsRequired();
            entity.Property(item => item.ProductCondition)
                .HasConversion<string>().HasMaxLength(30);
            entity.Property(item => item.RequestedAction)
                .HasConversion<string>().HasMaxLength(30);
            entity.Property(item => item.Status)
                .HasConversion<string>().HasMaxLength(30);
            entity.Property(item => item.TotalAmount).HasPrecision(18, 2);
            entity.Property(item => item.RefundedAmount).HasPrecision(18, 2);
            entity.Property(item => item.DueAdjustedAmount).HasPrecision(18, 2);
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.Property(item => item.ReviewNotes).HasMaxLength(1000);
            entity.HasIndex(item => item.ReturnNumber).IsUnique();
            entity.HasIndex(item => new { item.Status, item.RequestedOn });
            entity.HasOne(item => item.Sale).WithMany()
                .HasForeignKey(item => item.SaleId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ComplaintReason).WithMany()
                .HasForeignKey(item => item.ComplaintReasonId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.PaymentMethod).WithMany()
                .HasForeignKey(item => item.PaymentMethodId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(item => !item.Sale.IsDeleted);
        });

        modelBuilder.Entity<SalesReturnDetail>(entity =>
        {
            entity.ToTable("SalesReturnDetails");
            entity.Property(item => item.Quantity).HasPrecision(18, 3);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.HasIndex(item => new { item.SalesReturnId, item.SaleDetailId })
                .IsUnique();
            entity.HasOne(item => item.SalesReturn)
                .WithMany(item => item.Details)
                .HasForeignKey(item => item.SalesReturnId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.SaleDetail).WithMany()
                .HasForeignKey(item => item.SaleDetailId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReturnApproval>(entity =>
        {
            entity.ToTable("ReturnApprovals");
            entity.Property(item => item.Action)
                .HasConversion<string>().HasMaxLength(30);
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.HasIndex(item => new { item.SalesReturnId, item.PerformedOn });
            entity.HasOne(item => item.SalesReturn)
                .WithMany(item => item.History)
                .HasForeignKey(item => item.SalesReturnId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductSerial>(entity =>
        {
            entity.ToTable("ProductSerials");
            entity.Property(item => item.SerialNumber).HasMaxLength(100).IsRequired();
            entity.Property(item => item.NormalizedSerialNumber)
                .HasMaxLength(100)
                .IsRequired();
            entity.Property(item => item.Status)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.HasIndex(item => item.NormalizedSerialNumber)
                .IsUnique()
                .HasFilter("\"IsDeleted\" = false");
            entity.HasIndex(item => new { item.ProductId, item.Status });
            entity.HasIndex(item => item.SaleId);
            entity.HasOne(item => item.Product).WithMany()
                .HasForeignKey(item => item.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Purchase).WithMany()
                .HasForeignKey(item => item.PurchaseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.PurchaseDetail).WithMany()
                .HasForeignKey(item => item.PurchaseDetailId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Sale).WithMany()
                .HasForeignKey(item => item.SaleId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.SaleDetail).WithMany()
                .HasForeignKey(item => item.SaleDetailId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Customer).WithMany()
                .HasForeignKey(item => item.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(item => !item.IsDeleted && !item.Product.IsDeleted);
        });

        modelBuilder.Entity<WarrantyClaim>(entity =>
        {
            entity.ToTable("WarrantyClaims");
            entity.Property(item => item.ClaimNumber).HasMaxLength(30).IsRequired();
            entity.Property(item => item.Complaint).HasMaxLength(1000).IsRequired();
            entity.Property(item => item.RequestedAction)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(item => item.Status)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.Property(item => item.ReviewNotes).HasMaxLength(1000);
            entity.HasIndex(item => item.ClaimNumber).IsUnique();
            entity.HasIndex(item => new { item.Status, item.RequestedOn });
            entity.HasIndex(item => item.ProductSerialId);
            entity.HasOne(item => item.ProductSerial).WithMany()
                .HasForeignKey(item => item.ProductSerialId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Sale).WithMany()
                .HasForeignKey(item => item.SaleId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Customer).WithMany()
                .HasForeignKey(item => item.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(item =>
                !item.IsDeleted &&
                !item.ProductSerial.IsDeleted &&
                !item.ProductSerial.Product.IsDeleted &&
                !item.Sale.IsDeleted);
        });

        modelBuilder.Entity<WarrantyClaimHistory>(entity =>
        {
            entity.ToTable("WarrantyClaimHistories");
            entity.Property(item => item.Action)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.HasIndex(item => new { item.WarrantyClaimId, item.PerformedOn });
            entity.HasOne(item => item.WarrantyClaim)
                .WithMany(item => item.History)
                .HasForeignKey(item => item.WarrantyClaimId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(item =>
                !item.WarrantyClaim.IsDeleted &&
                !item.WarrantyClaim.ProductSerial.IsDeleted &&
                !item.WarrantyClaim.ProductSerial.Product.IsDeleted &&
                !item.WarrantyClaim.Sale.IsDeleted);
        });

        modelBuilder.Entity<OnlineOrder>(entity =>
        {
            entity.ToTable("OnlineOrders");
            entity.Property(item => item.OrderNumber).HasMaxLength(30).IsRequired();
            entity.Property(item => item.Source)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(item => item.Status)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(item => item.CustomerName).HasMaxLength(200).IsRequired();
            entity.Property(item => item.CustomerPhone).HasMaxLength(30).IsRequired();
            entity.Property(item => item.DeliveryAddress).HasMaxLength(500).IsRequired();
            entity.Property(item => item.Subtotal).HasPrecision(18, 2);
            entity.Property(item => item.DeliveryCharge).HasPrecision(18, 2);
            entity.Ignore(item => item.GrandTotal);
            entity.Property(item => item.CourierName).HasMaxLength(150);
            entity.Property(item => item.TrackingNumber).HasMaxLength(150);
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.HasIndex(item => item.OrderNumber).IsUnique();
            entity.HasIndex(item => new { item.Status, item.OrderedOn });
            entity.HasIndex(item => item.CustomerPhone);
            entity.HasQueryFilter(item => !item.IsDeleted);
        });

        modelBuilder.Entity<OnlineOrderDetail>(entity =>
        {
            entity.ToTable("OnlineOrderDetails");
            entity.Property(item => item.Quantity).HasPrecision(18, 3);
            entity.Property(item => item.UnitPrice).HasPrecision(18, 2);
            entity.Property(item => item.CostPrice).HasPrecision(18, 4);
            entity.Ignore(item => item.LineTotal);
            entity.HasIndex(item => new { item.OnlineOrderId, item.ProductId })
                .IsUnique();
            entity.HasOne(item => item.OnlineOrder)
                .WithMany(item => item.Details)
                .HasForeignKey(item => item.OnlineOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Product).WithMany()
                .HasForeignKey(item => item.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(item =>
                !item.OnlineOrder.IsDeleted && !item.Product.IsDeleted);
        });

        modelBuilder.Entity<OnlineOrderHistory>(entity =>
        {
            entity.ToTable("OnlineOrderHistories");
            entity.Property(item => item.Action)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.HasIndex(item => new { item.OnlineOrderId, item.PerformedOn });
            entity.HasOne(item => item.OnlineOrder)
                .WithMany(item => item.History)
                .HasForeignKey(item => item.OnlineOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(item => !item.OnlineOrder.IsDeleted);
        });

        modelBuilder.Entity<ImportBatch>(entity =>
        {
            entity.ToTable("ImportBatches");
            entity.Property(item => item.BatchNumber).HasMaxLength(60).IsRequired();
            entity.Property(item => item.Kind)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(item => item.Status)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(item => item.OriginalFileName).HasMaxLength(260);
            entity.HasIndex(item => item.BatchNumber).IsUnique();
            entity.HasIndex(item => new { item.Kind, item.CreatedOn });
        });

        modelBuilder.Entity<ExportLog>(entity =>
        {
            entity.ToTable("ExportLogs");
            entity.Property(item => item.Kind)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(item => item.FileName).HasMaxLength(260).IsRequired();
            entity.HasIndex(item => new { item.Kind, item.CreatedOn });
        });

        modelBuilder.Entity<NotificationMessage>(entity =>
        {
            entity.ToTable("NotificationMessages");
            entity.Property(item => item.Kind)
                .HasConversion<string>()
                .HasMaxLength(40);
            entity.Property(item => item.Channel)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(item => item.Status)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(item => item.DeduplicationKey)
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(item => item.Title).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Message).HasMaxLength(1000).IsRequired();
            entity.Property(item => item.RecipientName).HasMaxLength(200);
            entity.Property(item => item.RecipientPhone).HasMaxLength(30);
            entity.HasIndex(item => item.DeduplicationKey).IsUnique();
            entity.HasIndex(item => new { item.Kind, item.Status, item.GeneratedOn });
            entity.HasQueryFilter(item => !item.IsDeleted);
        });

        modelBuilder.Entity<BackgroundJobRun>(entity =>
        {
            entity.ToTable("BackgroundJobRuns");
            entity.Property(item => item.JobName).HasMaxLength(100).IsRequired();
            entity.Property(item => item.RunKey).HasMaxLength(150).IsRequired();
            entity.Property(item => item.Status)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(item => item.Error).HasMaxLength(1000);
            entity.HasIndex(item => item.RunKey).IsUnique();
            entity.HasIndex(item => new { item.JobName, item.StartedOn });
            entity.HasQueryFilter(item => !item.IsDeleted);
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyAuditInformation();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditInformation();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplyAuditInformation()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedOn = now;
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Entity.LastModifiedOn = now;
            }

            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedOn = now;
                entry.Entity.LastModifiedOn = now;
            }
        }
    }
}
