namespace MVP_Anthem;

public sealed class MVPConfig
{
    public MVP_Settings Settings { get; set; } = new();
    public Menu_Settings Menu { get; set; } = new();
    public Dictionary<string, Dictionary<string, MVP_Template>> MVPs { get; set; } = new()
    {
        {
            "category.public_mvp", new Dictionary<string, MVP_Template>
            {
                {
                    "mvp_1", new MVP_Template
                    {
                        DisplayName = "mvp_1.name",
                        Sound = "Weapon_AK47.Single",
                        EnablePreview = true,
                        ShowHtml = true,
                        ShowChat = true,
                        Permissions = []
                    }
                },
                {
                    "mvp_2", new MVP_Template
                    {
                        DisplayName = "mvp_2.name",
                        Sound = "Weapon_AWP.Single",
                        EnablePreview = true,
                        ShowHtml = true,
                        ShowChat = true,
                        Permissions = []
                    }
                }
            }
        }
    };
}
public sealed class MVP_Settings
{
    public List<string> SoundEventFiles { get; set; } = new List<string>();
    public List<string> MenuTypes { get; set; } = ["core", "t3"];
    public string MenuType { get; set; } = "t3";
    public List<string> MVPCommands { get; set; } = new List<string> { "mvp" };
    public bool ShakePlayerScreen { get; set; } = true;
    public bool RemovePlayerInGameMvp { get; set; } = true;
    public bool GiveRandomMVPOnFirstConnect { get; set; } = true;
    public bool GiveRandomMVPOnFirstJoin
    {
        get => GiveRandomMVPOnFirstConnect;
        set => GiveRandomMVPOnFirstConnect = value;
    }
    public float DefaultVolume { get; set; } = 0.2f;
    public int MVPMaxDuration { get; set; } = 10; // center html message will depend on this.
}
public sealed class Menu_Settings
{
    public bool FreezePlayer { get; set; } = true;
    public bool EnableSounds { get; set; } = true;
    public bool GradientTitleColor { get; set; } = true;
}
public sealed class MVP_Template
{
    public string DisplayName { get; set; } = string.Empty;
    public string Sound { get; set; } = string.Empty;
    public bool EnablePreview { get; set; } = true;
    public bool ShowHtml { get; set; } = true;
    public bool ShowChat { get; set; } = true;
    public List<string> Permissions { get; set; } = new List<string>();
}
