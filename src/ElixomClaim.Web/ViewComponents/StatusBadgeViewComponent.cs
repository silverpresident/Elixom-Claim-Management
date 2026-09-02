using Microsoft.AspNetCore.Mvc;

namespace ElixomClaim.Web.ViewComponents;

public class StatusBadgeViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(string status, string? label = null)
    {
        var normalized = status?.Trim() ?? "Unknown";
        var badgeCss = normalized.ToLowerInvariant() switch
        {
            "draft" => "status-badge-draft",
            "submitted" => "status-badge-submitted",
            "accepted" => "status-badge-accepted",
            "rejected" => "status-badge-rejected",
            "processing" => "status-badge-processing",
            "collected" => "status-badge-collected",
            "scheduled" => "status-badge-scheduled",
            "paid" => "status-badge-paid",
            "honoured" => "status-badge-honoured",
            "transferred" => "status-badge-transferred",
            _ => "bg-secondary text-white"
        };

        var displayLabel = string.IsNullOrWhiteSpace(label) ? normalized : label;

        return View(new StatusBadgeViewModel
        {
            Status = normalized,
            Label = displayLabel,
            CssClass = badgeCss
        });
    }
}

public class StatusBadgeViewModel
{
    public required string Status { get; set; }
    public required string Label { get; set; }
    public required string CssClass { get; set; }
}
