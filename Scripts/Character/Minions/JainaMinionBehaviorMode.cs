namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 随从行为模式。
/// </summary>
public enum JainaMinionBehaviorMode
{
    /// <summary>
    /// 手动模式（默认）：随从永不自动行动，一切行动靠玩家点击随从触发（行动点制）。
    /// </summary>
    Manual,

    /// <summary>
    /// 自动模式：随从在玩家回合结束时自动攻击随机敌人，并执行回合结束被动。
    /// </summary>
    Auto
}
