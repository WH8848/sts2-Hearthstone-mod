using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 西芙 (Sif)：力量+1。在本局对战中，你每施放过一个派系的法术都会提升
/// （火焰/冰霜/奥术——每个新派系首次施放后，力量再+1）。
/// 挂在吉安娜玩家身上（西芙遗物战斗开始施加，幂等）。
/// 触发：AfterCardPlayed——玩家（含自动释放/随机释放，语义"施放"）打出
/// 法术牌（IsSpellCard + GetSchoolOf 动态判定）后，若该派系本场战斗首次施放，
/// 施加 1 点真实力量（StrengthPower，与游戏内力量叠加、显示标准力量图标）。
/// 英雄技能卡无派系关键词（只有 HeroPower），GetSchoolOf 返回 null，自动排除。
/// 派系集合存在 Power 实例内：战斗结束 Power 随战斗清除，每场战斗重新计数。
/// </summary>
[RegisterPower]
public sealed class SifPower : PowerModel, IModPowerAssetOverrides
{
    /// <inheritdoc />
    public PowerAssetProfile AssetProfile => new("res://assets/power_icons/jaina_power_sif_power.png");

    /// <inheritdoc />
    public string? CustomIconPath => AssetProfile.IconPath;

    /// <inheritdoc />
    public string? CustomBigIconPath => AssetProfile.BigIconPath;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => true;

    /// <summary>
    /// 本场战斗已施放过（获得过力量加成）的派系集合
    /// </summary>
    private readonly HashSet<JainaSpellSchool> _schoolsGranted = [];

    /// <summary>
    /// 幂等挂载西芙（西芙遗物每场战斗开始调用；已有则不动）。
    /// 施加基础力量+1（StrengthPower 真实力量，与游戏内力量叠加）。
    /// </summary>
    public static async Task EnsureAppliedAsync(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        if (player?.Creature == null || player.Creature.Powers.Any(p => p is SifPower))
        {
            return;
        }
        await PowerCmd.Apply<SifPower>(choiceContext, [player.Creature], 1m, player.Creature, null);
        // 基础力量 +1
        await PowerCmd.Apply<StrengthPower>(choiceContext, [player.Creature], 1m, player.Creature, null);
    }

    /// <summary>
    /// 玩家施放一个法术牌后：若该派系本场战斗首次施放，力量再+1
    /// （含自动释放/随机释放——"施放"语义；英雄技能无派系关键词，自动排除）
    /// </summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner?.Player;
        if (player == null || cardPlay.Card?.Owner != player)
        {
            return;
        }
        var school = GetSchoolOfSpell(cardPlay.Card);
        if (school == null || !_schoolsGranted.Add(school.Value))
        {
            return;
        }
        await PowerCmd.Apply<StrengthPower>(choiceContext, [Owner], 1m, Owner, null);
    }

    /// <summary>
    /// 法术牌的派系：法术牌（IsSpellCard：攻击/技能，或带"法术牌"关键词的能力牌）
    /// + 派系关键词（GetSchoolOf 动态判定，升级形态自动跟随）；
    /// 英雄技能卡（只有 HeroPower 关键词）返回 null。
    /// </summary>
    private static JainaSpellSchool? GetSchoolOfSpell(CardModel card)
    {
        if (card == null || !jaina.Scripts.Character.JainaCastTracker.IsSpellCard(card))
        {
            return null;
        }
        return jaina.Scripts.Character.JainaCastTracker.GetSchoolOf(card);
    }
}
