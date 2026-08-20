using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 灌注：第一次打出灌注卡牌（灵体采集者/小精灵驾驭者）会将你的英雄技能
/// 替换为"小精灵的祝福"；此后每层灌注都会让英雄技能额外释放一次
/// （小精灵的祝福释放次数 = 灌注层数，见 <see cref="Cards.BlessingOfImpsCard"/>）。
/// 挂在吉安娜玩家身上，由灵体采集者等卡施加。
/// </summary>
[RegisterPower]
public sealed class EmpowerPower : PowerModel, IModPowerAssetOverrides
{
    /// <inheritdoc />
    public PowerAssetProfile AssetProfile => new("res://assets/power_icons/jaina_power_empower_power.png");

    /// <inheritdoc />
    public string? CustomIconPath => AssetProfile.IconPath;

    /// <inheritdoc />
    public string? CustomBigIconPath => AssetProfile.BigIconPath;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 灌注层数（由小精灵的祝福读取：释放次数 = 层数）
    /// </summary>
    public int EmpowerStacks => (int)Amount;

    /// <summary>
    /// 第一次灌注（层数 0 → 1）：将玩家的英雄技能替换为"小精灵的祝福"。
    /// 此后每层灌注不再替换（已替换），只增加层数（祝福释放次数随之 +1）。
    /// 联机：PowerCmd.Apply 两端确定性执行，替换流程两端一致。
    /// </summary>
    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        if (EmpowerStacks != 1)
        {
            return; // 仅第一次灌注触发替换
        }
        var player = Owner.Player;
        if (player == null || Owner.IsDead)
        {
            return;
        }
        var combatState = Owner.CombatState;
        if (combatState == null)
        {
            return;
        }
        var rec = jaina.Scripts.Character.JainaCastTracker.For(combatState);
        // 英雄技能已是小精灵的祝福（重复触发防御）：不重复替换
        if (rec.CurrentHeroPowerTypeByPlayer.TryGetValue(player.NetId, out var current) &&
            current == typeof(Cards.BlessingOfImpsCard))
        {
            return;
        }

        // 替换：从所有战斗牌堆移除旧英雄技能卡（火焰冲击/二级火焰冲击/奥术爆裂/冰冷触摸），
        // 再置入小精灵的祝福（与英雄卡替换英雄技能同一流程，见 JainaHeroCardTemplate.OnPlay）
        var oldHeroPowers = new System.Collections.Generic.List<MegaCrit.Sts2.Core.Models.CardModel>();
        foreach (var pileType in new[] { MegaCrit.Sts2.Core.Entities.Cards.PileType.Hand,
            MegaCrit.Sts2.Core.Entities.Cards.PileType.Draw,
            MegaCrit.Sts2.Core.Entities.Cards.PileType.Discard,
            MegaCrit.Sts2.Core.Entities.Cards.PileType.Exhaust,
            MegaCrit.Sts2.Core.Entities.Cards.PileType.Play })
        {
            var pile = pileType.GetPile(player);
            if (pile == null)
            {
                continue;
            }
            foreach (var card in pile.Cards)
            {
                if (card != null && card.CanonicalKeywords?.Contains(jaina.Scripts.Character.Keywords.JainaKeywords.HeroPower) == true)
                {
                    oldHeroPowers.Add(card);
                }
            }
        }
        if (oldHeroPowers.Count > 0)
        {
            // 不能用 skipVisuals=true（RemoveFromCombat 会跳过手牌节点移除，UI 残留）
            await CardPileCmd.RemoveFromCombat(oldHeroPowers, skipVisuals: false);
        }

        rec.CurrentHeroPowerTypeByPlayer[player.NetId] = typeof(Cards.BlessingOfImpsCard);

        // 创建小精灵的祝福实例并加入手牌（英雄技能卡不占手牌位；
        // 不标记"衍生"——之后每回合由该卡自己的 BeforeHandDraw 重新入手）
        var heroPower = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
            combatState, player, typeof(Cards.BlessingOfImpsCard), 0);
        if (heroPower != null)
        {
            await CardPileCmd.AddGeneratedCardToCombat(heroPower, MegaCrit.Sts2.Core.Entities.Cards.PileType.Hand, player);
        }
    }
}
