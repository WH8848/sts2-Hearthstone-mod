using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 顺水漂流力量跟踪（挂在被加力量的随从身上，隐藏）。
/// 顺水漂流（霜冻射线升级）选择友方随从时：力量实际加到<b>吉安娜</b>身上，
/// 同时在该随从身上挂本 Power——Amount = 该随从累计给吉安娜贡献的力量数。
/// 该随从死亡时：从吉安娜身上扣回 Amount 点力量（炉石语义：随从死亡，
/// 其提供的加成消失），随后本 Power 随生物消失。
/// 联机：Power 施加/死亡扣回为确定性命令，两端一致。
/// </summary>
[RegisterPower]
public sealed class GoWithTheFlowStrengthPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;

    /// <summary>
    /// 被顺水漂流加力量的随从死亡：从吉安娜身上扣回该随从贡献的力量
    /// （Amount = 累计贡献数；扣回后本 Power 随生物移除，力量加成不再保留）。
    /// </summary>
    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature,
        bool wasRemovalPrevented, float deathAnimLength)
    {
        if (creature != Owner)
        {
            return;
        }
        var petOwner = Owner.PetOwner;
        if (petOwner == null)
        {
            return;
        }
        if (Amount > 0)
        {
            await PowerCmd.Apply<StrengthPower>(choiceContext, [petOwner.Creature], -Amount, Owner, null);
        }
    }
}
