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
/// 正在撬动对手的回合结束按钮 (Prying the Opponent's End Turn Button) -
/// 吉安娜初始遗物"旗鼓相当的对手"的先古升级版（欧罗巴斯之触替换而来）。
/// 开始战斗：每场战斗开始时，获得一张幸运币，并额外抽 1 张牌。
/// </summary>
[RegisterRelic(typeof(JainaRelicPool))]
public sealed class EvenMatchAncient : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    /// <summary>
    /// 遗物图标：小图 85x85（与基础版同主题：幸运币原画裁剪）
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
        // 手牌满时 AddGeneratedCardToCombat 自动改道弃牌堆（原版满手语义，牌不消失不消耗）
        var combatState = Owner.Creature.CombatState;
        var canonical = MegaCrit.Sts2.Core.Models.ModelDb.GetByIdOrNull<MegaCrit.Sts2.Core.Models.CardModel>(
            MegaCrit.Sts2.Core.Models.ModelDb.GetId(typeof(LuckyCoin)));
        if (canonical == null)
        {
            return;
        }
        var coin = combatState.CreateCard(canonical, Owner);
        jaina.Scripts.Character.JainaCastTracker.MarkGenerated(coin);
        await CardPileCmd.AddGeneratedCardToCombat(coin, PileType.Hand, Owner);

        // 并额外抽 1 张牌
        await CardPileCmd.Draw(new ThrowingPlayerChoiceContext(), 1, Owner);
    }
}
