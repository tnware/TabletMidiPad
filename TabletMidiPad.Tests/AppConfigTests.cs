using System.Text.Json;
using Xunit;

namespace TabletMidiPad.Tests;

public sealed class AppConfigTests
{
    [Fact]
    public void LegacyPenButtonFields_MigrateToUpperPenButton()
    {
        var json = JsonSerializer.Serialize(new AppConfig
        {
            UpperPenButtonAction = "",
            PenButtonAction = PenButtonActions.CcToggle,
            PenButtonMidiChannel = 3,
            PenButtonCc = 75,
            PenButtonNote = 40,
            Zones = new List<ZoneConfig> { ZoneConfig.CreateDefault() }
        });

        AppConfig cfg = DeserializeAndNormalize(json);

        Assert.Equal(PenButtonActions.CcToggle, cfg.UpperPenButtonAction);
        Assert.Equal(3, cfg.UpperPenButtonMidiChannel);
    }

    [Fact]
    public void LegacyExpressKeyFields_MigrateToExpressKeyList()
    {
        var json = JsonSerializer.Serialize(new AppConfig
        {
            ExpressKeys = new List<ExpressKeyConfig>(),
            ExpressKeyAction = PenButtonActions.NoteMomentary,
            ExpressKeyMidiChannel = 2,
            ExpressKeyBaseCc = 50,
            ExpressKeyBaseNote = 30,
            Zones = new List<ZoneConfig> { ZoneConfig.CreateDefault() }
        });

        AppConfig cfg = DeserializeAndNormalize(json);

        Assert.Equal(8, cfg.ExpressKeys.Count);
        Assert.Equal(PenButtonActions.NoteMomentary, cfg.ExpressKeys[0].Action);
    }

    [Fact]
    public void EmptyZones_DefaultsToSingleZone()
    {
        var json = JsonSerializer.Serialize(new AppConfig { Zones = new List<ZoneConfig>() });

        AppConfig cfg = DeserializeAndNormalize(json);

        Assert.NotNull(cfg.Zones);
    }

    [Fact]
    public void LegacyReleaseBehavior_MigratesFromResetPressureFlag()
    {
        var zone = ZoneConfig.CreateDefault();
        zone.ReleaseBehavior = "";
        zone.ResetPressureOnRelease = false;

        var json = JsonSerializer.Serialize(new AppConfig { Zones = new List<ZoneConfig> { zone } });
        AppConfig cfg = DeserializeAndNormalize(json);

        string behavior = string.IsNullOrWhiteSpace(cfg.Zones[0].ReleaseBehavior)
            ? (cfg.Zones[0].ResetPressureOnRelease ? ZoneReleaseBehaviors.ResetPressureOnly : ZoneReleaseBehaviors.NoSendOnRelease)
            : cfg.Zones[0].ReleaseBehavior;

        Assert.Equal(ZoneReleaseBehaviors.NoSendOnRelease, behavior);
    }

    private static AppConfig DeserializeAndNormalize(string json)
    {
        var cfg = JsonSerializer.Deserialize<AppConfig>(json)!;
        cfg.Zones ??= new List<ZoneConfig> { ZoneConfig.CreateDefault() };

        if (cfg.Zones.Count == 0)
        {
            cfg.Zones.Add(ZoneConfig.CreateDefault());
        }

        if (string.IsNullOrWhiteSpace(cfg.UpperPenButtonAction) && !string.IsNullOrWhiteSpace(cfg.PenButtonAction))
        {
            cfg.UpperPenButtonAction = cfg.PenButtonAction;
            cfg.UpperPenButtonMidiChannel = cfg.PenButtonMidiChannel;
            cfg.UpperPenButtonCc = cfg.PenButtonCc;
            cfg.UpperPenButtonNote = cfg.PenButtonNote;
        }

        cfg.ExpressKeys ??= new List<ExpressKeyConfig>();
        if (cfg.ExpressKeys.Count == 0 && !string.IsNullOrWhiteSpace(cfg.ExpressKeyAction))
        {
            for (int i = 0; i < 8; i++)
            {
                cfg.ExpressKeys.Add(new ExpressKeyConfig
                {
                    Action = cfg.ExpressKeyAction,
                    MidiChannel = cfg.ExpressKeyMidiChannel,
                    Cc = Math.Clamp(cfg.ExpressKeyBaseCc + i, 0, 127),
                    Note = Math.Clamp(cfg.ExpressKeyBaseNote + i, 0, 127)
                });
            }
        }

        foreach (ZoneConfig zone in cfg.Zones)
        {
            if (string.IsNullOrWhiteSpace(zone.ReleaseBehavior))
            {
                zone.ReleaseBehavior = zone.ResetPressureOnRelease
                    ? ZoneReleaseBehaviors.ResetPressureOnly
                    : ZoneReleaseBehaviors.NoSendOnRelease;
            }
        }

        return cfg;
    }
}
