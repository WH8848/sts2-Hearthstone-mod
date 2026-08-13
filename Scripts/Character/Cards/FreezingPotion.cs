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
    /// 双生法术关键词（升级后生效）+ 冻结 + 冰霜派系 + 法术牌
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        IsUpgraded
            ? [JainaKeywords.Twinspell, JainaKeywords.Spell, JainaKeywords.Freeze, JainaKeywords.Frost]
            : [JainaKeywords.Spell, JainaKeywords.Freeze, JainaKeywords.Frost];

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
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        // 霜冻射线（升级后）：目标是任一角色
        Creature? target = cardPlay.Target;
        if (target is not { IsAlive: true })
        {
            return;
        }

        // 冻结目标 1 层
        await PowerCmd.Apply<FreezePower>(choiceContext, [target], 1m, base.Owner.Creature, this);

        // 双生法术：仅当本卡仍具有双生法术关键词时复制。
        // 不能按 IsUpgraded 判断——复制品也是升级实例，但已 RemoveKeyword 移除词条，
        // 用 Keywords 判断可保证复制品打出时不再复制（避免无限复制链）。
        MegaCrit.Sts2.Core.Logging.Log.Info($"[JainaDebug] FreezingPotion OnPlay: upgraded={IsUpgraded} hasTwinspell={Keywords.Contains(JainaKeywords.Twinspell)}");
        if (Keywords.Contains(JainaKeywords.Twinspell))
        {
            // CreateClone 保留 Owner（MutableClone 的卡无 Owner 会导致入牌堆 NRE）
            var copy = CreateClone();
            copy.RemoveKeyword(JainaKeywords.Twinspell);
            jaina.Scripts.Character.JainaCastTracker.MarkGenerated(copy);
            await CardPileCmd.AddGeneratedCardToCombat(copy, PileType.Hand, base.Owner);
            MegaCrit.Sts2.Core.Logging.Log.Info($"[JainaDebug] FreezingPotion twinspell copied: copyKeywords={string.Join(",", copy.Keywords)}");
        }
    }

    protected override void OnUpgrade()
    {
        // 升级为霜冻射线：双生法术 + 目标改为任一角色
    }
}
