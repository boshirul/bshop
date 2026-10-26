using RetailShop.Application.Common;
using RetailShop.Domain.Finance;
namespace RetailShop.Application.Finance;
public sealed record LedgerRow(Guid JournalId,DateOnly Date,string JournalNumber,string SourceType,Guid SourceId,string? Description,decimal Debit,decimal Credit,decimal RunningBalance);
public sealed record GeneralLedgerReport(Guid AccountId,string AccountCode,string AccountName,NormalBalance NormalBalance,DateOnly From,DateOnly To,decimal OpeningBalance,decimal ClosingBalance,PagedResult<LedgerRow> Movements,bool Provisional);
public sealed record TrialBalanceRow(Guid AccountId,string Code,string Name,AccountType Type,bool Active,decimal OpeningDebit,decimal OpeningCredit,decimal PeriodDebit,decimal PeriodCredit,decimal ClosingDebit,decimal ClosingCredit);
public sealed record TrialBalanceReport(DateOnly From,DateOnly To,IReadOnlyCollection<TrialBalanceRow> Rows,decimal TotalDebit,decimal TotalCredit,decimal Difference,bool Provisional);
public sealed record StatementRow(Guid AccountId,string Code,string Name,decimal Current,decimal Previous);
public sealed record ProfitLossReport(DateOnly From,DateOnly To,IReadOnlyCollection<StatementRow> Income,IReadOnlyCollection<StatementRow> Expenses,decimal GrossRevenue,decimal Returns,decimal NetSales,decimal CostOfGoodsSold,decimal GrossProfit,decimal OperatingExpenses,decimal NetProfit,bool Provisional);
public sealed record BalanceSheetReport(DateOnly AsOf,IReadOnlyCollection<StatementRow> Assets,IReadOnlyCollection<StatementRow> Liabilities,IReadOnlyCollection<StatementRow> Equity,decimal TotalAssets,decimal TotalLiabilities,decimal TotalEquity,decimal CurrentEarnings,decimal Difference,bool Provisional);
public sealed record CashFlowReport(DateOnly From,DateOnly To,decimal BeginningCash,decimal Operating,decimal Investing,decimal Financing,decimal Unclassified,decimal NetMovement,decimal EndingCash,bool Complete,bool Provisional);
public sealed record FinanceDashboardReport(DateOnly From,DateOnly To,decimal Cash,decimal Bank,decimal Wallet,decimal CardClearing,decimal Receivables,decimal Payables,decimal Inventory,decimal Revenue,decimal Cogs,decimal GrossProfit,decimal Expenses,decimal NetProfit,bool Provisional);
public sealed record FinanceCsvFile(string FileName,string ContentType,byte[] Content);
public interface IFinanceReportingService
{
 Task<OperationResult<GeneralLedgerReport>> GeneralLedgerAsync(Guid accountId,DateOnly from,DateOnly to,int page,int pageSize,CancellationToken ct=default);
 Task<OperationResult<TrialBalanceReport>> TrialBalanceAsync(DateOnly from,DateOnly to,CancellationToken ct=default);
 Task<OperationResult<ProfitLossReport>> ProfitLossAsync(DateOnly from,DateOnly to,CancellationToken ct=default);
 Task<OperationResult<BalanceSheetReport>> BalanceSheetAsync(DateOnly asOf,CancellationToken ct=default);
 Task<OperationResult<CashFlowReport>> CashFlowAsync(DateOnly from,DateOnly to,CancellationToken ct=default);
 Task<OperationResult<FinanceDashboardReport>> DashboardAsync(DateOnly from,DateOnly to,CancellationToken ct=default);
 Task<OperationResult<FinanceCsvFile>> ExportTrialBalanceAsync(DateOnly from,DateOnly to,CancellationToken ct=default);
}
