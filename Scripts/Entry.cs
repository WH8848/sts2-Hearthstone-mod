using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using jaina.Scripts.Character.Cards;
using jaina.Scripts.Character.Minions;
using MegaCrit.Sts2.Core.Entities.Creatures;
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

        // 验证 5 张原版卡的 OnPlay 状态机 patch 目标完整：
        // 游戏版本更新导致状态机结构变化时 TargetMethods 静默失配（不生效不报错），
        // 此处显式 Warn 告警（缺失卡名会出现在启动日志）。
        jaina.Scripts.Character.Powers.HeroPowerHandFullDrawCardPatch.VerifyTargets();

        // 通用验证：反射扫描所有 [HarmonyPatch]（无显式目标）+ TargetMethod(s) 动态定位的 patch，
        // 目标缺失时 Harmony 静默跳过（不生效不报错）——版本更新 IL 变化时显式告警。
        VerifyDynamicHarmonyTargets();

        // 【临时诊断】PatchAll 后检查 ToLocString 的 patch 是否应用（排查图鉴 SwitchExpressionException）
        try
        {
            var toLoc = System.Reflection.MethodBase.GetMethodFromHandle(
                typeof(MegaCrit.Sts2.Core.Entities.Cards.CardTypeExtensions).GetMethod(
                    nameof(MegaCrit.Sts2.Core.Entities.Cards.CardTypeExtensions.ToLocString))!.MethodHandle);
            var info = Harmony.GetPatchInfo(toLoc);
            var prefixOwners = info?.Prefixes.Select(p => p.owner).ToList() ?? [];
            Logger.Info($"[JainaDiag] ToLocString patch info: prefixes={info?.Prefixes.Count ?? 0} " +
                        $"owners=[{string.Join(",", prefixOwners)}] " +
                        $"Minion={jaina.Scripts.Character.Cards.JainaCardTypes.Minion} " +
                        $"Hero={jaina.Scripts.Character.Cards.JainaCardTypes.Hero}");
        }
        catch (System.Exception ex)
        {
            Logger.Info($"[JainaDiag] ToLocString patch info failed: {ex}");
        }

        RitsuLibFramework.EnsureGodotScriptsRegistered(assembly, Logger);
        // 自动注册内容
        ModTypeDiscoveryHub.RegisterModAssembly(ModId, assembly);

        // 交易托管动作描述符预注册（联机远端也须注册：惰性静态初始化若未触发，
        // 远端收到动作广播时 opcode 未知被静默丢弃 → 交易只在主机执行 →
        // 两端牌堆分歧 → StateDivergence 断联。此处确保所有进程启动即注册。）
        jaina.Scripts.Character.Powers.JainaTradeAction.EnsureRegistered();

        // 衍生卡金色手牌高亮（RitsuLib gold 通道）：本局对战内衍生出来的
        // Jaina 法术/随从卡在手牌中金色发光；非衍生卡保持原版行为
        RegisterCardHandGlow(assembly);

        // 小玩物小屋抽到的牌：手牌蓝色描边发光（与衍生卡金色区分，提示本回合打出可重新开启小屋）
        // 规则注册到 CardModel 基类，抽到的任意卡（含中立卡）都生效；必须在内容注册冻结前调用
        jaina.Scripts.Character.Powers.TrinketHandGlow.Register();

        // 条件触发卡（匣中古神/埃匹希斯冲击/不公平游戏/能量之泉）：条件满足时手牌深白描边发光
        // （抽牌堆无随从牌 → 打出触发额外效果）；必须在内容注册冻结前调用
        jaina.Scripts.Character.Powers.JainaConditionGlow.Register();

        // 注册吉安娜随从布局：将随从摆放在玩家（充能球区域）周围
        MinionLayoutManager.Register(new OrbStyleMinionLayout(), priority: 100);

        // 随从选中快捷键（小键盘1-7 选中/取消己方随从，Esc 取消；RitsuLib
        // RuntimeHotkeyService 注册 + ModSettings 设置页可改键，见 MinionSelectHotkeys）
        jaina.Scripts.Character.Powers.MinionSelectHotkeys.Initialize();

        // 武器系统：战斗开始时给玩家挂载角色固有的 1 点攻击行动点（与武器无关，
        // 武器只赋予攻击力；攻击力为 0 时不可行动）
        MegaCrit.Sts2.Core.Combat.CombatManager.Instance.CombatBegan += OnCombatBeganForWeaponAction;

        // 战斗结束：清空 AutoPlay 实例标记与"吉安娜发起"标记（防跨战斗残留——
        // 残留标记会导致战斗外的玩家选择（敲击升级/商店/事件等）被 AutoPickSelectionPatch
        // 误判为随机释放而自动选）
        MegaCrit.Sts2.Core.Combat.CombatManager.Instance.CombatEnded += _ =>
        {
            jaina.Scripts.Character.Powers.AutoPlayGuard.CurrentAutoPlayCard = null;
            jaina.Scripts.Character.Powers.AutoPlayGuard.CurrentAutoPlayIsJainaOrigin = false;
            // 清空引燃记录（防跨战斗残留误消耗）
            jaina.Scripts.Character.Powers.IgniteTracker.Clear();
        };

        // 【临时诊断】游戏就绪后打印 JainaCardPool 实际内容（排查寒冰箭不在商店候选问题）
        RegisterMerchantDiag();

        // 【诊断】打印匣中古神/谜之匣的释放卡池实际内容（模型注册就绪后）
        RegisterYoggPoolDiag();
    }

    /// <summary>
    /// 通用验证：反射扫描本程序集中所有 <c>[HarmonyPatch]</c>（<b>无显式目标</b>）+
    /// <c>TargetMethod()/TargetMethods()</c> 动态定位的 patch 类——
    /// 目标方法缺失时 Harmony <b>静默跳过</b>（patch 不生效、不报错）。
    /// 游戏版本更新导致状态机/方法结构变化时，此处显式 Warn 告警（带类名）。
    /// 显式目标（typeof+nameof / 字符串方法名）的 patch 不需要：PatchAll 找不到会抛异常（显式失败）。
    /// </summary>
    private static void VerifyDynamicHarmonyTargets()
    {
        try
        {
            var assembly = typeof(Entry).Assembly;
            int checkedCount = 0;
            foreach (var type in assembly.GetTypes())
            {
                if (type.GetCustomAttribute<HarmonyPatch>() == null)
                {
                    continue;
                }
                // 只检查动态定位：含 TargetMethod/TargetMethods 私有静态方法
                var targetMethod = type.GetMethod("TargetMethod",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                var targetMethods = type.GetMethod("TargetMethods",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                if (targetMethod == null && targetMethods == null)
                {
                    continue;
                }
                checkedCount++;
                if (targetMethod != null)
                {
                    var result = targetMethod.Invoke(null, null);
                    if (result == null)
                    {
                        Logger.Warn($"[Jaina] Harmony 动态目标缺失: {type.Name}.TargetMethod 返回 null——" +
                                    "patch 未生效，游戏版本可能已更新，请检查该 patch");
                    }
                }
                if (targetMethods != null)
                {
                    var result = targetMethods.Invoke(null, null) as System.Collections.IEnumerable;
                    bool any = false;
                    if (result != null)
                    {
                        var enumerator = result.GetEnumerator();
                        any = enumerator.MoveNext();
                    }
                    if (!any)
                    {
                        Logger.Warn($"[Jaina] Harmony 动态目标缺失: {type.Name}.TargetMethods 返回空——" +
                                    "patch 未生效，游戏版本可能已更新，请检查该 patch");
                    }
                }
            }
            Logger.Info($"[JainaDiag] dynamic harmony targets verified: {checkedCount} patches");
        }
        catch (System.Exception ex)
        {
            Logger.Warn($"[Jaina] dynamic harmony target verification failed: {ex}");
        }
    }

    /// <summary>
    /// 战斗开始：统一按"牌库检测"规则给玩家挂载角色能力——
    /// 只有手牌/抽牌堆/弃牌堆中有<b>吉安娜武器卡</b>的玩家挂载武器攻击行动点；
    /// 只有手牌/抽牌堆/弃牌堆中有<b>吉安娜随从卡</b>的玩家挂载随从军势。
    /// 吉安娜也按此规则：开局卡组无相关卡则不显示；中途获得/发现的吉安娜卡进入
    /// 牌库后，下一场战斗开始检测到即挂载（武器卡/随从卡均按卡类型判定，含升级形态）。
    /// 注意：必须遍历全部玩家（而非仅 LocalContext.GetMe）——CombatBegan 事件在每端
    /// 独立触发且此处使用本地执行上下文（不广播）——若只给本地玩家挂载，
    /// 联机时另一端看不到该 Power，导致状态分歧（State Divergence）断线。
    /// 每端为所有玩家挂载 → 两端状态一致（牌库两端同步，检测结果一致）。
    /// </summary>
    private static void OnCombatBeganForWeaponAction(MegaCrit.Sts2.Core.Combat.CombatState state)
    {
        try
        {
            foreach (var player in state.Players)
            {
                var pcs = player.PlayerCombatState;
                if (pcs == null)
                {
                    continue;
                }
                var ctx = new MegaCrit.Sts2.Core.GameActions.Multiplayer.ThrowingPlayerChoiceContext();
                // 牌库检测：手牌/抽牌堆/弃牌堆中有吉安娜武器卡 → 挂武器攻击行动点
                if (pcs.AllCards.Any(c => c is jaina.Scripts.Character.Weapons.JainaWeaponCardTemplate))
                {
                    _ = MegaCrit.Sts2.Core.Helpers.TaskHelper.RunSafely(
                        jaina.Scripts.Character.Weapons.JainaWeaponSlot.EnsureAttackAction(ctx, player));
                }
                // 牌库检测：手牌/抽牌堆/弃牌堆中有吉安娜随从卡 → 挂随从军势（幂等）
                if (pcs.AllCards.Any(c => c is jaina.Scripts.Character.Cards.JainaMinionCardTemplate))
                {
                    _ = MegaCrit.Sts2.Core.Helpers.TaskHelper.RunSafely(
                        jaina.Scripts.Character.Powers.MinionSquadPower.EnsureAppliedAsync(ctx, player));
                }
                // 引燃时钟：对所有玩家幂等挂载（引燃卡牌 3 回合后消耗的检查点，
                // 不依赖牌库检测——任意玩家手牌都可能出现带引燃的卡）
                _ = MegaCrit.Sts2.Core.Helpers.TaskHelper.RunSafely(
                    jaina.Scripts.Character.Powers.IgniteClockPower.EnsureAppliedAsync(ctx, player));
                // 打出记录兜底钩子：对所有玩家幂等挂载——原版卡(如"熵")打出时
                // 也能被 RecordPlayed 记录,蓄谋诈骗犯战吼才能正确重放"上一张"
                _ = MegaCrit.Sts2.Core.Helpers.TaskHelper.RunSafely(
                    jaina.Scripts.Character.Powers.PlayedRecordHookPower.EnsureAppliedAsync(ctx, player));
                // 联机：角色死亡时清空其随从槽（参考故障机器人/亡灵契约师）。
                // 玩家角色死亡是确定性事件，两端各自触发 → 两端随从清理一致。
                player.Creature.Died -= OnPlayerCreatureDied;
                player.Creature.Died += OnPlayerCreatureDied;
            }
        }
        catch (System.Exception ex)
        {
            Logger.Info($"[JainaWeapon] combat-began attack action failed: {ex}");
        }
    }

    /// <summary>
    /// 玩家角色死亡：清空其随从槽（逐个击杀随从——宠物死亡自动从
    /// PlayerCombatState.Pets 移除，随从槽清空）。
    /// </summary>
    private static void OnPlayerCreatureDied(Creature playerCreature)
    {
        try
        {
            var player = playerCreature.Player;
            if (player?.PlayerCombatState == null)
            {
                return;
            }
            foreach (var pet in player.PlayerCombatState.Pets.ToList())
            {
                if (pet != null && pet.IsAlive)
                {
                    _ = MegaCrit.Sts2.Core.Helpers.TaskHelper.RunSafely(
                        MegaCrit.Sts2.Core.Commands.CreatureCmd.Kill(pet));
                }
            }
        }
        catch (System.Exception ex)
        {
            Logger.Info($"[Jaina] clear pets on player death failed: {ex}");
        }
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

    /// <summary>
    /// 【诊断】匣中古神/谜之匣释放卡池内容打印：
    /// 模型注册就绪后枚举 YoggBoxCard.GetSpellPoolCanonicals()，
    /// 按费用排序打印每张卡的 Id/标题/费用/可升级级别数，供排查卡池构成。
    /// </summary>
    private static void RegisterYoggPoolDiag()
    {
        try
        {
            Logger.Info("[JainaDiag] subscribing yogg pool diag...");
            STS2RitsuLib.RitsuLibFramework.SubscribeLifecycle<STS2RitsuLib.ModelRegistryInitializedEvent>(static _ =>
            {
                try
                {
                    var pool = jaina.Scripts.Character.Cards.YoggBoxCard.GetSpellPoolCanonicals();
                    Logger.Info($"[JainaDiag] Yogg spell pool canonical count={pool.Count}");
                    foreach (var c in pool
                                 .OrderBy(c => c.EnergyCost.Canonical)
                                 .ThenBy(c => c.Id.Entry))
                    {
                        int maxLevel = jaina.Scripts.Character.JainaCastTracker.GetDiscoverPoolMaxUpgradeLevel(c.GetType());
                        Logger.Info($"[JainaDiag] yogg pool: cost={c.EnergyCost.Canonical} lvl0..{maxLevel} {c.Id} | {c.Title}");
                    }
                    // 基础版"抽牌堆无随从时费用≥2"过滤后的池
                    var cost2Plus = pool.Where(c => c.EnergyCost.Canonical >= 2).ToList();
                    Logger.Info($"[JainaDiag] Yogg pool (cost>=2 only) count={cost2Plus.Count}");
                }
                catch (System.Exception ex)
                {
                    Logger.Info($"[JainaDiag] yogg pool diag error: {ex}");
                }
            });
            Logger.Info("[JainaDiag] yogg pool diag subscribed.");
        }
        catch (System.Exception ex)
        {
            Logger.Info($"[JainaDiag] yogg pool diag subscribe failed: {ex}");
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
