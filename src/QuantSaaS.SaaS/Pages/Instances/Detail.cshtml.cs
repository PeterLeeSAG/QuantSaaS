using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QuantSaaS.Infrastructure.Services;
using QuantSaaS.SaaS.ViewModels;

namespace QuantSaaS.SaaS.Pages.Instances;

public class DetailModel : PageModel
{
    private readonly IInstanceService _instances;

    public DetailModel(IInstanceService instances) => _instances = instances;

    public InstanceDetailViewModel Vm { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var dto = await _instances.GetDetailAsync(id);
        if (dto is null) return NotFound();

        Vm = new InstanceDetailViewModel
        {
            Id                      = dto.Id,
            Symbol                  = dto.Symbol,
            AssetClass              = dto.AssetClass,
            BrokerType              = dto.BrokerType,
            Status                  = dto.Status,
            CreatedAt               = dto.CreatedAt,
            LastTickAt              = dto.LastTickAt,
            TotalEquity             = dto.TotalEquity,
            AvailableFunds          = dto.AvailableFunds,
            LongTermHoldingsQty     = dto.LongTermHoldingsQty,
            ActivePositionQty       = dto.ActivePositionQty,
            SealedQty               = dto.SealedQty,
            PendingSettlementAmount = dto.PendingSettlementAmount,
            EquityCurve             = dto.EquityCurve.ToList(),
            EquityLabels            = dto.EquityLabels.ToList(),
            RecentTrades            = dto.RecentTrades.Select(t => new TradeRow
            {
                Symbol       = t.Symbol,
                AssetClass   = t.AssetClass,
                Action       = t.Action,
                FilledQty    = t.FilledQty,
                FilledPrice  = t.FilledPrice,
                Fee          = t.Fee,
                Status       = t.Status,
                FilledAt     = t.FilledAt
            }).ToList(),
            PendingSettlements      = dto.PendingSettlements.Select(s => new PendingSettlementRow
            {
                ClientOrderId      = s.ClientOrderId,
                Amount             = s.Amount,
                SettlementDateUtc  = s.SettlementDateUtc,
                IsSettled          = s.IsSettled
            }).ToList()
        };

        return Page();
    }
}
