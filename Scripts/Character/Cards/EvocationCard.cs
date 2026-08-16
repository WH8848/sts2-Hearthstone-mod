using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 唤醒 (Evocation) - 0费技能牌（稀有，奥术派系）。
/// 用随机法师法术牌填满你的手牌。这些牌具有虚无。
/// 升级后（唤醒+）：用随机升级过的法师法术牌填满你的手牌。这些牌具有虚无。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class EvocationCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 法术牌 + 奥术派系
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Arcane];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    public override string CustomPortraitPath => "res://assets/card_art/evocation.png";

    public EvocationCard()
        : base(0, CardType.Skill, CardRarity.Rare, TargetType.None, true)
    {
    }

    /// <summary>
    /// 升级后卡牌名称变为"唤醒+"
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
            return title.GetFormattedText() + "+";
        }
    }

    /// <summary>
    /// 吉安娜法师法术牌池（与匣中古神随机施放池一致，排除英雄技能卡）
    /// </summary>
    private static readonly System.Type[] MageSpellPool =
    [
        typeof(Fireball),
        typeof(Frostbolt),
        typeof(ArcaneIntellect),
        typeof(FreezingPotion),
        typeof(IceBarrier),
        typeof(Trick),
        typeof(Awaken),
        typeof(NorgannonWisdom),
        typeof(DeepFreezeCard),
        typeof(FlameWard),
        typeof(DeathborneCard),
        typeof(FlameLance),
        typeof(FrostNova),
        typeof(ArcaneBarrage),
        typeof(ApexisBlast),
        typeof(IgniteCard)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        var combatState = base.Owner.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }
        var rng = base.Owner.RunState.Rng.CombatCardSelection;

        // 用随机法师法术牌填满手牌（不占手牌位的英雄技能卡不影响容量）
        while (!jaina.Scripts.Character.JainaHandHelper.IsHandFull(base.Owner))
        {
            var type = rng.NextItem(MageSpellPool);
            if (type == null)
            {
                break;
            }
            // 升级后（唤醒+）：生成升级过的法术牌（+）
            int upgradeLevel = IsUpgraded ? 1 : 0;
            var card = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
                combatState, base.Owner, type, upgradeLevel);
            if (card == null)
            {
                continue;
            }
            // 这些牌具有虚无（回合结束时留在手牌则消耗）
            MegaCrit.Sts2.Core.Commands.CardCmd.ApplyKeyword(card, CardKeyword.Ethereal);
            jaina.Scripts.Character.JainaCastTracker.MarkGenerated(card);
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, base.Owner);
        }
    }
}
