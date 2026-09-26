using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

namespace MVP_Anthem;

public interface IMvpMenu : IDisposable
{
    void OpenMainMenu(IPlayer player);
    void RemovePlayer(int playerId);
}

internal sealed record MvpMenuPage(string Title, List<MvpMenuEntry> Entries);
internal sealed record MvpMenuEntry(string Text, Action<IPlayer>? Action = null,
    Func<IPlayer, MvpMenuPage>? Submenu = null, bool Disabled = false, bool KeepOpen = false);

public abstract class MvpMenuBase(ISwiftlyCore core, MVPConfig config, MVPCookies cookies,
    Func<IPlayer, float> getVolume) : IMvpMenu
{
    protected ISwiftlyCore Core { get; } = core;
    protected MVPConfig Config { get; } = config;
    private bool _disposed;

    public void OpenMainMenu(IPlayer player)
    {
        if (!_disposed && player.IsValid && !player.IsFakeClient)
            Open(player, BuildMain(player));
    }

    private protected abstract void Open(IPlayer player, MvpMenuPage page);
    protected bool IsActive => !_disposed;
    public virtual void Dispose() => _disposed = true;
    public virtual void RemovePlayer(int playerId) { }

    private string Text(IPlayer player, string key, params object[] args) =>
        Core.Translation.GetPlayerLocalizer(player)[key, args];

    private MvpMenuPage BuildMain(IPlayer player)
    {
        var settings = cookies.GetPlayerSettings(player)!;
        var current = Text(player, "mvp.none");
        if (settings.HasRandomMvp) current = Text(player, "mvp.random<option>");
        else if (TryAccessible(player, settings.MVPName, out var template)) current = Text(player, template.DisplayName);
        var entries = new List<MvpMenuEntry>
        {
            new(Text(player, "mvp.main_menu.active_mvp<option>", current), Disabled: true),
            new(Text(player, "mvp.main_menu.select_mvp<option>"), Submenu: BuildSelection)
        };
        if (settings.HasRandomMvp || !string.IsNullOrWhiteSpace(settings.MVPName))
            entries.Add(new(Text(player, "mvp.main_menu.remove_mvp<option>"), Submenu: BuildRemoval));
        return new(Text(player, "mvp.main_menu<title>"), entries);
    }

    private MvpMenuPage BuildSelection(IPlayer player)
    {
        var entries = new List<MvpMenuEntry>();
        foreach (var (category, mvps) in Config.MVPs)
        {
            if (!mvps.Values.Any(mvp => Helper.PlayerHasAccessToMvp(Core, player, mvp))) continue;
            entries.Add(new(Text(player, category), Submenu: p => BuildCategory(p, category)));
        }
        entries.Insert(0, new(Text(player, "mvp.random<option>"), p =>
        {
            var selected = Helper.GetRandomMvpForPlayer(Core, Config, p);
            if (selected is not { } choice) return;
            Change(p, s => { s.HasRandomMvp = true; s.MVPName = choice.MvpName; s.SoundPath = choice.SoundPath; },
                "mvp.selcted", Text(p, "mvp.random<option>"));
        }, Disabled: entries.Count == 0));
        return new(Text(player, "mvp.main_menu.select_mvp<option>"), entries);
    }

    private MvpMenuPage BuildCategory(IPlayer player, string category)
    {
        var entries = new List<MvpMenuEntry>();
        if (Config.MVPs.TryGetValue(category, out var mvps))
            foreach (var (key, template) in mvps)
                if (Helper.PlayerHasAccessToMvp(Core, player, template))
                    entries.Add(new(Text(player, template.DisplayName), Submenu: p => BuildActions(p, key)));
        return new(Text(player, category), entries);
    }

    private MvpMenuPage BuildActions(IPlayer player, string key)
    {
        if (!TryAccessible(player, key, out var template)) return BuildSelection(player);
        var entries = new List<MvpMenuEntry>
        {
            new(Text(player, "mvp.select_this<option>"), p =>
            {
                if (!TryAccessible(p, key, out var current)) return;
                Change(p, s => { s.HasRandomMvp = false; s.MVPName = key; s.SoundPath = current.Sound; },
                    "mvp.selcted", Text(p, current.DisplayName));
            })
        };
        if (template.EnablePreview)
            entries.Add(new(Text(player, "mvp.preview<option>"), p =>
            {
                if (TryAccessible(p, key, out var current) && current.EnablePreview)
                    Helper.PlaySound(p, current.Sound, getVolume(p));
            }, KeepOpen: true));
        return new(Text(player, template.DisplayName), entries);
    }

    private MvpMenuPage BuildRemoval(IPlayer player) => new(Text(player, "mvp.remove.confirm<title>"),
    [
        new(Text(player, "mvp.remove.confirm_yes<option>"), p =>
            Change(p, s => { s.HasRandomMvp = false; s.MVPName = ""; s.SoundPath = ""; }, "mvp.removed")),
        new(Text(player, "mvp.remove.cancel<option>"), Reopen)
    ]);

    private bool TryAccessible(IPlayer player, string key, out MVP_Template template) =>
        Helper.TryGetMvpTemplate(Config, key, out template) && Helper.PlayerHasAccessToMvp(Core, player, template);

    private void Change(IPlayer player, Action<MVPCookies.PlayerSettings> change, string message, params object[] args)
    {
        if (_disposed || !player.IsValid) return;
        var settings = cookies.GetPlayerSettings(player);
        if (settings == null) return;
        change(settings);
        cookies.SavePlayerSettings(settings);
        player.SendChat($"{Text(player, "prefix")} {Text(player, message, args)}");
        Reopen(player);
    }

    private void Reopen(IPlayer player) => Core.Scheduler.NextTick(() => OpenMainMenu(player));
}
