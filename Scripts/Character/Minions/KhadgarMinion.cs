using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using jaina.Scripts.Character.Powers;
using jaina.Scripts.Character.Weapons;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 益智大师卡德加 (Khadgar) - 吉安娜专属随从。
/// 属性：攻击 5，生命 5。
/// 战吼：装备一个 0/6 的魔法智慧之球（每回合结束随机施放一个有用的法师法术，失去1点耐久度）。
/// </summary>
[RegisterMonster]
public sealed class KhadgarMinion : JainaMinionBase
{
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 5;

    public override int MaxInitialHp => 5;

    /// <summary>
    /// 战斗视觉：卡德加卡图原画场景
    /// </summary>
    protected override string MinionVisualsPath => "res://assets/card_art/khadgar.png";

    /// <summary>
    /// 战吼：装备魔法智慧之球（0/6 武器 + 回合结束施法效果）。仅手牌打出时触发。
    /// </summary>
    public override async Task OnBattlecry(PlayerChoiceContext choiceContext)
    {
        var owner = Creature.PetOwner;
        if (owner == null)
        {
            return;
        }
        // 战吼层无卡牌实例，source 传 null（仅用于记录，无卡牌依赖）
        await KhadgarOrbHelper.EquipOrb(choiceContext, owner, null);
    }
}
