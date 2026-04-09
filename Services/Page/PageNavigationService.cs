namespace TabletMidiPad.Services.Page;

/// <summary>
/// Default page navigation/state service.
/// </summary>
internal sealed class PageNavigationService : IPageNavigationService
{
    public int ActivePageIndex { get; private set; }

    public int? PendingPageIndex { get; private set; }

    public void Initialize(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        EnsurePages(config);
        ActivePageIndex = 0;
        PendingPageIndex = null;
    }

    public PageSelectionResult Select(AppConfig config, int pageIndex)
    {
        EnsurePages(config);
        int safeIndex = Math.Clamp(pageIndex, 0, config.Pages.Count - 1);
        bool changed = safeIndex != ActivePageIndex;
        ActivePageIndex = safeIndex;
        PendingPageIndex = null;
        return new PageSelectionResult(changed, ActivePageIndex, changed
            ? $"Switched to {GetActivePage(config).Name}."
            : string.Empty);
    }

    public PageSelectionResult SelectNext(AppConfig config)
    {
        EnsurePages(config);
        int count = config.Pages.Count;
        int target = ActivePageIndex + 1;
        if (target >= count)
        {
            target = config.WrapPageNavigation ? 0 : ActivePageIndex;
        }

        return Select(config, target);
    }

    public PageSelectionResult SelectPrevious(AppConfig config)
    {
        EnsurePages(config);
        int target = ActivePageIndex - 1;
        if (target < 0)
        {
            target = config.WrapPageNavigation ? config.Pages.Count - 1 : ActivePageIndex;
        }

        return Select(config, target);
    }

    public PageSelectionResult AddPage(AppConfig config)
    {
        EnsurePages(config);
        int pageNumber = config.Pages.Count + 1;
        config.Pages.Add(PageConfig.CreateDefault(pageNumber));
        return Select(config, config.Pages.Count - 1) with
        {
            StatusMessage = "Page added. Click Apply Settings to save."
        };
    }

    public PageSelectionResult RemovePage(AppConfig config, int pageIndex)
    {
        EnsurePages(config);
        if (config.Pages.Count <= 1 || pageIndex < 0 || pageIndex >= config.Pages.Count)
        {
            return new PageSelectionResult(false, ActivePageIndex, string.Empty);
        }

        config.Pages.RemoveAt(pageIndex);
        int safe = Math.Clamp(ActivePageIndex, 0, config.Pages.Count - 1);
        if (pageIndex < ActivePageIndex)
        {
            safe = Math.Max(0, safe - 1);
        }

        Select(config, safe);
        return new PageSelectionResult(true, ActivePageIndex, "Page removed. Click Apply Settings to save.");
    }

    public bool TryQueueDeferred(AppConfig config, int pageIndex)
    {
        EnsurePages(config);
        int safe = Math.Clamp(pageIndex, 0, config.Pages.Count - 1);
        if (safe == ActivePageIndex)
        {
            PendingPageIndex = null;
            return false;
        }

        PendingPageIndex = safe;
        return true;
    }

    public PageSelectionResult? ConsumeDeferred(AppConfig config)
    {
        if (PendingPageIndex is not int pending)
        {
            return null;
        }

        PendingPageIndex = null;
        return Select(config, pending);
    }

    public PageConfig GetActivePage(AppConfig config)
    {
        EnsurePages(config);
        ActivePageIndex = Math.Clamp(ActivePageIndex, 0, config.Pages.Count - 1);
        return AppConfig.GetPage(config, ActivePageIndex);
    }

    private static void EnsurePages(AppConfig config)
    {
        config.Pages ??= new();
        if (config.Pages.Count == 0)
        {
            config.Pages.Add(PageConfig.CreateDefault(1));
        }
    }
}
