using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Players;

namespace MVP_Anthem;

public sealed class CoreMvpMenu(ISwiftlyCore core, MVPConfig config, MVPCookies cookies,
    Func<IPlayer, float> getVolume)
    : MvpMenuBase(core, config, cookies, getVolume)
{
    private readonly Dictionary<int, List<IMenuAPI>> _menus = [];

    private protected override void Open(IPlayer player, MvpMenuPage page)
    {
        RemovePlayer(player.PlayerID);
        Core.MenusAPI.OpenMenuForPlayer(player, Build(player, page));
    }

    public override void RemovePlayer(int playerId)
    {
        if (_menus.Remove(playerId, out var menus))
            foreach (var menu in menus) Core.MenusAPI.CloseMenu(menu);
    }

    public override void Dispose()
    {
        base.Dispose();
        foreach (var id in _menus.Keys.ToArray()) RemovePlayer(id);
    }

    private IMenuAPI Build(IPlayer player, MvpMenuPage page)
    {
        var builder = Core.MenusAPI.CreateBuilder();
        builder.Design.SetMenuTitle(Config.Menu.GradientTitleColor
            ? HtmlGradient.GenerateGradientText(page.Title, "#F59E0B", "#3B82F6") : page.Title);
        builder.SetPlayerFrozen(Config.Menu.FreezePlayer);
        if (Config.Menu.EnableSounds) builder.EnableSound();
        else builder.DisableSound();
        foreach (var entry in page.Entries)
        {
            if (entry.Submenu is { } submenu)
                builder.AddOption(new SubmenuMenuOption(entry.Text, () => Build(player, submenu(player)), 250, 250));
            else if (entry.Disabled)
                builder.AddOption(new TextMenuOption(entry.Text, 250, 250) { Enabled = false, PlaySound = false });
            else
            {
                var option = new ButtonMenuOption(entry.Text, 250, 250) { CloseAfterClick = !entry.KeepOpen };
                option.Click += (_, args) =>
                {
                    if (IsActive && args.Player is { IsValid: true } current)
                        Core.Scheduler.NextTick(() =>
                        {
                            if (IsActive && current.IsValid) entry.Action?.Invoke(current);
                        });
                    return ValueTask.CompletedTask;
                };
                builder.AddOption(option);
            }
        }
        var menu = builder.Build();
        if (!_menus.TryGetValue(player.PlayerID, out var owned)) _menus[player.PlayerID] = owned = [];
        owned.Add(menu);
        return menu;
    }
}
