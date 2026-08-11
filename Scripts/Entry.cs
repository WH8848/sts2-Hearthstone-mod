using System.Reflection;
using jaina.Scripts.Character.Minions;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MinionLib.Layout;
using STS2RitsuLib;
using STS2RitsuLib.Interop;

namespace jaina.Scripts;

[ModInitializer(nameof(Init))]
public class Entry
{
    // 你的modid
    public const string ModId = "jaina";
    public static readonly Logger Logger = RitsuLibFramework.CreateLogger(ModId);

    public static void Init()
    {
        // harmony可用，但是最好用ritsu的封装patch，见补丁系统一章
        // var harmony = new Harmony("com.example.testmod");
        // harmony.PatchAll();
        var assembly = Assembly.GetExecutingAssembly();
        RitsuLibFramework.EnsureGodotScriptsRegistered(assembly, Logger);
        // 自动注册内容
        ModTypeDiscoveryHub.RegisterModAssembly(ModId, assembly);

        // 注册吉安娜随从布局：将随从摆放在玩家（充能球区域）周围
        MinionLayoutManager.Register(new OrbStyleMinionLayout(), priority: 100);
    }
}
