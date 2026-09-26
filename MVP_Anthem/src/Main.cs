using Cookies.Contract;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.GameEvents;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using T3Menu.Contract;
using Volume.Contract;

namespace MVP_Anthem;

[PluginMetadata(
    Id = "MVP_Anthem",
    Name = "MVP Anthem",
    Version = "1.2.0",
    Description = "MVP Plugin with fully customizable config"
)]
public sealed class Main(ISwiftlyCore core) : BasePlugin(core)
{
    private const string PlayerCookiesInterfaceKey = "Cookies.Player.v1";
    private const string PlayerCookiesInterfaceKeyLegacy = "Cookies.Player.V1";
    private const string VolumeInterfaceKey = "Volume.Player.v1";
    private const string VolumeFeatureKey = "MVP_Anthem";

    private ServiceProvider? _provider;
    public static new ISwiftlyCore Core { get; set; } = null!;
    private IPlayerCookiesAPIv1? Cookies { get; set; }
    private IPlayerVolumeAPI? VolumeApi { get; set; }
    private MVPConfig Config { get; set; } = new MVPConfig();
    private MVPCookies? mvpCookies { get; set; }
    private IMvpMenu? Menu { get; set; }
    private IT3Menu? _t3Menu;
    private bool _loaded;
    private IPlayerCookiesAPIv1? _runtimeCookies;
    private IPlayerVolumeAPI? _runtimeVolume;
    private IT3Menu? _runtimeMenu;
    private List<Guid> _commandIds { get; } = [];

    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
        Cookies = ResolveSharedInterface<IPlayerCookiesAPIv1>(
            interfaceManager,
            [PlayerCookiesInterfaceKey, PlayerCookiesInterfaceKeyLegacy]
        );
        var volumeApi = ResolveSharedInterface<IPlayerVolumeAPI>(interfaceManager, [VolumeInterfaceKey]);
        if (!ReferenceEquals(VolumeApi, volumeApi))
        {
            UnregisterVolumeFeature();
            VolumeApi = volumeApi;
            RegisterVolumeFeature();
        }
        _t3Menu = ResolveSharedInterface<IT3Menu>(interfaceManager, [IT3Menu.Key]);
        InitializeRuntimeIfReady();
    }

    public override void OnSharedInterfaceInjected(IInterfaceManager interfaceManager)
    {
        UseSharedInterface(interfaceManager);
    }

    public override void Load(bool hotReload)
    {
        Core = base.Core;

        Core.Configuration.InitializeJsonWithModel<MVPConfig>("config.jsonc", "Main")
            .Configure(builder =>
            {
                builder.AddJsonFile("config.jsonc", false, true);
            });

        ServiceCollection services = new();
        services.AddSwiftly(Core)
                .AddOptionsWithValidateOnStart<MVPConfig>().BindConfiguration("Main");

        _provider = services.BuildServiceProvider();
        Config = _provider.GetRequiredService<IOptions<MVPConfig>>().Value;
        _loaded = true;
        Core.Event.OnClientDisconnected += OnClientDisconnected;
        Core.Event.OnMapUnload += OnMapUnload;
        Core.Event.OnPrecacheResource += OnPrecacheResource;
        InitializeRuntimeIfReady();
    }

    private void InitializeRuntimeIfReady()
    {
        if (!_loaded) return;
        if (Menu != null && ReferenceEquals(Cookies, _runtimeCookies)
            && ReferenceEquals(VolumeApi, _runtimeVolume) && ReferenceEquals(_t3Menu, _runtimeMenu)) return;
        _runtimeCookies = Cookies;
        _runtimeVolume = VolumeApi;
        _runtimeMenu = _t3Menu;
        Menu?.Dispose();
        Menu = null;
        UnregisterConfiguredCommands();
        if (Cookies is null)
        {
            mvpCookies = null;
            return;
        }

        mvpCookies = new MVPCookies(Cookies);
        Menu = string.Equals(Config.Settings.MenuType, "t3", StringComparison.OrdinalIgnoreCase) && _t3Menu != null
            ? new T3MvpMenu(Core, Config, mvpCookies, _t3Menu, GetPlayerVolume)
            : new CoreMvpMenu(Core, Config, mvpCookies, GetPlayerVolume);
        if (string.Equals(Config.Settings.MenuType, "t3", StringComparison.OrdinalIgnoreCase) && _t3Menu == null)
            Core.Logger.LogWarning("T3Menu is unavailable; MVP Anthem is using the Core menu.");
        RegisterConfiguredCommands();
        foreach (var player in Core.PlayerManager.GetAllValidPlayers())
            InitializePlayer(player);
    }

    private void RegisterConfiguredCommands()
    {
        foreach (var cmd in Config.Settings.MVPCommands.Select(command => command?.Trim() ?? "").Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(cmd))
                continue;

            if (!Core.Command.IsCommandRegistered(cmd))
            {
                var guid = Core.Command.RegisterCommand(cmd, OnMvpCommand);
                _commandIds.Add(guid);
            }
        }
    }

    private void UnregisterConfiguredCommands()
    {
        foreach (var commandGuid in _commandIds)
        {
            Core.Command.UnregisterCommand(commandGuid);
        }
        _commandIds.Clear();
    }

    private void RegisterVolumeFeature()
    {
        if (VolumeApi == null) return;
        try
        {
            VolumeApi.RegisterFeature(VolumeFeatureKey, "MVP Anthem");
        }
        catch (Exception ex)
        {
            Core.Logger.LogError(ex, "Could not register MVP Anthem with Volume.API.");
            VolumeApi = null;
        }
    }

    private void UnregisterVolumeFeature()
    {
        if (VolumeApi == null) return;
        try
        {
            VolumeApi.UnregisterFeature(VolumeFeatureKey);
        }
        catch (Exception ex)
        {
            Core.Logger.LogError(ex, "Could not unregister MVP Anthem from Volume.API.");
        }
    }

    private float GetPlayerVolume(IPlayer player)
    {
        if (VolumeApi == null) return Helper.ClampVolume(Config.Settings.DefaultVolume);
        try
        {
            return Helper.ClampVolume(VolumeApi.GetEffectiveVolume((long)player.SteamID, VolumeFeatureKey));
        }
        catch (Exception ex)
        {
            Core.Logger.LogError(ex, "Could not read MVP Anthem volume for {SteamId}.", player.SteamID);
            return Helper.ClampVolume(Config.Settings.DefaultVolume);
        }
    }

    private T? ResolveSharedInterface<T>(IInterfaceManager interfaceManager, IEnumerable<string> keys) where T : class
    {
        foreach (var key in keys.Distinct(StringComparer.Ordinal))
        {
            if (!interfaceManager.HasSharedInterface(key))
                continue;

            try
            {
                return interfaceManager.GetSharedInterface<T>(key);
            }
            catch
            {
                // Continue trying fallback keys.
            }
        }

        return null;
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult OnPlayerConnectFull(EventPlayerConnectFull e)
    {
        if (e.UserIdPlayer is { IsValid: true } player) InitializePlayer(player);
        return HookResult.Continue;
    }

    private void InitializePlayer(IPlayer player)
    {
        if (mvpCookies == null || !player.IsValid || player.IsFakeClient) return;
        var settings = mvpCookies.GetPlayerSettings(player);
        if (settings == null)
            return;

        settings.Player = player;
        var shouldSave = false;

        if (!settings.HadFirstConnect)
        {
            settings.HadFirstConnect = true;
            shouldSave = true;

            if (Config.Settings.GiveRandomMVPOnFirstJoin)
            {
                var randomMvp = Helper.GetRandomMvpForPlayer(Core, Config, player);
                if (randomMvp.HasValue)
                {
                    settings.MVPName = randomMvp.Value.MvpName;
                    settings.SoundPath = randomMvp.Value.SoundPath;
                }
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(settings.SoundPath) && !string.IsNullOrWhiteSpace(settings.MVPName))
            {
                var mappedSoundPath = Helper.GetSoundPath(Config, settings.MVPName);
                if (!string.IsNullOrWhiteSpace(mappedSoundPath))
                {
                    settings.SoundPath = mappedSoundPath;
                    shouldSave = true;
                }
            }
        }

        if (shouldSave)
        {
            mvpCookies.SavePlayerSettings(settings);
        }

        return;
    }
    [GameEventHandler(HookMode.Pre)]
    public HookResult OnRoundMvp(EventRoundMvp e)
    {
        if (mvpCookies is null)
            return HookResult.Continue;

        if (e.UserIdPlayer is not IPlayer mvpPlayer)
            return HookResult.Continue;

        if (Config.Settings.RemovePlayerInGameMvp)
        {
            Helper.RemoveInGameMvp(mvpPlayer);
        }

        var settings = mvpCookies.GetPlayerSettings(mvpPlayer);
        if (settings == null)
            return HookResult.Continue;

        settings.Player = mvpPlayer;

        string mvpName;
        string soundPath;
        MVP_Template mvpTemplate;
        var shouldSaveSettings = false;

        if (settings.HasRandomMvp)
        {
            var randomMvp = Helper.GetRandomMvpForPlayer(Core, Config, mvpPlayer);
            if (!randomMvp.HasValue)
                return HookResult.Continue;

            mvpName = randomMvp.Value.MvpName;
            soundPath = randomMvp.Value.SoundPath;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(settings.MVPName))
                return HookResult.Continue;

            mvpName = settings.MVPName;
            soundPath = Helper.GetSoundPath(Config, mvpName);
        }

        if (!Helper.TryGetMvpTemplate(Config, mvpName, out mvpTemplate))
            return HookResult.Continue;
        if (!Helper.PlayerHasAccessToMvp(Core, mvpPlayer, mvpTemplate))
            return HookResult.Continue;

        if (string.IsNullOrWhiteSpace(soundPath))
        {
            soundPath = mvpTemplate.Sound ?? string.Empty;
            if (!settings.HasRandomMvp && !string.IsNullOrWhiteSpace(soundPath))
            {
                settings.SoundPath = soundPath;
                shouldSaveSettings = true;
            }
        }

        if (shouldSaveSettings)
        {
            mvpCookies.SavePlayerSettings(settings);
        }

        var listeners = Core.PlayerManager.GetAllValidPlayers().Where(player => !player.IsFakeClient).ToArray();
        if (listeners.Length == 0)
            return HookResult.Continue;

        if (!string.IsNullOrWhiteSpace(soundPath))
        {
            var listenerVolumes = listeners
                .Select(listener => (Player: listener, Volume: GetPlayerVolume(listener)))
                .ToArray();

            Helper.PlaySound(listenerVolumes, soundPath);
        }

        var mvpPlayerName = Helper.GetPlayerName(mvpPlayer);
        var chatKey = $"{mvpName}.chat";
        var htmlKey = $"{mvpName}.html";

        if (mvpTemplate.ShowChat)
        {
            foreach (var listener in listeners)
            {
                var localizer = Core.Translation.GetPlayerLocalizer(listener);
                var prefix = localizer["prefix"];
                var displayName = localizer[mvpTemplate.DisplayName];
                var chatText = localizer[chatKey, mvpPlayerName, displayName];
                listener.SendChat($"{prefix} {chatText}");
            }
        }

        if (mvpTemplate.ShowHtml)
        {
            var htmlDurationSeconds = Math.Max(0, Config.Settings.MVPMaxDuration);
            if (htmlDurationSeconds > 0)
            {
                foreach (var listener in listeners)
                {
                    var localizer = Core.Translation.GetPlayerLocalizer(listener);
                    var displayName = localizer[mvpTemplate.DisplayName];
                    var htmlText = localizer[htmlKey, System.Net.WebUtility.HtmlEncode(mvpPlayerName), System.Net.WebUtility.HtmlEncode(displayName)];
                    Helper.SendHTML(listener, htmlText, htmlDurationSeconds);
                }
            }
        }

        return HookResult.Continue;
    }

    public override void Unload()
    {
        _loaded = false;
        UnregisterConfiguredCommands();
        UnregisterVolumeFeature();
        VolumeApi = null;
        Core.Event.OnClientDisconnected -= OnClientDisconnected;
        Core.Event.OnMapUnload -= OnMapUnload;
        Core.Event.OnPrecacheResource -= OnPrecacheResource;
        Menu?.Dispose();
        Menu = null;
        Helper.Reset();
        mvpCookies = null;
        _provider?.Dispose();
        _provider = null;
    }

    private void OnClientDisconnected(IOnClientDisconnectedEvent e)
    {
        Menu?.RemovePlayer(e.PlayerId);
        mvpCookies?.RemovePlayer(e.PlayerId);
        Helper.RemoveHtmlMessage(e.PlayerId);
    }

    private void OnMapUnload(IOnMapUnloadEvent e)
    {
        foreach (var player in Core.PlayerManager.GetAllValidPlayers()) Menu?.RemovePlayer(player.PlayerID);
        Helper.Reset();
    }

    private void OnPrecacheResource(IOnPrecacheResourceEvent e)
    {
        foreach (var resource in Config.Settings.SoundEventFiles.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct())
            e.AddItem(resource);
    }

    private void OnMvpCommand(ICommandContext context)
    {
        if (!context.IsSentByPlayer || context.Sender is not IPlayer player || !player.IsValid)
        {
            context.Reply("This command can only be used by a player.");
            return;
        }

        InitializePlayer(player);
        if (Menu is null)
        {
            context.Reply("MVP menu is unavailable: required shared interfaces are not ready.");
            return;
        }

        Menu.OpenMainMenu(player);
    }
    [EventListener<EventDelegates.OnTick>]
    public void OnTick()
    {
        Helper.RenderHtmlMessages();
    }
}
