using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Characters;

namespace jaina.Scripts.Character;

/// <summary>
/// 吉安娜·普罗德摩尔 - 来自库尔提拉斯的冰霜法师
/// </summary>
[RegisterCharacter]
public sealed class Jaina : ModCharacterTemplate<JainaCardPool, JainaRelicPool, JainaPotionPool>
{
    public override CharacterGender Gender => CharacterGender.Feminine;

    /// <summary>
    /// 初始无需解锁（设置前置角色为铁甲战士后，随涅奥的初始扩展解锁）
    /// </summary>
    protected override System.Type? UnlocksAfterRunAsType => null;

    public override Color NameColor => new(0.35f, 0.7f, 1f);

    public override int StartingHp => 40;

    public override int StartingGold => 90;

    public override float AttackAnimDelay => 0.2f;

    public override float CastAnimDelay => 0.3f;

    public override Color EnergyLabelOutlineColor => new("1E3A5FFF");

    public override Color DialogueColor => new("0B4F6C");

    public override VfxColor SpeechBubbleColor => VfxColor.Blue;

    public override Color MapDrawingColor => new("4FA3D1");

    public override Color RemoteTargetingLineColor => new("4FA3D1FF");

    public override Color RemoteTargetingLineOutline => new("1E3A5FFF");

    public override List<string> GetArchitectAttackVfx()
    {
        return
        [
            "vfx/vfx_attack_blunt",
            "vfx/vfx_attack_slash",
            "vfx/vfx_bloody_impact",
            "vfx/vfx_rock_shatter",
            "vfx/vfx_attack_blunt"
        ];
    }

}
