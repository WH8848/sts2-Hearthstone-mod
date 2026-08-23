using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using jaina.Scripts.Character.Keywords;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 拨号机器人 (Robocaller) - 1费随从卡（普通）。属性 3/2。
/// [gold]保留[/gold]：手牌中不会在回合结束时被弃置。
/// 每回合开始时随机拨号（三个 0~3 的数字，可重复），卡面上的拨号数字同步更新；
/// 战吼：从抽牌堆定向抽取费用消耗等于当前拨号数字的牌各一张（每回合随机拨号！）。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class RobocallerCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 当前拨号结果（三个 0~5 的数字；每回合开始时随机拨号并更新卡面显示）
    /// </summary>
    public int[] CurrentDials { get; private set; } = new int[3];

    /// <summary>
    /// 卡牌原画：炉石传说"拨号机器人"（Robocaller, 110757）官方原画；
    /// 升级后切换为 Signature 异画版
    /// </summary>
    public override string CustomPortraitPath =>
        IsUpgraded
            ? "res://assets/card_art/robocaller_signature.png"
            : "res://assets/card_art/robocaller.png";

    /// <summary>
    /// 机械种族 + 战吼（悬停解释）+ 保留（手牌不弃置）+ 消耗（随从卡打出后消耗，模板默认）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [JainaKeywords.Mech, JainaKeywords.Battlecry, CardKeyword.Retain, CardKeyword.Exhaust];

    /// <summary>
    /// 拨号数字动态变量：卡面 {Roll0}/{Roll1}/{Roll2} 跟随当前拨号结果变化
    /// </summary>
    protected override IEnumerable<MegaCrit.Sts2.Core.Localization.DynamicVars.DynamicVar> CanonicalVars =>
    [
        ModCardVars.Computed("Roll0", 0m, card => card is RobocallerCard rc ? rc.CurrentDials[0] : 0m),
        ModCardVars.Computed("Roll1", 0m, card => card is RobocallerCard rc ? rc.CurrentDials[1] : 0m),
        ModCardVars.Computed("Roll2", 0m, card => card is RobocallerCard rc ? rc.CurrentDials[2] : 0m)
    ];

    protected override Type MinionType => typeof(RobocallerMinion);

    protected override int MinionAttack => 3;

    protected override int MinionHealth => 2;

    public RobocallerCard()
        : base(1, CardRarity.Common)
    {
    }

    /// <summary>
    /// 每回合开始时（主人回合）随机拨号：三个 0~3 的数字（可重复），
    /// 更新卡面拨号数字显示。用共享 RNG（联机两端确定性一致）。
    /// </summary>
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != base.Owner)
        {
            return;
        }
        var rng = player.RunState.Rng.CombatTargets;
        for (int i = 0; i < 3; i++)
        {
            CurrentDials[i] = rng.NextInt(0, 4);
        }
        await Task.CompletedTask;
    }
}
