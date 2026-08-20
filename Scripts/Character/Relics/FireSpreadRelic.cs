using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Relics;

/// <summary>
/// 失火 (Fire Spread) - 吉安娜稀有遗物。
/// 每场战斗开始时，抽3张牌并附加保留与引燃
/// （保留：回合结束时不会弃置；引燃：3回合后消耗，见 <see cref="IgniteTracker"/>）。
/// </summary>
[RegisterRelic(typeof(JainaRelicPool))]
public sealed class FireSpreadRelic : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    /// <summary>
    /// 遗物图标：小图 85x85（程序绘制占位，卡图待 wiki 原画）
    /// </summary>
    public override string? CustomIconPath => "res://assets/relic_icons/fire_spread_icon.png";

    /// <summary>
    /// 遗物轮廓图标：85x85
    /// </summary>
    public override string? CustomIconOutlinePath => "res://assets/relic_icons/fire_spread_outline.png";

    /// <summary>
    /// 遗物大图：256x256
    /// </summary>
    public override string? CustomBigIconPath => "res://assets/relic_icons/fire_spread_big.png";

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    public override async Task BeforeCombatStart()
    {
        if (Owner.Creature.IsDead || Owner.Creature.CombatState == null)
        {
            return;
        }

        Flash();

        // 每场战斗开始时，抽3张牌
        var drawn = (await CardPileCmd.Draw(new ThrowingPlayerChoiceContext(), 3m, Owner)).ToList();
        // 给抽到的牌附加保留与引燃（保留：回合结束不弃；引燃：3回合后消耗）
        foreach (var card in drawn)
        {
            if (card == null || !card.IsMutable)
            {
                continue;
            }
            try
            {
                // 保留关键词（卡面显示"保留"，回合结束时留在手牌）
                card.AddKeyword(CardKeyword.Retain);
            }
            catch
            {
                // 附加失败不影响其余流程
            }
            // 引燃：3回合后消耗（记录附加回合，由 IgniteClockPower 每回合检查）
            IgniteTracker.ApplyIgnite(card, Owner);
        }
    }
}
