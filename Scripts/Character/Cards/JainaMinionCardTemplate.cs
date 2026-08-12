using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using jaina.Scripts.Character.Keywords;
using jaina.Scripts.Character.Minions;
using MinionLib.Minion;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 吉安娜随从牌基类（炉石传说风格）。
/// 打出后召唤一个随从生物留在场上（随从回合结束自动攻击敌人）。
/// 卡面通过关键词（亡语/冲锋）自动注入文本，描述中展示随从的攻击/生命属性。
/// 卡牌类型为动态注册的"随从"类型（JainaCardTypes.Minion），
/// 显示文本由 ToLocString patch 提供，卡框/边框由 FramePath 等 patch 映射为技能样式。
/// </summary>
public abstract class JainaMinionCardTemplate : ModCardTemplate
{
    /// <summary>
    /// 该随从牌召唤的随从生物类型
    /// </summary>
    protected abstract Type MinionType { get; }

    /// <summary>
    /// 随从攻击力
    /// </summary>
    protected abstract int MinionAttack { get; }

    /// <summary>
    /// 随从生命值
    /// </summary>
    protected abstract int MinionHealth { get; }

    /// <summary>
    /// 随从站场位置（默认玩家前方上方区域）
    /// </summary>
    protected virtual MinionPosition MinionPosition => MinionPosition.FrontUpper;

    /// <summary>
    /// 卡牌类型：动态注册的"随从"类型
    /// </summary>
    public override CardType Type => JainaCardTypes.Minion;

    /// <summary>
    /// 内置关键词：亡语。
    /// 通过 CanonicalKeywords 声明（而非构造函数 AddKeyword），
    /// 避免修改游戏创建的 canonical 不可变实例导致 CanonicalModelException。
    /// 注册了 CardDescriptionPlacement.BeforeCardDescription 的关键词
    /// 会自动将其金色 BBCode 注入到卡面描述之前。
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        JainaKeywords.Deathrattle
    ];

    protected JainaMinionCardTemplate(int cost, CardRarity rarity)
        : base(cost, CardType.Skill, rarity, TargetType.Self, true)
    {
    }

    /// <summary>
    /// 打出：召唤随从生物站场。随从由 MinionLib 管理，回合结束自动攻击敌人。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await JainaMinionPool.SummonMinionByType(
            choiceContext,
            base.Owner,
            MinionType,
            maxHp: MinionHealth,
            attack: MinionAttack,
            position: MinionPosition);
    }
}