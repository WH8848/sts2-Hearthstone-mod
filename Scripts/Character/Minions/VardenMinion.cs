using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using jaina.Scripts.Character.Powers;
using MinionLib.Minion;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 瓦尔登·晨拥 (Varden Dawngrasp) - 吉安娜专属随从。
/// 属性：攻击 3，生命 3。战吼：给予敌方全体 7 层冻结。如果敌方已被冻结，则每层冻结对其造成 4 点伤害。
/// </summary>
[RegisterMonster]
public sealed class VardenMinion : JainaMinionBase
{
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 3;

    public override int MaxInitialHp => 3;

    protected override string MinionVisualsPath => "res://assets/card_art/varden_dawngrasp.png";

    /// <summary>
    /// 战吼：先按现有冻结层数结算伤害，再给予全体敌人 7 层冻结。
    /// 仅手牌打出时触发，随机召唤不触发。
    /// </summary>
    public override async Task OnBattlecry(PlayerChoiceContext choiceContext)
    {
        var owner = Creature.PetOwner;
        if (owner == null || Creature.CombatState == null)
        {
            return;
        }
        var enemies = Creature.CombatState.GetOpponentsOf(Creature).Where(e => e != null && e.IsAlive).ToList();
        foreach (var enemy in enemies)
        {
            // 已冻结的：每层冻结造成 4 点伤害
            var existing = enemy.GetPower<FreezePower>();
            if (existing != null && existing.Amount > 0)
            {
                await CreatureCmd.Damage(choiceContext, [enemy], existing.Amount * 4m, ValueProp.Unpowered, Creature);
            }
            // 给予 7 层冻结（无视人工制品：瓦尔登的冻结不被人工制品阻挡）
            FreezePower.BypassArtifactNextApply = true;
            try
            {
                await PowerCmd.Apply<FreezePower>(choiceContext, [enemy], 7m, owner.Creature, null);
            }
            finally
            {
                FreezePower.BypassArtifactNextApply = false;
            }
        }
    }
}
