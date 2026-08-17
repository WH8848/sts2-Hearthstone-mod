using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using jaina.Scripts.Character.Keywords;
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 霜冻射线 (Ray of Frost) - 0费技能牌（罕见，冰霜派系）。
/// 双生法术：冻结一个随从或敌人；使用后获得一张复制牌（复制品不再复制）。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class RayOfFrostCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 双生法术复制品标记：复制品打出时不再复制（替代依赖 Keywords 的判断，
    /// 因为 LocalKeywords 懒初始化在升级前可能已缓存）
    /// </summary>
    public bool IsTwinspellCopy;

    /// <summary>
    /// 双生法术 + 冻结 + 冰霜派系 + 法术牌
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [JainaKeywords.Twinspell, JainaKeywords.Spell, JainaKeywords.Freeze, JainaKeywords.Frost];

    public override string CustomPortraitPath => "res://assets/card_art/ray_of_frost.png";

    public RayOfFrostCard()
        : base(0, CardType.Skill, CardRarity.Uncommon, JainaTargetTypes.AnyTargetable, true)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        // 冻结目标 1 层
        Creature? target = cardPlay.Target;
        if (target is not { IsAlive: true })
        {
            return;
        }
        await PowerCmd.Apply<FreezePower>(choiceContext, [target], 1m, base.Owner.Creature, this);

        // 双生法术：仅原件（非复制品）复制。
        // 不依赖 Keywords.Contains——LocalKeywords 懒初始化只算一次（时序依赖）。
        // 用显式标记判断：复制品标记 IsTwinspellCopy，打出时不再复制。
        if (!IsTwinspellCopy)
        {
            // CreateClone 保留 Owner（MutableClone 的卡无 Owner 会导致入牌堆 NRE）
            // 手牌满时不复制（满手入手会被 0.111.1 静默改道弃牌堆）
            if (jaina.Scripts.Character.JainaHandHelper.IsHandFull(base.Owner))
            {
                return;
            }
            var copy = (RayOfFrostCard)CreateClone();
            copy.RemoveKeyword(JainaKeywords.Twinspell);
            copy.IsTwinspellCopy = true;
            jaina.Scripts.Character.JainaCastTracker.MarkGenerated(copy);
            await CardPileCmd.AddGeneratedCardToCombat(copy, PileType.Hand, base.Owner);
        }
    }
}
