using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Relics;

/// <summary>
/// 魔导师学徒 (Apprentice of the Magi) - 吉安娜稀有遗物。
/// 每回合开始，你的第一张奥术法术耗费减少1点。
/// 战斗开始由 <see cref="ApprenticeOfTheMagiPower"/> 每回合驱动（幂等挂载）。
/// </summary>
[RegisterRelic(typeof(JainaRelicPool))]
public sealed class ApprenticeOfTheMagiRelic : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    /// <summary>
    /// 遗物图标：小图 85x85（程序绘制占位，卡图待 wiki 原画）
    /// </summary>
    public override string? CustomIconPath => "res://assets/relic_icons/apprentice_of_the_magi_icon.png";

    /// <summary>
    /// 遗物轮廓图标：85x85
    /// </summary>
    public override string? CustomIconOutlinePath => "res://assets/relic_icons/apprentice_of_the_magi_outline.png";

    /// <summary>
    /// 遗物大图：256x256
    /// </summary>
    public override string? CustomBigIconPath => "res://assets/relic_icons/apprentice_of_the_magi_big.png";

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    /// <summary>
    /// 战斗开始：给玩家挂魔导师学徒（每回合第一张奥术法术 -1 费，幂等）
    /// </summary>
    public override async Task BeforeCombatStart()
    {
        if (Owner.Creature.IsDead || Owner.Creature.CombatState == null)
        {
            return;
        }
        await ApprenticeOfTheMagiPower.EnsureAppliedAsync(new ThrowingPlayerChoiceContext(), Owner);
    }
}
