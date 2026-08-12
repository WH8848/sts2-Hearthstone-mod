using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Keywords;
using jaina.Scripts.Character.Powers;
using MinionLib.Targeting;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 冰冻药水 (Freezing Potion) - 0费：冻结一个敌人。
/// 升级后变为霜冻射线 (Frost Ray)：双生法术，冻结一个角色。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class FreezingPotion : ModCardTemplate
{
    /// <summary>
    /// 双生法术关键词（升级后生效）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        IsUpgraded
            ? [JainaKeywords.Twinspell]
            : [];

    /// <summary>
    /// 卡牌原画：炉石传说"冰冻药水"高清原画
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/freezing_potion.png";

    public FreezingPotion()
        : base(0, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy, true)
    {
    }

    /// <summary>
    /// 霜冻射线（升级后）：目标是任一角色（敌我通用）
    /// </summary>
    public override TargetType TargetType =>
        IsUpgraded ? MinionTargetTypes.AnyCreature : TargetType.AnyEnemy;

    /// <summary>
    /// 升级后卡牌名称变为"霜冻射线 (Frost Ray)"
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
        // 霜冻射线（升级后）：目标是任一角色
        Creature? target = cardPlay.Target;
        if (target is not { IsAlive: true })
        {
            return;
        }

        // 冻结目标 1 层
        await PowerCmd.Apply<FreezePower>(choiceContext, [target], 1m, base.Owner.Creature, this);

        // 双生法术：立即将一张该法术的复制置入你的手牌（复制品不再具有双生法术）
        if (IsUpgraded)
        {
            var copy = (CardModel)MutableClone();
            copy.RemoveKeyword(JainaKeywords.Twinspell);
            await CardPileCmd.Add(copy, PileType.Hand);
        }
    }

    protected override void OnUpgrade()
    {
        // 升级为霜冻射线：双生法术 + 目标改为任一角色
    }
}
