using System.Reflection;
using HarmonyLib;
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
        var assembly = Assembly.GetExecutingAssembly();

        // 注册动态卡牌类型（CardType.Minion 随从类型），须在模型注册前
        jaina.Scripts.Character.Cards.JainaCardTypes.Initialize();

        // 动态 CardType 兼容补丁（ToLocString 显示"随从"、卡框/边框映射技能样式等）
        var harmony = new Harmony("jaina");
        harmony.PatchAll(assembly);

        RitsuLibFramework.EnsureGodotScriptsRegistered(assembly, Logger);
        // 自动注册内容
        ModTypeDiscoveryHub.RegisterModAssembly(ModId, assembly);

        // 注册吉安娜随从布局：将随从摆放在玩家（充能球区域）周围
        MinionLayoutManager.Register(new OrbStyleMinionLayout(), priority: 100);
    }
}
