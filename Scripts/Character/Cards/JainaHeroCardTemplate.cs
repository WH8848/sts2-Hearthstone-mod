using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 吉安娜英雄卡基类（炉石传说式英雄卡）。
/// 英雄卡打出时：
/// 1. 获得护甲值（<see cref="HeroArmor"/>）；
/// 2. 触发英雄战吼（子类覆写 <see cref="OnHeroBattlecry"/>）；
/// 3. 用英雄卡的英雄技能替换现有英雄技能（<see cref="HeroPowerType"/> 指向新英雄技能卡类型）；
/// 4. 更改角色模型（目前无其它模型，使用铁甲战士模型代替——视觉保持原角色，机制预留）。
/// 卡牌类型为动态注册的"英雄"类型（JainaCardTypes.Hero），
/// 显示文本与卡框由 JainaCardTypePatches 映射为能力样式。
/// </summary>
public abstract class JainaHeroCardTemplate : ModCardTemplate
{
    /// <summary>
    /// 打出时获得的护甲值
    /// </summary>
    protected abstract int HeroArmor { get; }

    /// <summary>
    /// 替换后的英雄技能卡类型（null = 不替换英雄技能）
    /// </summary>
    protected virtual System.Type? HeroPowerType => null;

    /// <summary>
    /// 英雄战吼（子类实现）
    /// </summary>
    protected abstract Task OnHeroBattlecry(PlayerChoiceContext choiceContext, CardPlay cardPlay);

    /// <summary>
    /// 卡牌类型：动态注册的"英雄"类型
    /// </summary>
    public override CardType Type => JainaCardTypes.Hero;

    /// <summary>
    /// 悬停提示：显示本英雄卡的英雄技能卡（炉石式：英雄卡悬停展示其英雄技能）。
    /// 未指定英雄技能（HeroPowerType == null）的英雄卡无此提示。
    /// </summary>
    protected override IEnumerable<IHoverTip> AdditionalHoverTips
    {
        get
        {
            if (HeroPowerType == null)
            {
                yield break;
            }
            var heroPower = ModelDb.GetByIdOrNull<CardModel>(ModelDb.GetId(HeroPowerType));
            if (heroPower != null)
            {
                yield return new CardHoverTip(heroPower);
            }
        }
    }

    /// <summary>
    /// 英雄卡默认不可升级（MaxUpgradeLevel=0 → IsUpgradable=false，
    /// 升级界面/升级遗物不会把英雄卡列为可升级候选）。
    /// 有升级形态的英雄卡子类覆写 MaxUpgradeLevel 恢复可升级。
    /// </summary>
    public override int MaxUpgradeLevel => 0;

    protected JainaHeroCardTemplate(int cost, CardRarity rarity)
        : base(cost, CardType.Power, rarity, TargetType.Self, true)
    {
    }

    /// <summary>
    /// 打出：获得护甲 → 触发英雄战吼 → 替换英雄技能（模型切换预留：目前使用铁甲战士模型代替）
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        // 1. 获得护甲值
        if (HeroArmor > 0)
        {
            await CreatureCmd.GainBlock(base.Owner.Creature, HeroArmor, ValueProp.Unpowered, cardPlay);
        }

        // 2. 触发英雄战吼
        await OnHeroBattlecry(choiceContext, cardPlay);

        // 3. 替换英雄技能（若指定了新的英雄技能卡类型）
        if (HeroPowerType != null)
        {
            var combatState = base.Owner.Creature.CombatState;
            if (combatState == null)
            {
                return;
            }
            var rec = jaina.Scripts.Character.JainaCastTracker.For(combatState);
            // 英雄技能已是指定的新技能（如重复打出同一张英雄卡）：不重复创建
            if (rec.CurrentHeroPowerType == HeroPowerType)
            {
                return;
            }
            rec.CurrentHeroPowerType = HeroPowerType;

            // 创建新英雄技能卡实例并加入手牌（英雄技能卡不占手牌位）。
            // 与火焰冲击一致：不标记"衍生"（无蓝光、不计入牌库外法术计数），
            // 之后每回合由该卡自己的 BeforeHandDraw 重新入手。
            var heroPower = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
                combatState, base.Owner, HeroPowerType, 0);
            if (heroPower != null)
            {
                await CardPileCmd.AddGeneratedCardToCombat(heroPower, PileType.Hand, base.Owner);
            }
        }

        // 4. 更改角色模型：目前无其它模型，使用铁甲战士模型代替（占位，机制预留）
    }
}
