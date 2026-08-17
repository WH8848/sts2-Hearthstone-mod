using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using jaina.Scripts.Character.Keywords;
using jaina.Scripts.Character.Minions;
using MinionLib.Minion;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 吉安娜随从牌基类（炉石传说风格）。
/// 打出后召唤一个随从生物留在场上（随从回合结束自动攻击敌人）。
/// 卡面通过关键词（亡语/冲锋）自动注入文本，描述中展示随从的攻击/生命属性。
/// 卡牌类型为动态注册的"随从"类型（JainaCardTypes.Minion），
/// 显示文本由 ToLocString patch 提供，卡框/边框由 FramePath 等 patch 映射为技能样式。
/// 手牌发光（仅对局内衍生卡显示浓天蓝）由 JainaHandGlowPatch 统一处理。
/// </summary>
public abstract class JainaMinionCardTemplate : ModCardTemplate,
    MinionLib.Utilities.DescriptionPostProcess.IDescriptionPostProcessCard
{
    /// <summary>
    /// 该随从牌召唤的随从生物类型
    /// </summary>
    protected abstract Type MinionType { get; }

    /// <summary>
    /// 随从攻击力
    /// </summary>
    protected abstract int MinionAttack { get; }

    /// <summary>
    /// 随从生命值
    /// </summary>
    protected abstract int MinionHealth { get; }

    /// <summary>
    /// 覆写攻击/生命（幻觉药水的 1/1 复制用；null = 使用卡面标准值）
    /// </summary>
    private int? _overrideAttack;
    private int? _overrideHealth;

    /// <summary>
    /// 设置覆写属性（复制卡召唤时使用，如 1/1）
    /// </summary>
    public void SetOverrideStats(int? attack, int? health)
    {
        _overrideAttack = attack;
        _overrideHealth = health;
    }

    /// <summary>
    /// 标准攻击力（供 JainaMinionPool 等外部按卡面属性召唤时读取；含覆写）
    /// </summary>
    public int StandardMinionAttack => _overrideAttack ?? MinionAttack;

    /// <summary>
    /// 标准生命值（供 JainaMinionPool 等外部按卡面属性召唤时读取；含覆写）
    /// </summary>
    public int StandardMinionHealth => _overrideHealth ?? MinionHealth;

    /// <summary>
    /// 描述后处理（MinionLib IDescriptionPostProcessCard）：
    /// 覆写属性时把卡面描述中的"攻击/生命"替换为覆写值（如 3/4 → 1/1）
    /// </summary>
    public string PostProcessDescription(string description, MegaCrit.Sts2.Core.Entities.Cards.PileType pileType,
        MinionLib.Utilities.BetterExtraArgs.DescriptionPreviewType previewType,
        MegaCrit.Sts2.Core.Entities.Creatures.Creature? target = null)
    {
        if (_overrideAttack is int atk && _overrideHealth is int hp)
        {
            description = description.Replace($"{MinionAttack}/{MinionHealth}", $"{atk}/{hp}");
        }
        return description;
    }

    /// <summary>
    /// 随从站场位置（默认玩家前方上方区域）
    /// </summary>
    protected virtual MinionPosition MinionPosition => MinionPosition.FrontUpper;

    /// <summary>
    /// 卡牌类型：动态注册的"随从"类型
    /// </summary>
    public override CardType Type => JainaCardTypes.Minion;

    /// <summary>
    /// 随从卡默认可升级 1 次（取消不可升级限制）；升级后去除消耗词条
    /// （特殊随从如奥术师晨拥覆写 OnUpgrade 为空以保留消耗）。
    /// </summary>
    public override int MaxUpgradeLevel => 1;

    /// <summary>
    /// 升级：去除消耗词条（LocalKeywords 懒初始化只算一次，
    /// 升级形态 Keywords 缓存自基础状态——需显式移除 Exhaust，否则升级后卡面仍显示"消耗"）。
    /// </summary>
    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Exhaust);
    }

    /// <summary>
    /// 关键词：默认无（子类按需声明亡语/冲锋等）。
    /// 通过 CanonicalKeywords 声明（而非构造函数 AddKeyword），
    /// 避免修改游戏创建的 canonical 不可变实例导致 CanonicalModelException。
    /// 注册了 CardDescriptionPlacement.BeforeCardDescription 的关键词
    /// 会自动将其金色 BBCode 注入到卡面描述之前。
    /// 关键词悬停解释由游戏原版 CardModel.HoverTips 自动生成，无需手动遍历。
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    /// <summary>
    /// 本卡最近一次打出时召唤的随从生物（艾格文亡语转移光环用）
    /// </summary>
    public MegaCrit.Sts2.Core.Entities.Creatures.Creature? LastSummonedMinion { get; private set; }

    protected JainaMinionCardTemplate(int cost, CardRarity rarity)
        : base(cost, CardType.Skill, rarity, TargetType.Self, true)
    {
    }

    /// <summary>
    /// 满场（7 个随从）时随从牌不可打出：
    /// UI 显示不可用，拖出尝试会被游戏弹回（不消耗卡），并弹出无法打出的提示气泡。
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
    /// 打出：召唤随从生物站场。随从由 MinionLib 管理，回合结束自动攻击敌人。
    /// source 传本卡实例——随从 OnSummon 据此判断"从手牌打出"，触发战吼
    /// （随机召唤/效果召唤不传 source，不触发战吼，炉石规则）。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        LastSummonedMinion = await JainaMinionPool.SummonMinionByType(
            choiceContext,
            base.Owner,
            MinionType,
            maxHp: StandardMinionHealth,
            attack: StandardMinionAttack,
            position: MinionPosition,
            source: this);
    }
}