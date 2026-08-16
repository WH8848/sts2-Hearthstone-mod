using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using jaina.Scripts.Character.Keywords;
using jaina.Scripts.Character.Minions;
using MinionLib.Minion;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 吉安娜地标卡基类（炉石传说式地标）。
/// 地标 = 动态"地标"卡牌类型，带"地标"与"耐久度"关键词。
/// 打出后召唤一个地标单位<b>占据一个随从槽位</b>：
/// - 每两个回合可点击使用一次触发效果（使用后冷却 2 回合）；
/// - 拥有耐久度：每次使用耐久度 -1，归零时地标被摧毁（离开战场）。
/// </summary>
public abstract class JainaLandmarkCardTemplate : ModCardTemplate
{
    /// <summary>
    /// 地标初始耐久度（每次使用 -1）
    /// </summary>
    public abstract int LandmarkDurability { get; }

    /// <summary>
    /// 该地标牌召唤的地标单位类型
    /// </summary>
    protected abstract Type LandmarkType { get; }

    /// <summary>
    /// 卡牌类型：动态注册的"地标"类型
    /// </summary>
    public override CardType Type => JainaCardTypes.Landmark;

    /// <summary>
    /// 地标卡不可升级
    /// </summary>
    public override int MaxUpgradeLevel => 0;

    /// <summary>
    /// 关键词：地标（占随从槽，每两个回合可点击使用一次）+ 耐久度（归零时卡牌被消耗）+ 消耗
    /// （地标卡打出后进入场上，卡牌本身消耗，不回弃牌堆）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [JainaKeywords.Landmark, JainaKeywords.Durability, CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Durability", LandmarkDurability)
    ];

    protected JainaLandmarkCardTemplate(int cost, CardRarity rarity)
        : base(cost, CardType.Skill, rarity, TargetType.Self, true)
    {
    }

    /// <summary>
    /// 满场（7 个随从/地标）时地标牌不可打出
    /// </summary>
    protected override bool IsPlayable
    {
        get
        {
            if (base.Owner == null)
            {
                return true;
            }
            return JainaMinionPool.GetCurrentMinionCount(base.Owner) < JainaMinionPool.MaxMinions;
        }
    }

    /// <summary>
    /// 打出：召唤地标单位站场（占据一个随从槽位）。
    /// 生命值 = 耐久度（地标生命值视觉显示耐久度；地标免疫伤害，耐久只在使用时消耗）。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await JainaMinionPool.SummonMinionByType(
            choiceContext,
            base.Owner,
            LandmarkType,
            maxHp: LandmarkDurability,
            position: MinionPosition.FrontUpper);
    }
}
