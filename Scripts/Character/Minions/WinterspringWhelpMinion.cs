using System.Threading.Tasks;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using jaina.Scripts.Character.Cards;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 冬泉雏龙 (Winterspring Whelp) - 吉安娜专属随从。
/// 属性：攻击 1，生命 2。
/// 战吼：发现一张任意角色的费用为0的卡牌（三选一，可跳过；加入手牌）。
/// </summary>
[RegisterMonster]
public sealed class WinterspringWhelpMinion : JainaMinionBase
{
    /// <summary>
    /// 战斗视觉：冬泉雏龙卡图原画场景
    /// </summary>
    protected override string MinionVisualsPath => "res://assets/card_art/winterspring_whelp.png";

    public override int MinInitialHp => 2;

    public override int MaxInitialHp => 2;

    /// <summary>
    /// 战吼：发现一张任意角色的费用为0的卡牌（三选一，可跳过；加入手牌）。
    /// 全角色池（ModelDb.AllCards，应用 Jaina 随机池统一排除）；
    /// 同名卡不可自发现（排除冬泉雏龙自身）。
    /// </summary>
    public override async Task OnBattlecry(PlayerChoiceContext choiceContext)
    {
        MegaCrit.Sts2.Core.Logging.Log.Info("[JainaDiag] WinterspringWhelp OnBattlecry triggered");
        var owner = Creature.PetOwner;
        if (owner == null)
        {
            MegaCrit.Sts2.Core.Logging.Log.Warn("[JainaDiag] WinterspringWhelp PetOwner null");
            return;
        }
        var ownCardType = jaina.Scripts.Character.Minions.JainaMinionCardMap.GetCardType(GetType());
        // excludeXCost：发现池中不出现 X 费卡（禁忌烈焰/禁忌神龛等）——它们打出时
        // 消耗全部能量，不是 0 费卡，不应出现在"0 费卡"发现池中
        await JainaDiscoverHelper.DiscoverCardOfCostAndAddToHand(
            choiceContext, owner, 0, ownCardType, allClasses: true, excludeXCost: true);
    }
}
