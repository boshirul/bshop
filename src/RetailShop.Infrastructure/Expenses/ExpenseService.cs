using Microsoft.EntityFrameworkCore;
using RetailShop.Application.Auditing;
using RetailShop.Application.Common;
using RetailShop.Application.Expenses;
using RetailShop.Application.Finance;
using RetailShop.Domain.Expenses;
using RetailShop.Infrastructure.Persistence;

namespace RetailShop.Infrastructure.Expenses;

internal sealed class ExpenseService(RetailShopDbContext db, IAuditService audit,
    IAutomaticAccountingService? accounting = null) : IExpenseService
{
    public async Task<IReadOnlyCollection<ExpenseCategoryItem>> GetCategoriesAsync(CancellationToken ct) => await db.ExpenseCategories.AsNoTracking().OrderBy(x => x.Name).Select(x => new ExpenseCategoryItem(x.Id, x.Code, x.Name, x.IsActive)).ToArrayAsync(ct);
    public async Task<OperationResult<ExpenseCategoryItem>> SaveCategoryAsync(Guid? id, SaveExpenseCategoryRequest req, Guid userId, CancellationToken ct)
    {
        var duplicate = await db.ExpenseCategories.AnyAsync(x => x.Code == req.Code.Trim().ToUpper() && (!id.HasValue || x.Id != id), ct);
        if (duplicate) return OperationResult<ExpenseCategoryItem>.Failure("Expense category code already exists.");
        ExpenseCategory item;
        if (id.HasValue) { var existing = await db.ExpenseCategories.SingleOrDefaultAsync(x => x.Id == id, ct); if (existing is null) return OperationResult<ExpenseCategoryItem>.Failure("Expense category not found."); item = existing; item.Update(req.Code, req.Name, req.IsActive); item.LastModifiedBy = userId; item.LastModifiedOn = DateTimeOffset.UtcNow; }
        else { item = new ExpenseCategory(req.Code, req.Name) { CreatedBy = userId }; db.ExpenseCategories.Add(item); }
        await db.SaveChangesAsync(ct); await audit.WriteAsync(id.HasValue ? "Update" : "Create", "ExpenseCategory", item.Id.ToString(), item.Name, userId, ct);
        return OperationResult<ExpenseCategoryItem>.Success(new(item.Id, item.Code, item.Name, item.IsActive));
    }
    public async Task<PagedResult<ExpenseVoucherListItem>> GetVouchersAsync(string? search, DateOnly? from, DateOnly? to, int page, int size, CancellationToken ct)
    {
        var q = db.ExpenseVouchers.AsNoTracking().Include(x => x.PaymentMethod).AsQueryable(); if (!string.IsNullOrWhiteSpace(search)) q = q.Where(x => EF.Functions.ILike(x.VoucherNumber, $"%{search.Trim()}%") || EF.Functions.ILike(x.Payee, $"%{search.Trim()}%")); if (from.HasValue) q = q.Where(x => x.ExpenseDate >= from); if (to.HasValue) q = q.Where(x => x.ExpenseDate <= to); var total = await q.CountAsync(ct); var rows = await q.OrderByDescending(x => x.ExpenseDate).ThenByDescending(x => x.CreatedOn).Skip((Math.Max(1,page)-1)*Math.Clamp(size,1,100)).Take(Math.Clamp(size,1,100)).Select(x => new ExpenseVoucherListItem(x.Id,x.VoucherNumber,x.ExpenseDate,x.Payee,x.PaymentMethod == null ? null : x.PaymentMethod.Name,x.Status.ToString(),x.TotalAmount)).ToArrayAsync(ct); return new(rows, Math.Max(1,page), Math.Clamp(size,1,100), total);
    }
    public async Task<OperationResult<ExpenseVoucherItem>> GetVoucherAsync(Guid id, CancellationToken ct) { var x=await db.ExpenseVouchers.AsNoTracking().Include(x=>x.PaymentMethod).Include(x=>x.Details).ThenInclude(x=>x.Category).SingleOrDefaultAsync(x=>x.Id==id,ct); return x is null ? OperationResult<ExpenseVoucherItem>.Failure("Expense voucher not found.") : OperationResult<ExpenseVoucherItem>.Success(Map(x)); }
    public async Task<OperationResult<ExpenseVoucherItem>> CreateAsync(SaveExpenseVoucherRequest req, Guid userId, CancellationToken ct) { var valid=await Validate(req,ct); if (valid is not null) return OperationResult<ExpenseVoucherItem>.Failure(valid); await using var tx=await db.Database.BeginTransactionAsync(ct); var number=$"EXP-{req.ExpenseDate:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}"; var x=new ExpenseVoucher(number,req.ExpenseDate,req.Payee,req.PaymentMethodId,req.GeneralRemarks,userId); x.ReplaceDetails(req.Lines.Select(x=>new ExpenseVoucherDetail(x.CategoryId,x.Description,x.Amount,x.Reference))); db.ExpenseVouchers.Add(x); await db.SaveChangesAsync(ct); await audit.WriteAsync("Create","ExpenseVoucher",x.Id.ToString(),number,userId,ct); await tx.CommitAsync(ct); return await GetVoucherAsync(x.Id,ct); }
    public async Task<OperationResult<ExpenseVoucherItem>> UpdateAsync(Guid id,SaveExpenseVoucherRequest req,Guid userId,CancellationToken ct) { var valid=await Validate(req,ct); if(valid is not null)return OperationResult<ExpenseVoucherItem>.Failure(valid); var x=await db.ExpenseVouchers.Include(x=>x.Details).SingleOrDefaultAsync(x=>x.Id==id,ct); if(x is null)return OperationResult<ExpenseVoucherItem>.Failure("Expense voucher not found."); try{x.Update(req.ExpenseDate,req.Payee,req.PaymentMethodId,req.GeneralRemarks); x.ReplaceDetails(req.Lines.Select(x=>new ExpenseVoucherDetail(x.CategoryId,x.Description,x.Amount,x.Reference))); x.LastModifiedBy=userId;x.LastModifiedOn=DateTimeOffset.UtcNow;await db.SaveChangesAsync(ct);await audit.WriteAsync("Update","ExpenseVoucher",id.ToString(),x.VoucherNumber,userId,ct);return await GetVoucherAsync(id,ct);}catch(Exception e){return OperationResult<ExpenseVoucherItem>.Failure(e.Message);} }
    public async Task<OperationResult<ExpenseVoucherItem>> PostAsync(Guid id,Guid userId,CancellationToken ct)=>await ChangeStatus(id,userId,null,true,ct);
    public async Task<OperationResult<ExpenseVoucherItem>> VoidAsync(Guid id,string reason,Guid userId,CancellationToken ct)=>await ChangeStatus(id,userId,reason,false,ct);
    private async Task<OperationResult<ExpenseVoucherItem>> ChangeStatus(Guid id,Guid userId,string? reason,bool post,CancellationToken ct)
    {
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        var x=await db.ExpenseVouchers.Include(x=>x.Details).SingleOrDefaultAsync(x=>x.Id==id,ct);
        if(x is null)return OperationResult<ExpenseVoucherItem>.Failure("Expense voucher not found.");
        try
        {
            if(post)x.Post(userId);else x.Void(userId,reason??string.Empty);
            if(accounting is not null)
            {
                var finance = post
                    ? await accounting.PostExpenseAsync(new AutomaticExpensePosting(x.Id,x.ExpenseDate,x.PaymentMethodId,
                        x.Details.Select(d=>new AccountingExpenseLine(d.ExpenseCategoryId,d.Amount)).ToArray()),userId,ct)
                    : await accounting.ReverseAsync("ExpenseVoucher",x.Id,DateTimeOffset.UtcNow,reason??string.Empty,userId,ct,
                        new DateTimeOffset(x.ExpenseDate.ToDateTime(TimeOnly.MinValue),TimeSpan.FromHours(6)));
                if(!finance.Succeeded)return OperationResult<ExpenseVoucherItem>.Failure($"Finance posting failed: {string.Join("; ",finance.Errors)}");
            }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            await audit.WriteAsync(post?"Post":"Void","ExpenseVoucher",id.ToString(),x.VoucherNumber,userId,ct);
            return await GetVoucherAsync(id,ct);
        }
        catch(Exception e){return OperationResult<ExpenseVoucherItem>.Failure(e.Message);}
    }
    private async Task<string?> Validate(SaveExpenseVoucherRequest r,CancellationToken ct){if(string.IsNullOrWhiteSpace(r.Payee)||r.Lines.Count==0||r.Lines.Any(x=>x.Amount<=0||string.IsNullOrWhiteSpace(x.Description)))return "A payee and one or more positive expense lines are required.";var ids=r.Lines.Select(x=>x.CategoryId).Distinct().ToArray();if(await db.ExpenseCategories.CountAsync(x=>ids.Contains(x.Id)&&x.IsActive,ct)!=ids.Length)return "Select active expense categories.";return null;}
    private static ExpenseVoucherItem Map(ExpenseVoucher x)=>new(x.Id,x.VoucherNumber,x.ExpenseDate,x.Payee,x.PaymentMethodId,x.PaymentMethod?.Name,x.GeneralRemarks,x.Status.ToString(),x.TotalAmount,x.Details.Select(d=>new ExpenseVoucherLineItem(d.Id,d.ExpenseCategoryId,d.Category.Name,d.Description,d.Amount,d.Reference)).ToArray());
}
