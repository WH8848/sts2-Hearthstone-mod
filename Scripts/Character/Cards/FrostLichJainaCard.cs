using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Minions;
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 冰霜女巫吉安娜 (Frost Lich Jaina) - 3费英雄卡（稀有）。
/// 战吼：召唤一个3/6的水元素。在本局对战中，你的所有元素拥有吸血。
/// 替换英雄技能为"冰冷触摸"。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class FrostLichJainaCard : JainaHeroCardTemplate
{
    /// <summary>
    /// 获得 5 点格挡
    /// </summary>
    protected override int HeroArmor => 5;

    /// <summary>
    /// 替换英雄技能为冰冷触摸
    /// </summary>
    protected override System.Type? HeroPowerType => typeof(IcyTouchCard);

    /// <summary>
    /// 关键词：战吼 + 吸血（冰霜女巫的招牌：所有元素拥有吸血）。
    /// 悬停英雄卡时右侧显示战吼/吸血词条注释。
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Battlecry,
         jaina.Scripts.Character.Keywords.JainaKeywords.Lifesteal];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    public override string CustomPortraitPath => "res://assets/card_art/frost_lich_jaina.png";

    public FrostLichJainaCard()
        : base(3, CardRarity.Rare)
    {
    }

    /// <summary>
    /// 悬停提示：格挡关键词注释，再显示替换后的英雄技能卡（冰冷触摸）与战吼召唤的水元素卡
    /// （参考灵体采集者显示小精灵的做法）。
    /// </summary>
    protected override IEnumerable<IHoverTip> AdditionalHoverTips
    {
        get
        {
            yield return HoverTipFactory.Static(StaticHoverTip.Block);
            // 替换后的英雄技能卡（冰冷触摸）
            if (HeroPowerType != null)
            {
                var heroPower = ModelDb.GetByIdOrNull<CardModel>(ModelDb.GetId(HeroPowerType));
                if (heroPower != null)
                {
                    yield return new CardHoverTip(heroPower);
                }
            }
            // 战吼召唤的 3/6 水元素衍生物卡
            yield return new CardHoverTip(ModelDb.Card<WaterElementalCard>());
        }
    }

    /// <summary>
    /// 卡名不变
    /// </summary>
    public override string Title
    {
        get
        {
            var title = new LocString("cards", base.Id.Entry + ".title");
            return title.GetFormattedText();
        }
    }

    /// <summary>
    /// 战吼：召唤一个3/6的水元素；挂元素吸血光环（本局对战，你的所有元素拥有吸血）
    /// </summary>
    protected override async Task OnHeroBattlecry(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 召唤一个 3/6 的水元素
        await JainaMinionPool.SummonMinionByType(
            choiceContext,
            base.Owner,
            typeof(WaterElementalMinion),
            maxHp: 6m,
            attack: 3m,
            source: this);

        // 元素吸血光环：所有元素随从造成伤害时回复主人等量生命（每局打出一次即可，幂等）
        var combatState = base.Owner.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }
        var ownerCreature = base.Owner.Creature;
        if (!ownerCreature.Powers.Any(p => p is FrostLichJainaPower))
        {
            await PowerCmd.Apply<FrostLichJainaPower>(
                choiceContext, [ownerCreature], 1m, ownerCreature, this);
        }
    }
}
