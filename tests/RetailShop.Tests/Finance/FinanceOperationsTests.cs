using RetailShop.Domain.Finance;
namespace RetailShop.Tests.Finance;
public sealed class FinanceOperationsTests
{
 [Fact] public void Transfer_rejects_same_account_and_invalid_amount(){var id=Guid.NewGuid();Assert.Throws<ArgumentException>(()=>new FundTransfer(id,id,10,0,null,new DateOnly(2026,1,1),null,null,Guid.NewGuid()));Assert.Throws<ArgumentException>(()=>new FundTransfer(Guid.NewGuid(),Guid.NewGuid(),0,0,null,new DateOnly(2026,1,1),null,null,Guid.NewGuid()));}
 [Fact] public void Completed_transfer_is_immutable(){var x=new FundTransfer(Guid.NewGuid(),Guid.NewGuid(),10,0,null,new DateOnly(2026,1,1),null,null,Guid.NewGuid());x.MarkPosted(Guid.NewGuid());Assert.Throws<InvalidOperationException>(()=>x.MarkPosted(Guid.NewGuid()));}
 [Fact] public void Reconciliation_requires_zero_difference_to_complete(){var x=new FinancialReconciliation(Guid.NewGuid(),new DateOnly(2026,1,1),0,10,9,Guid.NewGuid());Assert.Throws<InvalidOperationException>(()=>x.Complete(Guid.NewGuid()));}
 [Fact] public void Balanced_reconciliation_can_complete(){var x=new FinancialReconciliation(Guid.NewGuid(),new DateOnly(2026,1,1),0,10,10,Guid.NewGuid());x.Complete(Guid.NewGuid());Assert.Equal(ReconciliationStatus.Completed,x.Status);}
 [Fact] public void Posted_manual_draft_cannot_be_changed(){var x=new ManualJournalDraft(new DateOnly(2026,1,1),"Adjustment",null,Guid.NewGuid());x.ReplaceLines([new(Guid.NewGuid(),10,0),new(Guid.NewGuid(),0,10)]);x.MarkPosted(Guid.NewGuid());Assert.Throws<InvalidOperationException>(()=>x.ReplaceLines([]));}
 [Fact] public void Opening_batch_requires_balanced_positive_lines_before_approval(){var x=new OpeningBalanceBatch(new(2026,1,1),"Opening",Guid.NewGuid());x.ReplaceLines([new(Guid.NewGuid(),10,0)]);Assert.Throws<InvalidOperationException>(()=>x.Approve(Guid.NewGuid()));}
 [Fact] public void Finance_activation_requires_all_readiness_flags(){var x=new FinanceActivation();Assert.Throws<InvalidOperationException>(()=>x.Activate(new(2026,1,1),Guid.NewGuid()));x.RecordReadiness(true,true,true);x.Activate(new(2026,1,1),Guid.NewGuid());Assert.True(x.AutomaticPostingEnabled);}
}
