
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
