using System;
using System.Threading.Tasks;
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

    public override string Title
    {
        get
        {
            var title = new LocString("cards", base.Id.Entry + ".title");
            return title.GetFormattedText();
        }
    }
}