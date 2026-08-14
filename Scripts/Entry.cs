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

        // 【临时诊断】游戏就绪后打印 JainaCardPool 实际内容（排查寒冰箭不在商店候选问题）
        RegisterMerchantDiag();
    }

    private static void RegisterMerchantDiag()
    {
        try
        {
            STS2RitsuLib.RitsuLibFramework.SubscribeLifecycle<STS2RitsuLib.GameReadyEvent>(static _ =>
            {
                try
                {
                    var pool = MegaCrit.Sts2.Core.Models.ModelDb.CardPool<jaina.Scripts.Character.JainaCardPool>();
                    var all = pool.AllCards.ToList();
                    Logger.Info($"[JainaDiag] JainaCardPool.AllCards count={all.Count}");
                    foreach (var c in all)
                    {
                        Logger.Info($"[JainaDiag] pool card: {c.Id} type={c.Type} rarity={c.Rarity} mp={c.MultiplayerConstraint} canGen={c.CanBeGeneratedInCombat}");
                    }
                    var frost = all.FirstOrDefault(c => c.Id.Entry.Contains("FROSTBOLT"));
                    Logger.Info($"[JainaDiag] Frostbolt in AllCards: {frost != null}");
                    var unlocked = pool.GetUnlockedCards(null!, MegaCrit.Sts2.Core.Entities.Cards.CardMultiplayerConstraint.SingleplayerOnly).Select(c => c.Id).ToList();
                    Logger.Info($"[JainaDiag] GetUnlockedCards(SP) count={unlocked.Count}: {string.Join(",", unlocked)}");
                }
                catch (System.Exception ex)
                {
                    Logger.Info($"[JainaDiag] ERROR: {ex}");
                }
            });
        }
        catch (System.Exception ex)
        {
            Logger.Info($"[JainaDiag] subscribe failed: {ex}");
        }
    }
}
