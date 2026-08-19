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
    /// 战斗视觉：卡德加卡图原画场景（puzzlemaster_khadgar.png——卡图与随从视觉共用）
    /// </summary>
    protected override string MinionVisualsPath => "res://assets/card_art/puzzlemaster_khadgar.png";

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
        // 战吼层无卡牌实例：传魔法智慧之球卡的 canonical 模板作为装备来源。
        // 不能传 null——JainaWeaponSlot.Equip 会因 weaponCard==null 静默不装备，
        // 导致球武器缺失：回合结束不施法、且 KhadgarOrbPower 因武器缺失被自移除（球消失）。
        var ballCard = MegaCrit.Sts2.Core.Models.ModelDb.Card<WondrousWisdomballCard>();
        await KhadgarOrbHelper.EquipOrb(choiceContext, owner, ballCard);
    }
}
