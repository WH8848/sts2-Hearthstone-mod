using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Cards;
using jaina.Scripts.Character.Weapons;
using MinionLib.Targeting;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 魔法智慧之球效果：在你的回合结束时，随机施放一个有用的法师法术
/// （火球术/寒冰箭/烈焰风暴/暴风雪/法术反制/寒冰护盾），随后失去 1 点耐久度。
/// 耐久度为 0 时武器能力消失（球效果一并移除）。
/// 挂在玩家身上，装备魔法智慧之球时施加。可见（能力图标）。
/// </summary>
[RegisterPower]
public sealed class KhadgarOrbPower : PowerModel, IModPowerAssetOverrides
{
    /// <inheritdoc />
    public PowerAssetProfile AssetProfile => new("res://assets/power_icons/jaina_power_khadgar_orb_power.png");

    /// <inheritdoc />
    public string? CustomIconPath => AssetProfile.IconPath;

    /// <inheritdoc />
    public string? CustomBigIconPath => AssetProfile.BigIconPath;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => true;

    /// <summary>
    /// 玩家回合结束时：随机施放一个有用的法师法术（免费自动打出，随机目标），
    /// 然后失去 1 点耐久度（0 时武器能力消失，球效果移除）。
    /// </summary>
    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext,
        MegaCrit.Sts2.Core.Combat.CombatSide side, IEnumerable<Creature> participants)
    {
        var player = Owner?.Player;
        MegaCrit.Sts2.Core.Logging.Log.Info($"[JainaDiag] KhadgarOrb BeforeSideTurnEnd: side={side} ownerNull={Owner == null} playerNull={player == null} ownerName={(Owner != null ? Owner.Name : "?")}");
        if (player == null || side != MegaCrit.Sts2.Core.Combat.CombatSide.Player)
        {
            return;
        }
        var weapon = Owner.Powers.OfType<JainaWeaponPower>().FirstOrDefault();
        MegaCrit.Sts2.Core.Logging.Log.Info($"[JainaDiag] KhadgarOrb weaponNull={weapon == null} powerCount={Owner.Powers.Count}");
        if (weapon == null)
        {
            // 武器已消失（被顶替/耐久耗尽）：球效果一并移除
            await PowerCmd.Remove(this);
            return;
        }

        MegaCrit.Sts2.Core.Logging.Log.Info("[JainaDiag] KhadgarOrb BeforeSideTurnEnd: casting mage spell");
        // 随机施放一个有用的法师法术
        await MageSpellCaster.CastRandomMageSpell(choiceContext, player);

        // 失去 1 点耐久度；耐久为 0 时武器能力消失（球效果一并移除）
        await JainaWeaponSlot.ConsumeDurability(choiceContext, Owner, weapon);
        if (weapon.Amount <= 0)
        {
            await PowerCmd.Remove(this);
        }
    }
}
