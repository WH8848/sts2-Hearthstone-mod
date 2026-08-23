using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using jaina.Scripts.Character.Keywords;
using jaina.Scripts.Character.Minions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 不稳定的骷髅 (Volatile Skeleton) - 吉安娜专属随从牌（炉石传说风格）。
/// 0费：召唤一个 2/2 的[red]不稳定的骷髅[/red]站场。
/// 随从属性展示在卡面，亡语关键词自动注入到描述前。
/// </summary>
[RegisterCard(typeof(JainaNeutralCardPool))]
public sealed class VolatileSkeletonCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 亡灵种族 + 亡语：随机对一个敌人造成 2 点伤害（随从死亡时触发）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        JainaKeywords.Undead,
        JainaKeywords.Deathrattle
    , CardKeyword.Exhaust];

    /// <summary>
    /// 卡牌原画：炉石传说"不稳定的骷髅"高清原画
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/volatile_skeleton.png";

    public VolatileSkeletonCard()
        : base(0, CardRarity.Token)
    {
    }

    /// <summary>
    /// 召唤不稳定的骷髅随从生物
    /// </summary>
    protected override Type MinionType => typeof(VolatileSkeleton);

    /// <summary>
    /// 随从攻击力 2
    /// </summary>
    protected override int MinionAttack => 2;

    /// <summary>
    /// 随从生命值 2
    /// </summary>
    protected override int MinionHealth => 2;

    
}