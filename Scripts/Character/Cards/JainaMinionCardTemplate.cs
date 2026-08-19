using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using jaina.Scripts.Character.Keywords;
using jaina.Scripts.Character.Minions;
using MinionLib.Minion;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.HoverTips;

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
    /// 公开访问器（JainaMinionCardMap 动态构建随从→卡映射用）
    /// </summary>
    public Type SummonedMinionType => MinionType;

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
    /// 额外悬停提示（子类按需追加自身特性：衍生物卡面、能力解释等）。
    /// "随从"关键词解释由模板兜底注入（格式同寒冰行者：随从 + 自身特性），
    /// 子类覆写本属性不会丢失"随从"解释。
    /// </summary>
    protected virtual IEnumerable<IHoverTip> ExtraMinionHoverTips => [];

    /// <summary>
    /// 悬停提示：固定包含"随从"关键词解释（使用时召唤、随从栏、7个上限等规则），
    /// 再追加子类自身特性提示（ExtraMinionHoverTips）。
    /// </summary>
    protected sealed override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromKeyword(DescriptionKeywords.Minion), .. ExtraMinionHoverTips];

    /// <summary>
    /// 描述后处理（MinionLib IDescriptionPostProcessCard）：
    /// 覆写属性时把卡面描述中的"攻击/生命"替换为覆写值（如 3/4 → 1/1）；
    /// 微型复制卡（带"微型"关键词）去掉描述中的"微缩"词条（微缩不再触发，卡面不应显示）。
    /// </summary>
    public string PostProcessDescription(string description, MegaCrit.Sts2.Core.Entities.Cards.PileType pileType,
        MinionLib.Utilities.BetterExtraArgs.DescriptionPreviewType previewType,
        MegaCrit.Sts2.Core.Entities.Creatures.Creature? target = null)
    {
        if (_overrideAttack is int atk && _overrideHealth is int hp)
        {
            description = description.Replace($"{MinionAttack}/{MinionHealth}", $"{atk}/{hp}");
        }
        if (Keywords.Contains(jaina.Scripts.Character.Keywords.JainaKeywords.Mini))
        {
            // 微型复制不再带"微缩"，描述中不再显示微缩词条（zhs/eng 两种格式）
            description = description
                .Replace("[gold]微缩[/gold]。\n", "")
                .Replace("[gold]Miniaturize[/gold].\n", "")
                .Replace("[gold]微缩[/gold]。", "")
                .Replace("[gold]Miniaturize[/gold].", "");
        }
        return description;
    }

    /// <summary>
    /// 打出带"微缩"关键词的随从牌后（仅从手牌打出触发；自动打出——召唤/复活登场——
    /// 不触发）：立即将一张 0 费 1/1 的复制（微型）置入你的手牌。
    /// 覆写 AfterCardPlayed 而非 Hook Postfix + 串行队列：与惊奇卡牌同因——
    /// Postfix fire-and-forget 与 networked 动作/checksum 生成点竞态
    /// （一端先入手复制品、另一端后入手 → 手牌分歧 → StateDivergence 断联）。
    /// 原版"打出后触发"的监听者（如女妖之嚎）都在 networked 钩子链内阻塞执行。
    /// 微型复制品完整保留原卡牌的所有文字效果，带"微型"关键词、去掉"微缩"（不再触发），
    /// 不消耗（打出后进弃牌堆，可再次抽回）。
    /// </summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // Hook 遍历所有监听者：每张随从牌都会收到事件，只响应"打出的是本卡"
        if (cardPlay.Card != this)
        {
            return;
        }
        // 只在从手牌使用时触发（自动打出——召唤/复活等——不触发）
        if (cardPlay.IsAutoPlay)
        {
            return;
        }
        // 只对带"微缩"关键词的随从牌生效
        if (!Keywords.Contains(jaina.Scripts.Character.Keywords.JainaKeywords.Miniaturize))
        {
            return;
        }
        try
        {
            var player = Owner;
            if (player == null || player.Creature?.CombatState == null)
            {
                return;
            }
            var combatState = player.Creature.CombatState;

            // 手牌满时 AddGeneratedCardToCombat 自动改道弃牌堆（原版满手语义，牌不消失不消耗）

            // 生成 0 费 1/1 的微型复制品（保留升级级别与全部文字效果）
            var copy = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
                combatState, player, GetType(), CurrentUpgradeLevel);
            if (copy == null)
            {
                return;
            }
            copy.EnergyCost.SetCustomBaseCost(0);
            if (copy is JainaMinionCardTemplate minionCard)
            {
                minionCard.SetOverrideStats(1, 1);
            }
            // 关键词：去掉"微缩"（微型不再触发微缩），加上"微型"；不打消耗标记
            copy.RemoveKeyword(jaina.Scripts.Character.Keywords.JainaKeywords.Miniaturize);
            copy.AddKeyword(jaina.Scripts.Character.Keywords.JainaKeywords.Mini);
            copy.RemoveKeyword(CardKeyword.Exhaust);

            jaina.Scripts.Character.JainaCastTracker.MarkGenerated(copy);
            await CardPileCmd.AddGeneratedCardToCombat(copy, PileType.Hand, player);
        }
        catch (System.Exception ex)
        {
            MegaCrit.Sts2.Core.Logging.Log.Error($"[Jaina] Miniaturize trigger failed: {ex}");
        }
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
    /// 自动打出（如诈骗犯重放随从牌）同样只召唤、不触发战吼——炉石规则：
    /// 非从手牌打出不触发战吼（cardPlay.IsAutoPlay 时不传 source）。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        MegaCrit.Sts2.Core.Logging.Log.Info($"[JainaDiag] MinionCard OnPlay: {GetType().Name} autoPlay={cardPlay.IsAutoPlay} minionType={MinionType.Name}");
        LastSummonedMinion = await JainaMinionPool.SummonMinionByType(
            choiceContext,
            base.Owner,
            MinionType,
            maxHp: StandardMinionHealth,
            attack: StandardMinionAttack,
            position: MinionPosition,
            source: cardPlay.IsAutoPlay ? null : this);
        // 记录施放（诈骗犯重放"上一张"用——随从卡可被诈骗犯重放，重放只召唤不触发战吼）。
        // 必须放在召唤（战吼触发）之后：诈骗犯的战吼在召唤中读取"上一张"——
        // 若先记录自己再触发战吼，战吼读到的是刚打出的诈骗犯自己 → 重放诈骗犯
        // （bug："火妖打出后打诈骗犯，诈骗犯重放诈骗犯而不是火妖"）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);
        MegaCrit.Sts2.Core.Logging.Log.Info($"[JainaDiag] MinionCard summon result: {GetType().Name} summoned={(LastSummonedMinion != null)}");
    }
}