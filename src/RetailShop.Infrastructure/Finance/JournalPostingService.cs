using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RetailShop.Application.Common;
using RetailShop.Application.Finance;
using RetailShop.Domain.Finance;
using RetailShop.Infrastructure.Persistence;

namespace RetailShop.Infrastructure.Finance;

internal sealed class JournalPostingService(RetailShopDbContext dbContext, TimeProvider timeProvider)
    : IJournalPostingService
{
    public async Task<OperationResult<JournalItem>> PostAsync(PostJournalRequest request, Guid performedBy,
        CancellationToken ct = default)
    {
        var normalized = Normalize(request);
        if (!normalized.Succeeded) return OperationResult<JournalItem>.Failure(normalized.Errors);
        var lines = normalized.Value!;
        var hash = ComputeHash(request, lines);
        var existing = await FindSourceAsync(request.SourceType, request.SourceId, request.PostingKind, ct);
        if (existing is not null) return RetryResult(existing, hash);

        var period = await dbContext.FiscalPeriods.SingleOrDefaultAsync(x =>
            x.StartDate <= request.BusinessDate && x.EndDate >= request.BusinessDate, ct);
        if (period is null) return OperationResult<JournalItem>.Failure("No fiscal period contains the business date.");
        if (!period.AllowsPosting(request.AllowSoftClosedPeriod))
            return OperationResult<JournalItem>.Failure("The fiscal period does not allow posting.");

        var accountIds = lines.Select(x => x.AccountId).Distinct().ToArray();
        var accounts = await dbContext.Accounts.Where(x => accountIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        foreach (var id in accountIds)
        {
            if (!accounts.TryGetValue(id, out var account))
                return OperationResult<JournalItem>.Failure("A journal account was not found.");
            if (!account.IsActive || account.Kind != AccountKind.Posting)
                return OperationResult<JournalItem>.Failure($"Account {account.Code} is inactive or does not allow posting.");
        }

        var now = timeProvider.GetUtcNow();
        var journal = new JournalEntry($"JRN-{request.BusinessDate:yyyyMMdd}-{Guid.NewGuid():N}"[..23],
            request.BusinessDate, now, request.SourceType.Trim(), request.SourceId,
            request.PostingKind.Trim(), hash, request.Description, performedBy, period.Id, null);
        foreach (var line in lines)
        {
            journal.AddLine(new JournalLine(line.AccountId, line.Debit, line.Credit, line.Description));
            accounts[line.AccountId].MarkPosted();
        }
        dbContext.JournalEntries.Add(journal);
        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            existing = await FindSourceAsync(request.SourceType, request.SourceId, request.PostingKind, ct);
            if (existing is not null) return RetryResult(existing, hash);
            throw;
        }
        return OperationResult<JournalItem>.Success(ToItem(journal, accounts));
    }

    public async Task<OperationResult<JournalItem>> ReverseAsync(Guid journalId, DateOnly businessDate,
        string reason, Guid performedBy, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) return OperationResult<JournalItem>.Failure("A reversal reason is required.");
        var original = await dbContext.JournalEntries.AsNoTracking().Include(x => x.Lines).ThenInclude(x => x.Account)
            .SingleOrDefaultAsync(x => x.Id == journalId, ct);
        if (original is null) return OperationResult<JournalItem>.Failure("Journal was not found.");
        if (await dbContext.JournalEntries.AnyAsync(x => x.ReversesJournalId == journalId, ct))
            return OperationResult<JournalItem>.Failure("The journal has already been reversed.");
        var request = new PostJournalRequest(businessDate, "JournalEntry", journalId,
            FinancePostingKinds.Reversal, reason,
            original.Lines.Select(x => new JournalLineRequest(x.AccountId, x.Credit, x.Debit, x.Description)).ToArray());
        var normalized = Normalize(request);
        if (!normalized.Succeeded) return OperationResult<JournalItem>.Failure(normalized.Errors);
        var lines = normalized.Value!;
        var period = await dbContext.FiscalPeriods.SingleOrDefaultAsync(x => x.StartDate <= businessDate && x.EndDate >= businessDate, ct);
        if (period is null || !period.AllowsPosting(false))
            return OperationResult<JournalItem>.Failure("Reversal requires an open fiscal period.");
        var hash = ComputeHash(request, lines);
        var now = timeProvider.GetUtcNow();
        var reversal = new JournalEntry($"REV-{businessDate:yyyyMMdd}-{Guid.NewGuid():N}"[..23], businessDate,
            now, request.SourceType, journalId, request.PostingKind, hash, reason, performedBy, period.Id, journalId);
        foreach (var line in lines) reversal.AddLine(new JournalLine(line.AccountId, line.Debit, line.Credit, line.Description));
        dbContext.JournalEntries.Add(reversal);
        try { await dbContext.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            if (await dbContext.JournalEntries.AnyAsync(x => x.ReversesJournalId == journalId, ct))
                return OperationResult<JournalItem>.Failure("The journal has already been reversed.");
            throw;
        }
        return OperationResult<JournalItem>.Success(ToItem(reversal,
            original.Lines.Select(x => x.Account).ToDictionary(x => x.Id)));
    }

    private async Task<JournalEntry?> FindSourceAsync(string sourceType, Guid sourceId, string postingKind, CancellationToken ct) =>
        await dbContext.JournalEntries.AsNoTracking().Include(x => x.Lines).ThenInclude(x => x.Account)
            .SingleOrDefaultAsync(x => x.SourceType == sourceType.Trim() && x.SourceId == sourceId && x.PostingKind == postingKind.Trim(), ct);

    private static OperationResult<IReadOnlyCollection<JournalLineRequest>> Normalize(PostJournalRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SourceType) || string.IsNullOrWhiteSpace(request.PostingKind) || request.SourceId == Guid.Empty)
            return OperationResult<IReadOnlyCollection<JournalLineRequest>>.Failure("Source type, source ID and posting kind are required.");
        if (request.SourceType.Trim().Length > 80 || request.PostingKind.Trim().Length > 80 || request.Description?.Trim().Length > 500)
            return OperationResult<IReadOnlyCollection<JournalLineRequest>>.Failure("Journal source, posting kind or description exceeds its allowed length.");
        if (request.Lines is null || request.Lines.Count < 2)
            return OperationResult<IReadOnlyCollection<JournalLineRequest>>.Failure("A journal requires at least two lines.");
        var lines = request.Lines.Select(x => x with { Debit = FinanceAmount.Round(x.Debit), Credit = FinanceAmount.Round(x.Credit) }).ToArray();
        if (lines.Any(x => x.AccountId == Guid.Empty || x.Debit < 0 || x.Credit < 0 ||
            x.Debit == 0 && x.Credit == 0 || x.Debit > 0 && x.Credit > 0 || x.Description?.Trim().Length > 300))
            return OperationResult<IReadOnlyCollection<JournalLineRequest>>.Failure("Each line requires one positive debit or credit amount.");
        if (lines.Sum(x => x.Debit) != lines.Sum(x => x.Credit))
            return OperationResult<IReadOnlyCollection<JournalLineRequest>>.Failure("Journal debits and credits must balance after BDT rounding.");
        return OperationResult<IReadOnlyCollection<JournalLineRequest>>.Success(lines);
    }

    private static string ComputeHash(PostJournalRequest request, IEnumerable<JournalLineRequest> lines)
    {
        var canonical = $"{request.BusinessDate:yyyy-MM-dd}|{request.SourceType.Trim()}|{request.SourceId:N}|{request.PostingKind.Trim()}|{request.Description?.Trim()}|" +
            string.Join(";", lines.OrderBy(x => x.AccountId).ThenBy(x => x.Debit).ThenBy(x => x.Credit)
                .Select(x => $"{x.AccountId:N},{x.Debit:F2},{x.Credit:F2},{x.Description?.Trim()}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static OperationResult<JournalItem> RetryResult(JournalEntry existing, string hash) =>
        existing.PayloadHash == hash
            ? OperationResult<JournalItem>.Success(ToItem(existing, existing.Lines.Select(x => x.Account).ToDictionary(x => x.Id)))
            : OperationResult<JournalItem>.Failure("A conflicting journal already exists for this source.");

    private static JournalItem ToItem(JournalEntry entry, IReadOnlyDictionary<Guid, Account> accounts) =>
        new(entry.Id, entry.Number, entry.BusinessDate, entry.SourceType, entry.SourceId, entry.PostingKind,
            entry.Status, entry.ReversesJournalId, entry.Lines.Select(x => new JournalLineItem(x.Id, x.AccountId,
                accounts[x.AccountId].Code, x.Debit, x.Credit, x.Description)).ToArray());
}
