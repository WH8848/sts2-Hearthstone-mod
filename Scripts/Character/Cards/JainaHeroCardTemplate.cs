using System.Collections.Generic;
using System.Linq;
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
    /// 关键词：战吼（打出英雄卡触发英雄战吼）。
    /// 挂 Battlecry 关键词后，悬停英雄卡时右侧显示"战吼"词条注释
    /// （游戏原版 CardModel.HoverTips 对卡上 Keywords 自动生成悬停解释）。
    /// 注意：不用 Exhaust 关键词——英雄卡是自定义 Hero 类型，原版打出后判定
    /// （GetResultLocationForCardPlay）对 Hero 类型不识别 Exhaust（实测进了弃牌堆）；
    /// 改为覆写 GetResultLocationForCardPlay 返回 PileType.None（与能力牌 Power 类型
    /// 同一路径：打出后从战斗移除，不再进入弃牌堆被抽回——炉石英雄卡一次性）。
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Battlecry];

    /// <summary>
    /// 打出后移除（像能力牌一样）：英雄卡打出后从战斗移除（PileType.None →
    /// RemoveFromCombat），不进弃牌堆。能力牌（Power 类型）由原版
    /// GetResultLocationForCardPlay 的 Type==Power 分支处理；英雄卡是自定义
    /// Hero 类型，需显式覆写（原版先例：ParticleWall/ShiningStrike/TheBall）。
    /// </summary>
    protected override CardLocation GetResultLocationForCardPlay()
    {
        return new CardLocation(Owner, PileType.None, CardPilePosition.Bottom);
    }

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

        // 2. 触发英雄战吼（自动打出——如诈骗犯重放英雄卡——不触发战吼，
        //    只获得护盾与英雄技能替换，与炉石"非手牌打出不触发战吼"一致）
        if (!cardPlay.IsAutoPlay)
        {
            await OnHeroBattlecry(choiceContext, cardPlay);
        }

        // 3. 替换英雄技能（若指定了新的英雄技能卡类型）
        if (HeroPowerType != null)
        {
            var combatState = base.Owner.Creature.CombatState;
            if (combatState == null)
            {
                return;
            }
            var rec = jaina.Scripts.Character.JainaCastTracker.For(combatState);
            // 英雄技能已是指定的新技能（如重复打出同一张英雄卡）：不重复创建（按玩家区分）
            rec.CurrentHeroPowerTypeByPlayer.TryGetValue(base.Owner.NetId, out var currentHeroPowerType);
            if (currentHeroPowerType == HeroPowerType)
            {
                return;
            }

            // 立刻替换：先从所有战斗牌堆移除旧英雄技能卡（火焰冲击/二级火焰冲击/
            // 上一张英雄卡的技能——弃牌堆/抽牌堆里的旧技能卡也要清掉，否则洗牌后会被抽到），
            // 再置入新英雄技能（避免新旧英雄技能同时存在）
            var oldHeroPowers = new List<CardModel>();
            foreach (var pileType in new[] { PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust, PileType.Play })
            {
                var pile = pileType.GetPile(base.Owner);
                if (pile == null)
                {
                    continue;
                }
                foreach (var card in pile.Cards)
                {
                    if (card != null && card.CanonicalKeywords?.Contains(jaina.Scripts.Character.Keywords.JainaKeywords.HeroPower) == true)
                    {
                        oldHeroPowers.Add(card);
                    }
                }
            }
            if (oldHeroPowers.Count > 0)
            {
                // 注意：不能用 skipVisuals=true——RemoveFromCombat 在 skipVisuals 时会跳过
                // NCard 手牌节点的查找与移除（list 为空 → UI 移除逻辑整体跳过），
                // 导致卡模型已移除但手牌 UI 上旧英雄技能卡仍然显示
                await CardPileCmd.RemoveFromCombat(oldHeroPowers, skipVisuals: false);
            }

            rec.CurrentHeroPowerTypeByPlayer[base.Owner.NetId] = HeroPowerType;

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
