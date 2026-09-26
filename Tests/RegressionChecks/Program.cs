using System.Reflection;
using Cookies.Contract;
using MVP_Anthem;
using Helper = MVP_Anthem.Helper;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Translation;
using T3Menu.Contract;

var checks = 0;
var loads = 0;
var saves = 0;
var allowed = true;
ulong session = 1;
var player = Stub.Create<IPlayer>((m, _) => m.Name switch
{
    "get_IsValid" => true,
    "get_PlayerID" => 1,
    "get_SessionId" => session,
    "get_SteamID" => 42UL,
    _ => Stub.Default(m.ReturnType)
});
var cookieApi = Stub.Create<IPlayerCookiesAPIv1>((m, args) =>
{
    if (m.Name == "Load") loads++;
    if (m.Name == "Save") saves++;
    return m.Name == "GetOrDefault" ? args![^1] : Stub.Default(m.ReturnType);
});
var cookies = new MVPCookies(cookieApi);
var first = cookies.GetPlayerSettings(player)!;
Check(ReferenceEquals(first, cookies.GetPlayerSettings(player)) && loads == 1, "Repeated reads use cached settings");
cookies.RemovePlayer(1);
Check(!ReferenceEquals(first, cookies.GetPlayerSettings(player)) && loads == 2, "Disconnect invalidates cache");
Check(Helper.ClampVolume(float.NaN) == 0 && Helper.ClampVolume(2) == 1 && Helper.ClampVolume(-1) == 0,
    "Invalid and out-of-range volumes are safe");
var previousSession = cookies.GetPlayerSettings(player);
session++;
Check(!ReferenceEquals(previousSession, cookies.GetPlayerSettings(player)) && loads == 3,
    "Reused player slot does not inherit cached settings");
var localizer = Stub.Create<ILocalizer>((m, args) => m.Name == "get_Item" ? args![0] : Stub.Default(m.ReturnType));
var translation = Stub.Create(typeof(ISwiftlyCore).GetProperty("Translation")!.PropertyType,
    (m, _) => m.Name == "GetPlayerLocalizer" ? localizer : Stub.Default(m.ReturnType));
var permission = Stub.Create(typeof(ISwiftlyCore).GetProperty("Permission")!.PropertyType,
    (m, _) => m.Name == "PlayerHasPermission" ? allowed : Stub.Default(m.ReturnType));
var scheduler = Stub.Create(typeof(ISwiftlyCore).GetProperty("Scheduler")!.PropertyType,
    (m, _) => Stub.Default(m.ReturnType));
var core = Stub.Create<ISwiftlyCore>((m, _) => m.Name switch
{
    "get_Translation" => translation,
    "get_Permission" => permission,
    "get_Scheduler" => scheduler,
    _ => Stub.Default(m.ReturnType)
});
Menu? opened = null;
var closed = new HashSet<Menu>();
var service = Stub.Create<IT3Menu>((m, args) =>
{
    if (m.Name == "Open") opened = (Menu)args![0]!;
    if (m.Name == "Close") closed.Add((Menu)args![0]!);
    return Stub.Default(m.ReturnType);
});
var config = new MVPConfig
{
    MVPs = new() { ["category"] = new() { ["restricted"] = new()
    { DisplayName = "anthem.name", Sound = "Weapon_AK47.Single", Permissions = ["vip"] } } }
};
using var menu = new T3MvpMenu(core, config, cookies, service, _ => .4f);
menu.OpenMainMenu(player);
var main = opened!;
Check(main.Items.OfType<SubmenuItem>().Count() == 1, "T3 main menu contains only the MVP selection submenu");
var selection = main.Items.OfType<SubmenuItem>().First().CreateSubmenu(player);
var category = selection.Items.OfType<SubmenuItem>().Single().CreateSubmenu(player);
var actions = category.Items.OfType<SubmenuItem>().Single().CreateSubmenu(player);
allowed = false;
actions.Items.OfType<MenuItem>().First().Invoke(player);
Check(saves == 0, "Permission revoked after opening prevents selection");
Check(Helper.GetRandomMvpForPlayer(core, config, player) == null, "Random selection excludes inaccessible anthems");
allowed = true;
Check(Helper.GetRandomMvpForPlayer(core, config, player)?.MvpName == "restricted", "Random selection returns eligible anthem");
actions.Items.OfType<MenuItem>().First().Invoke(player);
Check(saves == 1 && cookies.GetPlayerSettings(player)!.MVPName == "restricted", "Selection persists the selected anthem");
menu.RemovePlayer(1);
Check(closed.Contains(main) && closed.Contains(actions), "Cleanup closes owned root and nested menus");
Console.WriteLine($"All {checks} regression checks passed.");

void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    checks++;
    Console.WriteLine($"PASS: {message}");
}

public class Stub : DispatchProxy
{
    private Func<MethodInfo, object?[]?, object?> _handler = null!;
    public static T Create<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class => (T)Create(typeof(T), handler);
    public static object Create(Type type, Func<MethodInfo, object?[]?, object?> handler)
    {
        var proxy = (Stub)DispatchProxy.Create(type, typeof(Stub));
        proxy._handler = handler;
        return proxy;
    }
    public static object? Default(Type type) => type != typeof(void) && type.IsValueType ? Activator.CreateInstance(type) : null;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => _handler(method!, args);
}
