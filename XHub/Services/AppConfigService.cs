using XHub.Models;

namespace XHub.Services;

public class AppConfigService
{
    private readonly string _settingsPath;
    private readonly string? _backupPath;
    private readonly AppConfig _defaults;

    public AppConfigService(string settingsPath, string? backupPath, AppConfig defaults)
    {
        _settingsPath = settingsPath;
        _backupPath = backupPath;
        _defaults = defaults;
    }

    public AppConfig Load()
    {
        return JsonStorage.Load(_settingsPath, _backupPath, () => Clone(_defaults));
    }

    public void Save(AppConfig config)
    {
        JsonStorage.SaveAtomic(_settingsPath, _backupPath, config);
    }

    private static AppConfig Clone(AppConfig source)
    {
        return new AppConfig
        {
            ServerBasePath = source.ServerBasePath,
            UseSecondaryServerBasePath = source.UseSecondaryServerBasePath,
            SecondaryServerBasePath = source.SecondaryServerBasePath,
            LvBasePath = source.LvBasePath,
            LbBasePath = source.LbBasePath,
            StartBasePath = source.StartBasePath,
            ExitBasePath = source.ExitBasePath,
            ParticipantHintsStorePath = source.ParticipantHintsStorePath,
            VerlaufsakteKeyword = source.VerlaufsakteKeyword,
            WordBuBookmarkName = source.WordBuBookmarkName,
            WordBiBookmarkName = source.WordBiBookmarkName,
            WordBeBookmarkName = source.WordBeBookmarkName,
            WordLbBookmarkName = source.WordLbBookmarkName,
            VisibleQuickActions = source.VisibleQuickActions.ToList(),
            AutoRefreshHours = source.AutoRefreshHours,
            ShowStatusTags = source.ShowStatusTags,
            ShowParticipantPhoto = source.ShowParticipantPhoto,
            ScheduleRootPath = source.ScheduleRootPath
        };
    }
}
