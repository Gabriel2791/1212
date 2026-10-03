
using Cookies.Contract;
using SwiftlyS2.Shared.Players;

namespace MVP_Anthem;

public sealed class MVPCookies
{
    private const string MVPNameKey = "mvp_anthem.mvp_name";
    private const string SoundPathKey = "mvp_anthem.sound_path";
    private const string HadFirstConnectKey = "mvp_anthem.had_first_connect";
    private const string HasRandomMvpKey = "mvp_anthem.has_random_mvp";

    private readonly IPlayerCookiesAPIv1 Cookies;
    private readonly Dictionary<int, PlayerSettings> _cache = [];

    public void RemovePlayer(int playerId) => _cache.Remove(playerId);

    public MVPCookies(IPlayerCookiesAPIv1 cookies)
    {
        Cookies = cookies;
    }

    public void SavePlayerSettings(PlayerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(settings.Player);

        Cookies.Set(settings.Player, MVPNameKey, settings.MVPName);
        Cookies.Set(settings.Player, SoundPathKey, settings.SoundPath);
        Cookies.Set(settings.Player, HadFirstConnectKey, settings.HadFirstConnect);
        Cookies.Set(settings.Player, HasRandomMvpKey, settings.HasRandomMvp);
        Cookies.Save(settings.Player);
    }

    /// <summary>
    /// Writes a new MVP selection for an ONLINE player through the same cache the rest of
    /// the plugin reads from, so an external source (panel/RCON) never races the Cookies
    /// plugin's own in-memory state. This is the only safe way to update a player's MVP
    /// cookie from outside the plugin: the Cookies API only accepts a live IPlayer handle
    /// (no offline/steamid write path), so a direct database write bypasses this cache and
    /// gets silently overwritten the next time MVPCookies persists from memory (e.g. on
    /// disconnect or a later selection) or simply never shows up in-game because the cached
    /// PlayerSettings object the plugin is holding was never told about it.
    /// </summary>
    /// <returns>false if the player is not currently connected/valid.</returns>
    public bool TryUpdateMvpForOnlinePlayer(IPlayer player, string mvpName, string soundPath, bool hasRandomMvp = false)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!player.IsValid || player.IsFakeClient) return false;

        // GetPlayerSettings returns (and, if needed, first populates) the exact cached
        // instance InitializePlayer/OnRoundMvp read from, keyed by PlayerID+SessionId.
        var settings = GetPlayerSettings(player);
        if (settings == null) return false;

        settings.MVPName = mvpName ?? string.Empty;
        settings.SoundPath = soundPath ?? string.Empty;
        settings.HasRandomMvp = hasRandomMvp;
        settings.HadFirstConnect = true;

        SavePlayerSettings(settings);
        return true;
    }

    public PlayerSettings? GetPlayerSettings(IPlayer player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!player.IsValid || player.IsFakeClient) return null;
        if (_cache.TryGetValue(player.PlayerID, out var cached) && cached.SessionId == player.SessionId)
        {
            cached.Player = player;
            return cached;
        }
        Cookies.Load(player);

        return _cache[player.PlayerID] = new PlayerSettings
        {
            Player = player,
            SessionId = player.SessionId,
            MVPName = Cookies.GetOrDefault(player, MVPNameKey, string.Empty) ?? string.Empty,
            SoundPath = Cookies.GetOrDefault(player, SoundPathKey, string.Empty) ?? string.Empty,
            HasRandomMvp = Cookies.GetOrDefault(player, HasRandomMvpKey, false),
            HadFirstConnect = Cookies.GetOrDefault(player, HadFirstConnectKey, false)
        };
    }

    public sealed class PlayerSettings
    {
        public IPlayer Player { get; set; } = null!;
        public ulong SessionId { get; init; }
        public string MVPName { get; set; } = string.Empty;
        public string SoundPath { get; set; } = string.Empty;
        public bool HadFirstConnect { get; set; }
        public bool HasRandomMvp { get; set; }
    }
}
