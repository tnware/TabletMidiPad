using Xunit;

namespace TabletMidiPad.Tests;

public sealed class PageNavigationServiceTests
{
    [Fact]
    public void AddPageUsesFirstAvailableDefaultName()
    {
        var config = new AppConfig
        {
            Pages =
            [
                new PageConfig { Name = "Page 1" },
                new PageConfig { Name = "Page 3" },
            ],
        };
        var sut = new PageNavigationService();
        sut.Initialize(config);

        PageSelectionResult result = sut.AddPage(config);

        Assert.True(result.Changed);
        Assert.Equal(3, config.Pages.Count);
        Assert.Equal("Page 2", config.Pages[2].Name);
        Assert.Equal(2, sut.ActivePageIndex);
    }

    [Fact]
    public void RemovePageWhenOnlyOnePageReturnsUnchangedWithMessage()
    {
        var config = new AppConfig
        {
            Pages = [new PageConfig { Name = "Page 1" }],
        };
        var sut = new PageNavigationService();
        sut.Initialize(config);

        PageSelectionResult result = sut.RemovePage(config, pageIndex: 0);

        Assert.False(result.Changed);
        Assert.Single(config.Pages);
        Assert.Contains("last page", result.StatusMessage);
    }
}
