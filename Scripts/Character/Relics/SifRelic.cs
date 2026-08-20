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
/// 西芙 (Sif) - 吉安娜商店遗物。
/// 力量+1。在本局对战中，你每施放过一个派系的法术都会提升
/// （火焰/冰霜/奥术三个派系各首次施放时，力量再+1）。
/// 由 <see cref="SifPower"/> 在战斗开始施加基础力量并追踪派系施放。
/// </summary>
[RegisterRelic(typeof(JainaRelicPool))]
public sealed class SifRelic : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Shop;

    /// <summary>
    /// 遗物图标：小图 85x85（wiki 原画裁剪）
    /// </summary>
    public override string? CustomIconPath => "res://assets/relic_icons/sif_icon.png";

    /// <summary>
    /// 遗物轮廓图标：85x85
    /// </summary>
    public override string? CustomIconOutlinePath => "res://assets/relic_icons/sif_outline.png";

    /// <summary>
    /// 遗物大图：256x256
    /// </summary>
    public override string? CustomBigIconPath => "res://assets/relic_icons/sif_big.png";

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    public override async Task BeforeCombatStart()
    {
        if (Owner.Creature.IsDead || Owner.Creature.CombatState == null)
        {
            return;
        }

        Flash();

        // 战斗开始：施加基础力量+1，并挂载派系施放追踪（幂等）
        await SifPower.EnsureAppliedAsync(new ThrowingPlayerChoiceContext(), Owner);
    }
}
