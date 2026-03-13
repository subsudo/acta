using XHub.Models;

namespace XHub.Services;

public class UserPrefsService
{
    private readonly string _prefsPath;
    private readonly string? _backupPath;

    public UserPrefsService(string prefsPath, string? backupPath)
    {
        _prefsPath = prefsPath;
        _backupPath = backupPath;
    }

    public UserPrefs Load()
    {
        return JsonStorage.Load(_prefsPath, _backupPath, () => new UserPrefs());
    }

    public void Save(UserPrefs prefs)
    {
        JsonStorage.SaveAtomic(_prefsPath, _backupPath, prefs);
    }
}
