using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using jaina.Scripts.Character.Cards;
using jaina.Scripts.Character.Minions;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MinionLib.Layout;
using STS2RitsuLib;
using STS2RitsuLib.Interop;
using STS2RitsuLib.Scaffolding.Cards.HandGlow;

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

        // 衍生卡金色手牌高亮（RitsuLib gold 通道）：本局对战内衍生出来的
        // Jaina 法术/随从卡在手牌中金色发光；非衍生卡保持原版行为
        RegisterCardHandGlow(assembly);

        // 注册吉安娜随从布局：将随从摆放在玩家（充能球区域）周围
        MinionLayoutManager.Register(new OrbStyleMinionLayout(), priority: 100);

        // 【临时诊断】游戏就绪后打印 JainaCardPool 实际内容（排查寒冰箭不在商店候选问题）
        RegisterMerchantDiag();
    }

    /// <summary>
    /// 衍生卡金色手牌高亮：为所有 Jaina 法术/随从卡类型注册 RitsuLib gold 规则，
    /// 谓词 = 该卡实例是否本局对战内衍生（JainaCastTracker.IsGeneratedCard）。
    /// 英雄技能卡（火焰冲击/二级火焰冲击）不注册（不发光）。
    /// 必须在内容注册冻结（ModContentRegistry.IsFrozen）前调用。
    /// </summary>
    private static void RegisterCardHandGlow(Assembly assembly)
    {
        try
        {
            var goldRules = ModCardHandGlowRules.Gold(
                card => jaina.Scripts.Character.JainaCastTracker.IsGeneratedCard(card));
            var registered = 0;
            foreach (var type in assembly.GetTypes())
            {
                if (type.IsAbstract || type.IsInterface || type.ContainsGenericParameters)
                {
                    continue;
                }
                if (type == typeof(Fireblast) || type == typeof(FireblastAncient))
                {
                    continue;
                }
                if (typeof(JainaSpellCardTemplate).IsAssignableFrom(type) ||
                    typeof(JainaMinionCardTemplate).IsAssignableFrom(type))
                {
                    ModCardHandGlowRegistry.Register(type, goldRules);
                    registered++;
                }
            }
            Logger.Info($"[JainaDiag] card hand-glow registered: {registered} types");
        }
        catch (System.Exception ex)
        {
            Logger.Info($"[JainaDiag] hand-glow register failed: {ex}");
        }
    }

    private static void RegisterMerchantDiag()
    {
        try
        {
            // 0.111.1 审计：GameReadyEvent 依赖 NGame._Ready（可能早于内容就绪/旧 RitsuLib variant 无发布者），
            // 改用 ModelRegistryInitializedEvent（ModelDb.Init 之后发布，内容就绪信号，与商店候选语义贴合）
            Logger.Info("[JainaDiag] subscribing to ModelRegistryInitializedEvent...");
            STS2RitsuLib.RitsuLibFramework.SubscribeLifecycle<STS2RitsuLib.ModelRegistryInitializedEvent>(static _ =>
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
                }
                catch (System.Exception ex)
                {
                    Logger.Info($"[JainaDiag] ERROR: {ex}");
                }
            });
            Logger.Info("[JainaDiag] subscribed.");
        }
        catch (System.Exception ex)
        {
            Logger.Info($"[JainaDiag] subscribe failed: {ex}");
        }
    }
}
