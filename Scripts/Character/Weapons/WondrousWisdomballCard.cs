using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Weapons;

/// <summary>
/// 魔法智慧之球 (Wondrous Wisdomball) - 1费武器能力卡（衍生）。
/// 武器：攻击力 0 / 耐久度 6。
/// 在你的回合结束时，随机施放一个有用的法师法术。失去1点耐久度。
/// 由益智大师卡德加战吼装备（也可单独打出）。
/// </summary>
[RegisterCard(typeof(jaina.Scripts.Character.JainaNeutralCardPool))]
public sealed class WondrousWisdomballCard : JainaWeaponCardTemplate
{
    public override int WeaponAttack => 0;

    public override int WeaponDurability => 6;

    public override string CustomPortraitPath => "res://assets/card_art/magic_wisdomball.png";

    public WondrousWisdomballCard()
        : base(1, CardRarity.Token)
    {
    }

    /// <summary>
    /// 卡名不变
    /// </summary>
    public override string Title
    {
        get
        {
            var title = new LocString("cards", base.Id.Entry + ".title");
            return title.GetFormattedText();
        }
    }

    /// <summary>
    /// 打出：装备武器（顶替旧武器），并挂载"回合结束施放法师法术"效果。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        await KhadgarOrbHelper.EquipOrb(choiceContext, base.Owner, this);
    }
}

/// <summary>
/// 装备魔法智慧之球共用逻辑（卡德加战吼与球卡打出共用）：
/// 顶替旧武器并挂 0/6 球武器，挂"回合结束施法"效果 Power。
/// </summary>
public static class KhadgarOrbHelper
{
    public static async Task EquipOrb(PlayerChoiceContext choiceContext, Player player, CardModel? source)
    {
        if (player == null)
        {
            return;
        }
        // 顶替旧的球效果（换武器后不再生效）
        var oldOrb = player.Creature.Powers.OfType<KhadgarOrbPower>().FirstOrDefault();
        if (oldOrb != null)
        {
            await PowerCmd.Remove(oldOrb);
        }

        // 装备 0/6 武器（顶替旧武器）；装备失败不阻塞球效果挂载（球效果是回合结束施法，不依赖武器挂载成功）
        try
        {
            await JainaWeaponSlot.Equip(choiceContext, player, 0, 6, source);
        }
        catch (System.Exception ex)
        {
            MegaCrit.Sts2.Core.Logging.Log.Warn($"[Jaina] EquipOrb weapon equip failed: {ex}");
        }

        // 挂"回合结束随机施放法师法术 + 失去1点耐久度"
        await PowerCmd.Apply<KhadgarOrbPower>(choiceContext, [player.Creature], 1m, player.Creature, source);

        // 武器能力栏只显示特殊效果（攻击力在角色攻击意图、耐久度在图标右下角标）
        var weapon = player.Creature.Powers.OfType<JainaWeaponPower>().FirstOrDefault();
        if (weapon != null)
        {
            weapon.EffectLocKey = "JAINA_POWER_WISDOMBALL_EFFECT.description";
        }
        MegaCrit.Sts2.Core.Logging.Log.Info("[JainaDiag] EquipOrb: KhadgarOrbPower applied");
    }
}
