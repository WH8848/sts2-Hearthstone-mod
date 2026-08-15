using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 埃匹希斯冲击 (Apexis Blast) - 吉安娜专属攻击牌（普通，1 费）。
/// 造成 5 点伤害。如果你的抽牌堆中没有随从牌，随机召唤一个费用消耗为 1 的随从。
/// 升级后变为"火焰之地传送门 (Firelands Portal)"（2 费）：造成 6 点伤害，随机召唤一个费用为 2 的随从。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class ApexisBlast : JainaSpellCardTemplate
{
    /// <summary>
    /// 法术牌：攻击牌和技能牌都视为法术牌；升级后（火焰之地传送门）为火焰派系
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => IsUpgraded
        ? [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Fire]
        : [jaina.Scripts.Character.Keywords.JainaKeywords.Spell];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(5m, ValueProp.Move)
    ];

    /// <summary>
    /// 卡牌原画：埃匹希斯冲击 / 升级后（火焰之地传送门）切换原画
    /// </summary>
    public override string CustomPortraitPath =>
        IsUpgraded ? "res://assets/card_art/firelands_portal.png" : "res://assets/card_art/apexis_blast.png";

    public ApexisBlast()
        : base(1, CardType.Attack, CardRarity.Uncommon, JainaTargetTypes.AnyTargetable, true)
    {
    }

    /// <summary>
    /// 升级后卡牌名称变为"火焰之地传送门 (Firelands Portal)"，费用保持 1 费不变。
    /// </summary>
    public override string Title
    {
        get
        {
            var title = new LocString("cards", base.Id.Entry + ".title");
            if (!IsUpgraded)
            {
                return title.GetFormattedText();
            }
            LocString? upgraded = LocString.GetIfExists("cards", base.Id.Entry + ".titleUpgraded");
            return upgraded?.GetFormattedText() ?? title.GetFormattedText() + "+";
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        if (cardPlay.Target is not { IsAlive: true } target)
        {
            return;
        }

        // 造成伤害
        await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);

        if (IsUpgraded)
        {
            // 火焰之地传送门：随机召唤一个 2 费随从
            // 注意：必须 await —— 随机召唤消耗游戏同步 RNG（RunState.Rng），
            // fire-and-forget 会导致两端 RNG 消耗时序错位，联机状态分歧断联。
            var summoned = await JainaMinionPool.SummonRandomMinionOfCost(choiceContext, base.Owner, 2);
            // 记录召唤来源（不传 source 避免触发战吼）：随从进场后才开始计算——
            // 安东尼达斯等光环不响应"召唤出它的这张卡"的施放事件。
            if (summoned?.Monster is jaina.Scripts.Character.Minions.AntonidasMinion ant)
            {
                ant.SetSummonSourceCard(this);
            }
        }
        else
        {
            // 埃匹希斯冲击：若抽牌堆中没有随从牌，随机召唤一个 1 费随从
            bool hasMinionInDrawPile = base.Owner.PlayerCombatState.DrawPile.Cards.Any(
                c => c.Type == JainaCardTypes.Minion);
            if (!hasMinionInDrawPile)
            {
                await JainaMinionPool.SummonRandomMinionOfCost(choiceContext, base.Owner, 1);
            }
        }
    }

    protected override void OnUpgrade()
    {
        // 升级为火焰之地传送门：伤害 5 -> 6，费用保持 1 费（按用户要求不升级加费）。
        // 火焰派系关键词需显式加入：LocalKeywords 懒初始化只算一次，
        // 升级前缓存的 Keywords 不含 Fire，悬停提示（原版 HoverTips 遍历 Keywords）不会出现火焰解释。
        base.DynamicVars.Damage.BaseValue = 6m;
        AddKeyword(jaina.Scripts.Character.Keywords.JainaKeywords.Fire);
    }
}
