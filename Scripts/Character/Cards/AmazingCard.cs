using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Powers;
using MinionLib.Targeting;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 惊奇卡牌 (Scroll of Wonder) - 0费状态牌（衍生）。
/// 抽到时施放随机施放一个全角色卡牌，释放后此卡消耗。
/// 由惊奇套牌洗入抽牌堆；不进入掉落池。
/// </summary>
[RegisterCard(typeof(JainaNeutralCardPool))]
public sealed class AmazingCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 消耗：释放后此卡消耗
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public override string CustomPortraitPath => "res://assets/card_art/scroll_of_wonder.png";

    public AmazingCard()
        : base(0, CardType.Status, CardRarity.Token, TargetType.None, false)
    {
    }

    /// <summary>
    /// 抽到自身时：随机施放一个全角色卡牌（随机合法目标，联机可打队友），释放后本卡消耗。
    /// 覆写 AfterCardDrawn（而非 Hook Postfix + fire-and-forget 串行队列）：
    /// 原版"抽到时触发"的卡（如虚空 Void）都是覆写此钩子，在 networked 钩子链内
    /// <b>阻塞</b>执行——两端在同一动作上下文同步执行，回合开始抽牌（SetupPlayerTurn）
    /// 会等待其完成，checksum 在效果结算后生成，不会产生时序竞态。
    /// 旧实现（AmazingCardDrawPatch Postfix + JainaSerialExecutor）是 fire-and-forget
    /// 异步任务：与 networked 动作/checksum 生成点竞态——一端先结算完本地释放、
    /// 另一端后结算 → StateDivergence 假阳性断联（实测：客机玩家回合开始抽到惊奇卡牌，
    /// 本地释放打自己 2 点 → 客机端 HP 先变、host 端后变 → checksum 13 分歧断联）。
    /// 多张惊奇卡牌同时抽到：Hook 按抽牌顺序逐个 await 每个监听者（天然串行，
    /// 顺序 = 抽牌顺序，两端确定一致，无需串行执行器）。
    /// <para>
    /// <b>模型栈隔离（本轮修复）</b>：释放链（随机卡 AutoPlay，其 OnPlay 会把卡模型压栈、
    /// 可含动画等待/玩家选择/抽牌等长流程）不再在外层传入的 choiceContext 上运行——
    /// 而是在<b>独立嵌套的 HookPlayerChoiceContext</b>（原版官方钩子上下文机制，
    /// Hellraiser/寻觅打击/神谜即用它）上运行：内部 Push/Pop 只影响该上下文自己的
    /// 模型栈，玩家选择会生成独立钩子动作（GenerateHookAction）入队同步，
    /// 不会污染父上下文（抽牌动作的 BranchingPlayerChoiceContext）的模型栈——
    /// 修复 "Tried to pop model 惊奇卡牌 but 栈顶是 PARSE/死亡进军" 的 Push/Pop 交错断言。
    /// </para>
    /// </summary>
    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card != this)
        {
            return;
        }
        try
        {
            var player = Owner;
            if (player == null || player.Creature?.CombatState == null)
            {
                return;
            }
            var combatState = player.Creature.CombatState;
            var rng = player.RunState.Rng.CombatTargets;

            // 全角色攻击/技能/能力牌候选（Attack/Skill/Power——含吉安娜法术牌：
            // 攻击/技能牌及带"法术牌"关键词的能力牌；吉安娜的非法术能力牌
            // 如戏法图腾/炉石形态不在范围内；不含英雄技能卡），
            // 按可升级级别展开：每种牌的未升级与升级形态（+）都是独立候选
            // （应用 Jaina 随机池统一排除：
            // 8 个非角色/衍生池/任务卡/先古稀有度/多人专属）
            var candidates = new List<CardModel>();
            foreach (var canonical in ModelDb.AllCards)
            {
                if (canonical == null)
                {
                    continue;
                }
                if (canonical.Type != CardType.Attack && canonical.Type != CardType.Skill &&
                    canonical.Type != CardType.Power)
                {
                    continue;
                }
                // 吉安娜非法术能力牌（戏法图腾/炉石形态）不在范围内
                if (jaina.Scripts.Character.JainaCastTracker.IsExcludedFromSpellPool(canonical.GetType()))
                {
                    continue;
                }
                if (HeroPowerHandHelper.IsHeroPowerCard(canonical))
                {
                    continue;
                }
                if (!jaina.Scripts.Character.JainaRandomPoolHelper.IsEligible(canonical))
                {
                    continue;
                }
                int maxLevel = jaina.Scripts.Character.JainaCastTracker.GetDiscoverPoolMaxUpgradeLevel(canonical.GetType());
                for (int level = 0; level <= maxLevel; level++)
                {
                    var created = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
                        combatState, player, canonical.GetType(), level);
                    if (created != null)
                    {
                        candidates.Add(created);
                    }
                }
            }
            if (candidates.Count == 0)
            {
                return;
            }
            var spell = rng.NextItem(candidates);
            if (spell == null)
            {
                return;
            }
            jaina.Scripts.Character.JainaCastTracker.MarkGenerated(spell);
            jaina.Scripts.Character.Powers.RommathReplayTracker.Mark(spell);

            // 随机目标：AnyEnemy 单体攻击牌除非描述限定"对敌人"，目标放宽为全部存活生物
            // （自己/队友角色、双方随从、敌人）；其余按卡合法性过滤（合法优先，回退全量）。
            var target = jaina.Scripts.Character.JainaRandomPoolHelper.PickRandomTarget(player, combatState, spell);
            if (spell.TargetType != TargetType.None && target == null)
            {
                return; // 无合法目标：不施放（惊奇卡牌也不消耗）
            }

            // 惊奇卡牌是吉安娜 mod 的随机释放机制（非打出触发，手打标记不适用）：
            // 显式置位"吉安娜发起"——其释放的法术触发选择自动选（不弹界面）
            AutoPlayGuard.CurrentAutoPlayIsJainaOrigin = true;

            // 显示释放的卡牌（角色头顶气泡提示，如"释放了 火球术"——随机释放可见性）
            try
            {
                MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(
                    MegaCrit.Sts2.Core.Nodes.Vfx.NThoughtBubbleVfx.Create(
                        $"释放了 {spell.Title}", player.Creature, 1.5f));
            }
            catch
            {
                // 气泡显示失败不影响释放
            }

            // 释放链运行在独立嵌套上下文（原版 HookPlayerChoiceContext 机制）：
            // 释放卡的 OnPlay 压栈/动画等待/玩家选择都不进入父上下文模型栈
            // （修复父上下文 Push/Pop 交错断言；玩家选择经 GenerateHookAction
            // 以独立动作同步，两端确定性不受影响）。
            var netId = MegaCrit.Sts2.Core.Context.LocalContext.NetId;
            if (netId == null)
            {
                // 测试模式等无 NetId 场景：回退父上下文（保持旧行为）
                await RunReleaseAsync(choiceContext, player, spell, target);
            }
            else
            {
                var hookContext = new MegaCrit.Sts2.Core.GameActions.Multiplayer.HookPlayerChoiceContext(
                    this, netId.Value, combatState,
                    MegaCrit.Sts2.Core.Entities.Multiplayer.GameActionType.CombatPlayPhaseOnly);
                var release = RunReleaseAsync(hookContext, player, spell, target);
                await hookContext.AssignTaskAndWaitForPauseOrCompletion(release);
            }

            // 释放后此卡消耗
            if (Pile != null && Pile.Type == PileType.Hand)
            {
                await CardPileCmd.Add(this, PileType.Exhaust);
            }
        }
        catch (System.Exception ex)
        {
            MegaCrit.Sts2.Core.Logging.Log.Error($"[Jaina] AmazingCard draw trigger failed: {ex}");
        }
    }

    /// <summary>
    /// 释放链：先进入打出区，停顿后再自动打出（施放节奏与"倾泻"等自动打出卡一致——
    /// 原版 AutoPlayFromDrawPile 先逐张 Add 到打出区再逐个 AutoPlay）。
    /// 在指定上下文（独立嵌套 HookPlayerChoiceContext 或回退的父上下文）上执行。
    /// </summary>
    private static async Task RunReleaseAsync(PlayerChoiceContext context, Player player, CardModel spell, Creature? target)
    {
        if (spell.Pile == null)
        {
            await CardPileCmd.Add(spell, PileType.Play);
        }
        await Cmd.Wait(0.5f);
        await CardCmd.AutoPlay(context, spell, target, skipCardPileVisuals: true);
    }
}
