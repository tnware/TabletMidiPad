namespace TabletMidiPad.Services.Page;

/// <summary>
/// Owns active-page state and page navigation behavior.
/// </summary>
internal interface IPageNavigationService
{
    int ActivePageIndex { get; }

    int? PendingPageIndex { get; }

    void Initialize(AppConfig config);

    PageSelectionResult Select(AppConfig config, int pageIndex);

    PageSelectionResult SelectNext(AppConfig config);

    PageSelectionResult SelectPrevious(AppConfig config);

    PageSelectionResult AddPage(AppConfig config);

    PageSelectionResult RemovePage(AppConfig config, int pageIndex);

    bool TryQueueDeferred(AppConfig config, int pageIndex);

    PageSelectionResult? ConsumeDeferred(AppConfig config);

    PageConfig GetActivePage(AppConfig config);
}
