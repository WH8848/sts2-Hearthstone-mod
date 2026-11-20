using System;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 泰瑟兰·血望者 (Tesslan Bloodwatcher) - 2/5 随从。
/// 你的套牌之外的卡牌耗能减少 1 点。
/// </summary>
[RegisterMonster]
public sealed class TesslanBloodwatcherMinion : JainaMinionBase
{
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 5;

    public override int MaxInitialHp => 5;

    protected override string MinionVisualsPath => "res://assets/card_art/iceblood_tower.png";

    /// <summary>
    /// DeckVersion 由游戏在战斗开始时指向套牌中的原卡，牌库之外生成的战斗卡没有该引用。
    /// 依据：sts2/0.113.0/src/Core/Entities/Players/Player.cs:813-820。
    /// </summary>
    public override bool TryModifyEnergyCostInCombat(
        CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (!Creature.IsAlive)
        {
            return false;
        }

        var owner = Creature.PetOwner;
        if (owner == null || card.Owner != owner || card.DeckVersion != null)
        {
            return false;
        }

        modifiedCost = Math.Max(0, originalCost - 1);
        return true;
    }
}
