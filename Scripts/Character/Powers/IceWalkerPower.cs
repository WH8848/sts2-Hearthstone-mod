using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 寒冰行者光环：你的英雄技能还会给与目标 1 层冻结。
/// 挂在随从生物自身——随从死亡时本 Power 随生物移除，被动自动失效。
/// </summary>
[RegisterPower]
public sealed class IceWalkerPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => false;

    /// <summary>
    /// 玩家打出英雄技能卡（含免费自动打出，如小精灵驾驭者/鲁莽的学徒）后：
    /// 给目标 1 层冻结。
    /// </summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var owner = Owner?.PetOwner;
        if (owner == null || cardPlay.Card.Owner != owner)
        {
            return;
        }
        // 只响应英雄技能卡（火焰冲击/二级火焰冲击/奥术爆裂/冰冷触摸）
        if (!cardPlay.Card.Keywords.Contains(jaina.Scripts.Character.Keywords.JainaKeywords.HeroPower))
        {
            return;
        }
        // 目标存活才冻结
        if (cardPlay.Target is not { IsAlive: true } target)
        {
            return;
        }
        // 随从给予的冻结不被人工制品(Artifact)阻挡——与滑冰元素/瓦尔登一致
        // （见 ArtifactFreezeBypassPatch / FreezePower.BypassArtifactNextApply;
        //  联机两端命令确定性执行,行为一致）
        try
        {
            FreezePower.BypassArtifactNextApply = true;
            await PowerCmd.Apply<FreezePower>(choiceContext, [target], 1m, Owner, cardPlay.Card);
        }
        finally
        {
            FreezePower.BypassArtifactNextApply = false;
        }
    }
}
