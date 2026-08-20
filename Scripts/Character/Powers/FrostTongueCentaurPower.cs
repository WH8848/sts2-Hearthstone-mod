using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 霜舌半人马：在你施放一个冰霜法术后，召唤一个1/1的霜冻元素。
/// 挂在吉安娜玩家身上（霜舌半人马遗物战斗开始施加，幂等）。
/// 触发：AfterCardPlayed——玩家（含自动释放/随机释放，语义"施放"）打出
/// 冰霜法术（IsSpellCard + GetSchoolOf == Frost，动态判定升级形态）后，
/// 召唤 1/1 FrostElementalMinion（霜冻元素：造成伤害给 1 层冻结）。
/// </summary>
[RegisterPower]
public sealed class FrostTongueCentaurPower : PowerModel, IModPowerAssetOverrides
{
    /// <inheritdoc />
    public PowerAssetProfile AssetProfile => new("res://assets/power_icons/jaina_power_frost_tongue_centaur_power.png");

    /// <inheritdoc />
    public string? CustomIconPath => AssetProfile.IconPath;

    /// <inheritdoc />
    public string? CustomBigIconPath => AssetProfile.BigIconPath;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => true;

    /// <summary>
    /// 幂等挂载（霜舌半人马遗物每场战斗开始调用；已有则不动）
    /// </summary>
    public static async Task EnsureAppliedAsync(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        if (player?.Creature == null || player.Creature.Powers.Any(p => p is FrostTongueCentaurPower))
        {
            return;
        }
        await PowerCmd.Apply<FrostTongueCentaurPower>(choiceContext, [player.Creature], 1m, player.Creature, null);
    }

    /// <summary>
    /// 玩家施放冰霜法术后：召唤 1/1 霜冻元素（含自动释放/随机释放——"施放"语义；
    /// 霜冻元素本身不施放法术，无递归风险）
    /// </summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner?.Player;
        if (player == null || cardPlay.Card?.Owner != player)
        {
            return;
        }
        if (!IsFrostSpell(cardPlay.Card))
        {
            return;
        }
        await jaina.Scripts.Character.Minions.JainaMinionPool.SummonMinion<jaina.Scripts.Character.Minions.FrostElementalMinion>(
            choiceContext, player, maxHp: 1m, attack: 1m);
    }

    /// <summary>
    /// 冰霜法术：法术牌（IsSpellCard：攻击/技能，或带"法术牌"关键词的能力牌）
    /// + 冰霜派系（GetSchoolOf 动态判定）
    /// </summary>
    private static bool IsFrostSpell(CardModel card)
    {
        if (card == null || !jaina.Scripts.Character.JainaCastTracker.IsSpellCard(card))
        {
            return false;
        }
        return jaina.Scripts.Character.JainaCastTracker.GetSchoolOf(card) == jaina.Scripts.Character.JainaSpellSchool.Frost;
    }
}
