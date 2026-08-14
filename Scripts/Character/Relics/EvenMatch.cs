using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using jaina.Scripts.Character.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Relics;

/// <summary>
/// 旗鼓相当的对手 (Even Match) - 吉安娜的初始遗物。
/// 开始战斗：每场战斗开始时，获得一张幸运币（0费：获得 1 点能量，保留）。
/// </summary>
[RegisterRelic(typeof(JainaRelicPool))]
[RegisterCharacterStarterRelic(typeof(Jaina))]
[RegisterTouchOfOrobasRefinement(typeof(EvenMatchAncient))]
public sealed class EvenMatch : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Starter;

    /// <summary>
    /// 遗物图标：小图 85x85（幸运币原画裁剪）
    /// </summary>
    public override string? CustomIconPath => "res://assets/relic_icons/even_match_icon.png";

    /// <summary>
    /// 遗物轮廓图标：85x85
    /// </summary>
    public override string? CustomIconOutlinePath => "res://assets/relic_icons/even_match_outline.png";

    /// <summary>
    /// 遗物大图：256x256
    /// </summary>
    public override string? CustomBigIconPath => "res://assets/relic_icons/even_match_big.png";

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    public override async Task BeforeCombatStart()
    {
        if (Owner.Creature.IsDead || Owner.Creature.CombatState == null)
        {
            return;
        }

        Flash();

        // 每场战斗开始时，获得一张幸运币（加入手牌）
        // MutableClone 的卡无 Owner 会 NRE，用 CreateCard 生成带 Owner 的实例
        var combatState = Owner.Creature.CombatState;
        var coin = combatState.CreateCard(
            (MegaCrit.Sts2.Core.Models.CardModel)MegaCrit.Sts2.Core.Models.ModelDb.GetById<LuckyCoin>(
                MegaCrit.Sts2.Core.Models.ModelDb.GetId(typeof(LuckyCoin))),
            Owner);
        jaina.Scripts.Character.JainaCastTracker.MarkGenerated(coin);
        await CardPileCmd.AddGeneratedCardToCombat(coin, PileType.Hand, Owner);
    }
}
