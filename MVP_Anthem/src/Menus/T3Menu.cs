using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using T3Menu.Contract;

namespace MVP_Anthem;

public sealed class T3MvpMenu(ISwiftlyCore core, MVPConfig config, MVPCookies cookies, IT3Menu service,
    Func<IPlayer, float> getVolume)
    : MvpMenuBase(core, config, cookies, getVolume)
{
    private readonly Dictionary<int, List<Menu>> _menus = [];

    private protected override void Open(IPlayer player, MvpMenuPage page)
    {
        RemovePlayer(player.PlayerID);
        var menu = Build(player, page);
        service.Open(menu, player);
    }

    private Menu Build(IPlayer owner, MvpMenuPage page)
    {
        var menu = new Menu(page.Title);
        if (!_menus.TryGetValue(owner.PlayerID, out var owned)) _menus[owner.PlayerID] = owned = [];
        owned.Add(menu);
        foreach (var entry in page.Entries)
        {
            if (entry.Submenu is { } submenu)
                menu.AddSubmenu(entry.Text, player => Build(player, submenu(player)), entry.Disabled);
            else
                menu.AddItem(entry.Text, (player, _) =>
                {
                    if (IsActive && player.IsValid) entry.Action?.Invoke(player);
                }, entry.Disabled);
        }
        return menu;
    }

    public override void RemovePlayer(int playerId)
    {
        if (_menus.Remove(playerId, out var menus))
            foreach (var menu in menus) service.Close(menu, playerId);
    }

    public override void Dispose()
    {
        base.Dispose();
        foreach (var id in _menus.Keys.ToArray()) RemovePlayer(id);
    }
}
