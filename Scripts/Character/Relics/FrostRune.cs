using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Relics;

/// <summary>
/// 冰霜符文 - 吉安娜的初始遗物。战斗开始时获得额外格挡，并激活随从军势能力。
/// </summary>
[RegisterRelic(typeof(JainaRelicPool))]
[RegisterCharacterStarterRelic(typeof(Jaina))]
public sealed class FrostRune : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Starter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(6m, ValueProp.Move)
    ];

    public override async Task BeforeCombatStart()
    {
        if (!Owner.Creature.IsDead)
        {
            Flash();
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block.BaseValue, ValueProp.Move, null);

            // 施加随从军势能力：当吉安娜护甲被打破时，随从按打出顺序抵挡伤害
            if (!Owner.Creature.Powers.Any(p => p is MinionSquadPower))
            {
                await PowerCmd.Apply<MinionSquadPower>(
                    new ThrowingPlayerChoiceContext(), Owner.Creature, 1m, Owner.Creature, null);
            }
        }
    }
}
