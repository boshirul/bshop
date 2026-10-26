using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RetailShop.Api.Infrastructure;
using RetailShop.Application.Finance;
using RetailShop.Application.Security;
using RetailShop.Domain.Finance;
using RetailShop.Shared.Contracts;

namespace RetailShop.Api.Controllers;
[ApiController,Route("api/finance"),Authorize(Policy=Permissions.Finance.View)]
public sealed class FinanceController(IFinanceOperationsService service,IFinanceReportingService reports,IFinanceSetupService setup):ControllerBase
{
 [HttpGet("setup")] public async Task<IActionResult> Setup(CancellationToken ct)=>Ok(ApiResponse<FinanceSetupItem>.Success(await service.SetupAsync(ct)));
 [HttpGet("accounts")] public async Task<IActionResult> Accounts(string? search,CancellationToken ct)=>Ok(ApiResponse<IReadOnlyCollection<FinanceAccountItem>>.Success(await service.AccountsAsync(search,ct)));
 [HttpPost("accounts"),Authorize(Policy=Permissions.Finance.ManageAccounts)] public Task<IActionResult> Account(SaveFinanceAccountRequest r,CancellationToken ct)=>Result(service.SaveAccountAsync(null,r,ct));
 [HttpPut("accounts/{id:guid}"),Authorize(Policy=Permissions.Finance.ManageAccounts)] public Task<IActionResult> Account(Guid id,SaveFinanceAccountRequest r,CancellationToken ct)=>Result(service.SaveAccountAsync(id,r,ct));
 [HttpGet("financial-accounts")] public async Task<IActionResult> Financial(CancellationToken ct)=>Ok(ApiResponse<IReadOnlyCollection<FinancialAccountItem>>.Success(await service.FinancialAccountsAsync(ct)));
 [HttpPost("financial-accounts"),Authorize(Policy=Permissions.Finance.ManageAccounts)] public Task<IActionResult> Financial(SaveFinancialAccountRequest r,CancellationToken ct)=>Result(service.CreateFinancialAccountAsync(r,ct));
 [HttpGet("mappings")] public async Task<IActionResult> Mappings(CancellationToken ct)=>Ok(ApiResponse<IReadOnlyCollection<MappingItem>>.Success(await service.MappingsAsync(ct)));
 [HttpPost("mappings"),Authorize(Policy=Permissions.Finance.ManageAccounts)] public Task<IActionResult> Mapping(SaveMappingRequest r,CancellationToken ct)=>Result(service.CreateMappingAsync(r,ct));
 [HttpGet("journals")] public async Task<IActionResult> Journals(DateOnly? from,DateOnly? to,string? search,string? source,string? kind,Guid? accountId,int page=1,int pageSize=25,CancellationToken ct=default)=>Ok(ApiResponse<object>.Success(await service.JournalsAsync(from,to,search,source,kind,accountId,page,pageSize,ct)));
 [HttpPost("manual-journals"),Authorize(Policy=Permissions.Finance.CreateManualJournal)] public Task<IActionResult> Draft(ManualJournalRequest r,CancellationToken ct)=>Result(service.SaveManualDraftAsync(r,User.GetRequiredUserId(),ct));
 [HttpPost("manual-journals/{id:guid}/post"),Authorize(Policy=Permissions.Finance.PostJournal)] public Task<IActionResult> Post(Guid id,CancellationToken ct)=>Result(service.PostManualAsync(id,User.GetRequiredUserId(),ct));
 [HttpPost("journals/{id:guid}/reverse"),Authorize(Policy=Permissions.Finance.ReverseJournal)] public Task<IActionResult> Reverse(Guid id,ReverseRequest r,CancellationToken ct)=>Result(service.ReverseAsync(id,r.Date,r.Reason,User.GetRequiredUserId(),ct));
 [HttpPost("transfers"),Authorize(Policy=Permissions.Finance.ManageTransfers)] public Task<IActionResult> Transfer(TransferRequest r,CancellationToken ct)=>Result(service.TransferAsync(r,User.GetRequiredUserId(),ct));
 [HttpPost("reconciliations"),Authorize(Policy=Permissions.Finance.ManageReconciliation)] public Task<IActionResult> Reconcile(ReconciliationRequest r,CancellationToken ct)=>Result(service.ReconcileAsync(r,User.GetRequiredUserId(),ct));
 [HttpGet("periods")] public async Task<IActionResult> Periods(CancellationToken ct)=>Ok(ApiResponse<IReadOnlyCollection<FiscalPeriod>>.Success(await service.PeriodsAsync(ct)));
 [HttpPost("periods"),Authorize(Policy=Permissions.Finance.ClosePeriods)] public Task<IActionResult> Period(CreatePeriodRequest r,CancellationToken ct)=>Result(service.CreatePeriodAsync(r.Name,r.StartDate,r.EndDate,ct));
 [HttpPost("periods/{id:guid}/status"),Authorize(Policy=Permissions.Finance.ClosePeriods)] public Task<IActionResult> Period(Guid id,PeriodStatusRequest r,CancellationToken ct)=>Result(service.ChangePeriodAsync(id,r.Status,User.GetRequiredUserId(),ct));
 [HttpGet("reports/general-ledger"),Authorize(Policy=Permissions.Finance.View)] public Task<IActionResult> Ledger(Guid accountId,DateOnly from,DateOnly to,int page=1,int pageSize=50,CancellationToken ct=default)=>Result(reports.GeneralLedgerAsync(accountId,from,to,page,pageSize,ct));
 [HttpGet("reports/trial-balance"),Authorize(Policy=Permissions.Finance.ViewReports)] public Task<IActionResult> TrialBalance(DateOnly from,DateOnly to,CancellationToken ct)=>Result(reports.TrialBalanceAsync(from,to,ct));
 [HttpGet("reports/profit-loss"),Authorize(Policy=Permissions.Finance.ViewReports)] public Task<IActionResult> ProfitLoss(DateOnly from,DateOnly to,CancellationToken ct)=>Result(reports.ProfitLossAsync(from,to,ct));
 [HttpGet("reports/balance-sheet"),Authorize(Policy=Permissions.Finance.ViewReports)] public Task<IActionResult> BalanceSheet(DateOnly asOf,CancellationToken ct)=>Result(reports.BalanceSheetAsync(asOf,ct));
 [HttpGet("reports/cash-flow"),Authorize(Policy=Permissions.Finance.ViewReports)] public Task<IActionResult> CashFlow(DateOnly from,DateOnly to,CancellationToken ct)=>Result(reports.CashFlowAsync(from,to,ct));
 [HttpGet("reports/dashboard"),Authorize(Policy=Permissions.Finance.ViewReports)] public Task<IActionResult> Dashboard(DateOnly from,DateOnly to,CancellationToken ct)=>Result(reports.DashboardAsync(from,to,ct));
 [HttpGet("reports/trial-balance/export"),Authorize(Policy=Permissions.Finance.ViewReports)] public async Task<IActionResult> ExportTrialBalance(DateOnly from,DateOnly to,CancellationToken ct){var r=await reports.ExportTrialBalanceAsync(from,to,ct);return r.Succeeded?File(r.Value!.Content,r.Value.ContentType,r.Value.FileName):BadRequest(ApiResponse<object>.Failure(r.Errors));}
 [HttpGet("setup/opening-assessment"),Authorize(Policy=Permissions.Finance.ManageAccounts)] public async Task<IActionResult> OpeningAssessment(CancellationToken ct)=>Ok(ApiResponse<OpeningPositionItem>.Success(await setup.AssessOpeningAsync(ct)));
 [HttpGet("setup/readiness"),Authorize(Policy=Permissions.Finance.ManageAccounts)] public async Task<IActionResult> Readiness(DateOnly cutover,CancellationToken ct)=>Ok(ApiResponse<FinanceReadinessItem>.Success(await setup.ReadinessAsync(cutover,ct)));
 [HttpPost("opening-batches"),Authorize(Policy=Permissions.Finance.ManageAccounts)] public Task<IActionResult> OpeningBatch(OpeningBatchRequest r,CancellationToken ct)=>Result(setup.SaveOpeningBatchAsync(r,User.GetRequiredUserId(),ct));
 [HttpPost("opening-batches/{id:guid}/approve"),Authorize(Policy=Permissions.Finance.PostJournal)] public Task<IActionResult> ApproveOpening(Guid id,CancellationToken ct)=>Result(setup.ApproveOpeningBatchAsync(id,User.GetRequiredUserId(),ct));
 [HttpPost("opening-batches/{id:guid}/post"),Authorize(Policy=Permissions.Finance.PostJournal)] public Task<IActionResult> PostOpening(Guid id,CancellationToken ct)=>Result(setup.PostOpeningBatchAsync(id,User.GetRequiredUserId(),ct));
 [HttpPost("setup/activate"),Authorize(Policy=Permissions.Finance.PostJournal)] public Task<IActionResult> Activate(ActivateFinanceRequest r,CancellationToken ct)=>Result(setup.ActivateAsync(r.CutoverDate,User.GetRequiredUserId(),ct));
 private static async Task<IActionResult> Result<T>(Task<Application.Common.OperationResult<T>> task){var x=await task;return x.Succeeded?new OkObjectResult(ApiResponse<T>.Success(x.Value!)):new BadRequestObjectResult(ApiResponse<T>.Failure(x.Errors));}
}
public sealed record ReverseRequest(DateOnly Date,string Reason); public sealed record CreatePeriodRequest(string Name,DateOnly StartDate,DateOnly EndDate); public sealed record PeriodStatusRequest(FiscalPeriodStatus Status);
public sealed record ActivateFinanceRequest(DateOnly CutoverDate);
