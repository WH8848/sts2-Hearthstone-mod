# 制作 STS2 Mods 精华速记 — VFX 与 Patch 篇（第 9~10 章）

> 来源：tutorials.sts2modding.com 教程原文抓取（8 个文件），本文件为精华提炼，便于回查。
> 原文文件名：`docs_09-01-frame-animation.txt`、`docs_09-02-texture-atlas.txt`、`docs_09-03-vfx-instantiation.txt`、`docs_09-04-particle-vfx.txt`、`docs_09-05-world-environment.txt`、`docs_09-06-shader.txt`、`docs_09-07-game-builtin-vfx.txt`、`docs_10-patch.txt`

---

## 1. 帧动画的处理（docs_09-01）

**核心概念**
- 最基础的 VFX 形式：透明 PNG 序列帧 → `AnimatedSprite2D` 动画 → 游戏内实例化播放。
- 优点：常见简单、网络素材多为序列帧；缺点：性能差，需缩图优化。
- 资源路径约定：`YourMod/images/effect_1/fn0001.png`、`fn0002.png`…

**制作流程**
1. 序列图放入 Mod 资源目录；
2. Godot(Megadot) 编辑器**手动**新建场景：根节点 `Node2D` + 子节点 `AnimatedSprite2D`（尽量手搓，AI 生成的 .tscn 易有引用错误）；
3. 检查器给 `AnimatedSprite2D` 新建 `SpriteFrames` → 点开编辑界面 → 「从文件中添加帧」按顺序导入；
4. 有自动播放功能，无延时需求时**无需播放脚本**；一次性特效要自动销毁。

**tscn 关键结构（参考）**

```
[ext_resource type="Texture2D" path="res://RegentFX/frames/guiding_star/fn0001.png" id="1_t3dcf"]
[sub_resource type="SpriteFrames" id="SpriteFrames_pwecx"]
animations = [{"frames": [{"duration": 1.0, "texture": ExtResource("1_t3dcf")}, ...],
               "loop": true, "name": &"default", "speed": 5.0}]
[node name="AnimatedSprite2D" type="AnimatedSprite2D" parent="."]
sprite_frames = SubResource("SpriteFrames_pwecx")
```

**一次性特效自动销毁脚本（C#）**

```csharp
public partial class Effect : AnimatedSprite2D {
    public override void _Ready() {
        AnimationFinished += QueueFree; // 播放完毕自动删除自己
    }
}
```
（不想每个场景加脚本，可用 3-3 章 `VFXUtil` 的自定义销毁方法。）

**注意/坑**
- 帧图分辨率过大导致卡顿：批量选中图片 → 左上角导入设置 → 修改「最小分辨率像素」缩图（如 1080p → 720/540）。

---

## 2. 材质图集 Texture Atlas（docs_09-02）

**核心概念**
- 一张图按矩形表格排布多帧（如游戏自带斩击特效 `vfx_slash`）。
- 优点：性能较优、空间占用小；缺点：不适合大范围特效。
- 与帧动画流程相同，仅导入步骤不同。

**导入步骤**
1. 同样建场景与 `AnimatedSprite2D`；
2. 点 `SpriteFrames` → **「从精灵表中添加帧」**导入整图；
3. 配置切割：`水平数量`（一行几帧）、`垂直数量`（一共几行）、勾选**「裁切」**自动去空白边缘；
4. 例：水平 3 × 垂直 2 → 依次点击 6 次再添加。

---

## 3. 特效的播放(实例化)与缓存（docs_09-03）

**核心概念**
- ⚠️ 不要用 `AttackCommand.WithHitFX()` 等本体方法播特效：只能播放**游戏本体路径**下的特效；Mod 特效在 mod 子目录，除非 Patch 本体或用前置插件。
- 本体 `PreloadManager` 缓存策略：场景切换时 `UnloadAssets()` 只保留本体特效 → **Mod 特效场景会被意外清理**，需建独立缓存。
- 方案：自建 `VFXUtil` 工具类 + `ConcurrentDictionary<string, PackedScene>` 独立缓存（无需复杂 HarmonyPatch）。

**VFXUtil 核心代码**

```csharp
public static class VFXUtil {
    public static readonly ConcurrentDictionary<string, PackedScene> ModSceneCache = new();

    public static Node2D GenVFXNode(string scenePath) {
        if (ModSceneCache.TryGetValue(scenePath, out var modScene))
            return modScene.Instantiate<Node2D>();
        return PreloadManager.Cache.GetScene(scenePath).Instantiate<Node2D>();
    }
    public static T GenVFXNode<T>(string scenePath) where T : Node2D { /* 同上泛型版 */ }

    public static Node2D? PlaySimple(string scenePath, Vector2 position, float lifetime = 2f) {
        if (!TestMode.IsOn && NCombatRoom.Instance != null) {
            Node2D node2D = GenVFXNode(scenePath);
            NCombatRoom.Instance.CombatVfxContainer.AddChildSafely(node2D);
            node2D.GlobalPosition = position;
            SceneTreeTimer timer = node2D.GetTree().CreateTimer(lifetime);
            timer.Timeout += () => {
                if (GodotObject.IsInstanceValid(node2D)) node2D.QueueFreeSafely();
            };
            return node2D;
        }
        return null;
    }
}
// 用法
VFXUtil.PlaySimple("res://YourMod/scenes/vfx/glow.tscn", position, 2f);
```

**Mod 初始化（Entry.cs）预加载场景**

```csharp
static void LoadScenes() {
    var paths = new List<string> { "res://.../my_effect_1.tscn", /* ... */ };
    foreach (var path in paths) {
        if (ModSceneCache.ContainsKey(path)) continue;
        var scene = ResourceLoader.Load<PackedScene>(path, null, ResourceLoader.CacheMode.Reuse);
        if (scene != null) ModSceneCache[path] = scene;
    }
}
```

**NCombatRoom 场景结构与放置原则**

```
NCombatRoom (Control)
├── %CombatUi
├── %CombatSceneContainer
│   ├── %AllyContainer → NCreature(Player) → Body
│   ├── Visuals / Hitbox / IntentContainer / OrbManager
│   ├── %EnemyContainer → NCreature(Enemy)
│   └── EncounterSlots
├── %BgContainer              (ZIndex = -20)
├── %BackCombatVfxContainer   ← 后台特效容器
├── %CombatVfxContainer       (ZIndex = -9) ← 前台特效容器
└── RadialBlur
```

- **前台 `CombatVfxContainer`**：短暂攻击特效、命中、粒子爆发（覆盖角色上方）。
- **后台 `BackCombatVfxContainer`**：持续状态特效、背景元素（角色后方，如"创世之柱"）。
- 特效互盖可用代码改 `ZIndex`。
- 挂自定义脚本的特效用 `GenVFXNode<T>()` 工厂方法精确控制；父容器为 null 时 `Logger.Warn` + `QueueFree` 兜底。

**角色位置与朝向**

```csharp
NCreature? ownerNode = NCombatRoom.Instance?.GetCreatureNode(owner);
Vector2 spawnPos = ownerNode.VfxSpawnPosition; // 生物中心（特效生成位置）
Vector2 globalPos = ownerNode.GlobalPosition;  // 脚底

// 朝向判断（打大螃蟹 Boss 会朝左，特效需镜像翻转）
public static bool IsCharacterFacingRight(Creature creature) {
    Node2D? body = NCombatRoom.Instance?.GetCreatureNode(creature)?.Body;
    return body == null || body.Scale.X > 0;
}
bool facingRight = VFXUtil.IsCharacterFacingRight(creature);
int xFac = facingRight ? 1 : -1;
Vector2 position = creature.VfxSpawnPosition + new Vector2(100f * xFac, 0f);
```

**注意/坑**
- 不建缓存 → 特效**首次播放必卡顿**。

---

## 4. 粒子特效 GPUParticles2D（docs_09-04）

**核心概念**
- Godot 4 高性能粒子：`GPUParticles2D` + `ParticleProcessMaterial`（爆炸/烟雾/火花）。
- AI 协作流程：手绘简单圆形/方形/星形透明 PNG 给 AI，让其设计粒子特效；不知给什么素材就让 AI 反问（如"下雨特效"会找你要雨点素材）。

**场景关键属性**

| 属性 | 作用 | 典型值 |
|---|---|---|
| `one_shot` | 播放一次后停止 | true |
| `amount` | 粒子数量 | 1~100 |
| `lifetime` | 粒子存活时间 | 0.1~3.0 |
| `emitting` | 手动触发时 false，播放时 true | — |
| `explosiveness` | 爆发度 0~1（1.0=瞬间全发） | 1.0 |
| `fixed_fps` | 固定帧率 | 60 |
| `local_coords` / `texture` / `rotation` | — | — |

**ParticleProcessMaterial 常用参数**
- `particle_flag_align_y`、`particle_flag_disable_z`（2D 用）；`direction`（如 Vector3(0,1,0) 向上）+ `spread`（0=无扩散）；
- 速度：`initial_velocity_min/max`；`gravity`；`damping_min`（阻力减速）；
- 缩放/淡出曲线：`scale_min/max` + `scale_curve`(CurveXYZTexture)、`alpha_curve`(CurveTexture)；
- 进阶：`emission_shape = 6` + `emission_ring_radius`（环形发射）；`radial_accel` + `tangential_accel`（速度控制）；`turbulence_enabled` + `turbulence_noise_strength`（湍流）；`color_ramp`（生命周期内颜色渐变）。

**触发**

```csharp
VFXUtil.PlaySimple("res://YourMod/scenes/vfx/burst.tscn", _targetPosition);
```

---

## 5. WorldEnvironment 全局环境光照（docs_09-05）

**核心概念**
- `WorldEnvironment` 控制整场景默认 Environment：照明、后处理（SSAO/DOF/色调映射）、背景（纯色/天空盒）；同一场景只能有一个，可被 Camera3D 的 Environment 覆盖。
- 通过 `NGame.Instance.ActivateWorldEnvironment()` 获取**游戏本体**的 WorldEnvironment 节点 → 可改全屏亮度/曝光/对比度。
- 例：核爆特效 → 调高 `TonemapExposure`，用 Tween 补间淡入。
- ⚠️ 过度曝光=光污染+**光敏癫痫风险，慎用**。

**WorldEnvironmentUtil 工具类要点**

```csharp
public static class WorldEnvironmentUtil {
    private static WorldEnvironment? _cachedEnv;

    public static WorldEnvironment? GetOrActivateEnvironment() {
        if (_cachedEnv != null && GodotObject.IsInstanceValid(_cachedEnv)) return _cachedEnv;
        if (NGame.Instance == null) return null;
        _cachedEnv = NGame.Instance.ActivateWorldEnvironment();
        return _cachedEnv;
    }
    public static void DeactivateEnvironment() { /* NGame.Instance.DeactivateWorldEnvironment(); _cachedEnv=null */ }

    public static void SetGlowIntensity(float intensity) // env.Environment.GlowIntensity（0~3，默认 0.8）
    public static void SetExposure(float exposure)        // env.Environment.TonemapExposure
    public static void SetBrightness(float b)             // env.Environment.AdjustmentBrightness
    public static void SetContrast(float c)               // env.Environment.AdjustmentContrast
    public static void SetSaturation(float s)             // env.Environment.AdjustmentSaturation
    public static void ResetToDefaults()                  // 曝光1 亮度1 对比度1 饱和度1 发光0.8（不自动 Deactivate）
}
```

**注意/坑**
- Glow 需 Environment 开启 Glow 才看得到效果；`DeactivateEnvironment` 需手动调用。
- 用后记得复位，避免影响后续场景。

---

## 6. Shader（docs_09-06）

**核心概念（用途）**
- 动态调色/风格化：不改原图实现受伤闪红、冻结变蓝、中毒变绿；
- 纹理混合/程序化细节：遮罩混合、噪声生成火焰/水流/云；
- 描边/发光/轮廓：基于法线或深度；
- UV 动画与变形：流水滚动背景、顶点飘动呼吸。

**挂载方式**
- 给特效节点（`Sprite2D` / `AnimatedSprite2D` 等）：`CanvasItem → Material → 新建 ShaderMaterial → 加载 .gdshader` → 调参数。
- 提供"万能"shader 存为 `advanced.gdshader`（`shader_type canvas_item`）。

**advanced.gdshader 参数总览（uniform）**
- 基础：`opacity`；
- 发光/Bloom：`enable_glow`、`glow_intensity(0-5)`、`glow_radius(像素)`、`glow_color`、`glow_threshold`、`glow_softness`、`bloom_intensity`、`bloom_radius`；
- 模糊：`enable_blur`、`blur_radius`、`blur_direction`(0双向/1水平/2垂直)、`motion_blur_angle`、`motion_blur_distance`；
- 锐化：`enable_sharpen`、`sharpen_amount`、`sharpen_radius`；
- 扭曲：`enable_distortion`、`ripple_intensity/frequency/speed`（波纹）、`swirl_intensity`、`swirl_center`（漩涡）、`lens_distort`（鱼眼）、`chromatic_aberration`（RGB 分离）；
- 颜色：`hue_shift`、`saturation`、`brightness`、`contrast`、`enable_color_overlay`+`overlay_color`+`overlay_blend_mode`(0正常/1正片叠底/2滤色/3叠加/4柔光/5颜色加深)、`enable_gradient_map`+`gradient_color1/2`；
- 特殊：`enable_outline`+`outline_width/color`（描边）、`pixelate_size`（像素化）、`invert_colors`、`grayscale`、`sepia`、`threshold_level`（二值化）。

**fragment() 合成顺序**
1. `distort_uv(UV)` 扭曲（像素化→漩涡→波纹→镜头畸变）
2. 色差分离采样（chromatic_aberration）
3. `get_glow` 发光 → `bloom`（get_glow 阈值 0.3）
4. 模糊（motion_blur / gaussian_blur_2d / gaussian_blur 按 direction）
5. 锐化（center + (center-blurred)*amount）
6. `adjust_color`（HSV→对比度→反相→灰度→复古→阈值→渐变映射）
7. 颜色叠加 `blend(base, overlay, mode)`
8. 描边 `outline()`（8 方向采样 alpha 外扩）
9. 合成 `COLOR = vec4(final_rgb, final_alpha * opacity)`
- 内置变量：`TEXTURE`/`UV`/`TEXTURE_PIXEL_SIZE`/`TIME`/`COLOR`；工具函数 `rgb2hsv`/`hsv2rgb`/`gaussian_blur`/`get_glow` 等可直接复用。

**区域效果**
- 参考游戏 `starry_impact`（储君攻击受击）、`scream`（尖叫）：`vfx_distortion` = 一块区域的屏幕扭曲。
- **shader 是纯代码、AI 友好**：把游戏 gdshader 源码"偷"出来喂给 AI 让它实现你要的效果即可。

---

## 7. 游戏本体特效（docs_09-07）

**核心概念**
- 无素材时的最优解：直接用 STS2 内置 VFX，`VfxCmd` 类调用，免做场景（namespace `MegaCrit.Sts2.Core.Commands`）。

**常用 API**

```csharp
VfxCmd.PlayVfx(position, "vfx/vfx_attack_slash", vfxContainer);          // 指定位置（vfxContainer 可传当前战斗容器或 null）
VfxCmd.PlayOnCreatureCenter(target, "vfx/vfx_starry_impact");           // 生物中心（考虑是否死亡）
VfxCmd.PlayOnCreature(target, "vfx/vfx_bloody_impact");                 // 更底层的位置播放
VfxCmd.PlayOnSide(CombatSide.Enemy, "vfx/vfx_heavy_blunt", combatState); // 战斗一侧中心（AOE）
VfxCmd.PlayFullScreenInCombat("vfx/vfx_adrenaline", spawner);           // 全屏（spawner 定位容器，可 null）
VfxCmd.PlayOnCreatureCenters(enemies, "vfx/vfx_scratch");               // 批量
```

**内置特效路径常量（VfxCmd 字段）**
- 攻击类：`slashPath`(vfx_attack_slash 斩击)、`bluntPath`、`lightningPath`、`heavyBluntPath`、`bloodyImpactPath`、`starryImpactVfx`；
- 技能类：`adrenalinePath`、`blockPath`、`healPath`(vfx_cross_heal)、`gazePath`、`screamVfx`；
- 投掷类：`daggerThrowPath`(vfx_dagger_throw)、`chainPath`、`flyingSlashPath`；
- 其他：`bitePath`、`rockShatterPath`、`sandyImpactPath`、`slimeImpactVfxPath`。

**修改现有特效（VfxCmd 不返回节点）**
- 仿照 `PlayVfx` 源码自己写一个返回 `Node2D` 的函数：

```csharp
public static Node2D PlayVfxReturn(Vector2 position, string path, Control? vfxContainer) {
    string scenePath = SceneHelper.GetScenePath(path);
    Node2D node2D = PreloadManager.Cache.GetScene(scenePath).Instantiate<Node2D>(PackedScene.GenEditState.Disabled);
    vfxContainer?.AddChildSafely(node2D);
    node2D.GlobalPosition = position;
    return node2D; // 拿到节点后可遍历 GPUParticles2D/Sprite2D 上色等
}
```

**注意/坑**
- 修改**含 Material 的特效必须复制 Material**，否则改一个影响所有特效实例；
- 调整特效大小时**粒子特效不生效**，需改其相对位置参数。

---

## 8. Patch（docs_10，Harmony）

**核心概念**
- Harmony：运行时对 .NET 程序打补丁/替换/装饰（文档 https://harmony.pardeike.net/articles/intro.html ）。
- 初始化：`using HarmonyLib;` + `new Harmony("com.example.patch")`（**唯一 ID 防撞车**）+ `harmony.PatchAll()`（自动扫描本程序集所有 `[HarmonyPatch]`）。

**基础示例**

```csharp
[HarmonyPatch(typeof(SomeGameClass), nameof(SomeGameClass.DoSomething))]
public class Patch01 {
    // 开头执行；返回 bool，false 则跳过原方法体（Postfix 仍执行）
    // __instance = this；___counter = 类字段（私有也可，三个下划线）
    public static bool Prefix(SomeGameClass __instance, ref int ___counter) {
        if (___counter > 100) return false;
        ___counter = 0;
        return true;
    }
    // 结尾执行（每个 return 处）；__result 为返回值
    static void Postfix(ref int __result) => __result *= 2;
}
```

**[HarmonyPatch] 特性参数**
- `declaringType`（目标类）、`methodName`（目标方法，推荐 `nameof`）、`methodType`（构造函数/getter/setter/async 等编译改名方法）、`argumentTypes`（同名重载时区分）、`argumentVariations`（`ArgumentType.Normal/Ref/Out/Pointer` 数组）。
- `MethodType` 要点：属性 → `MethodType.Getter/Setter`（编译名 get_X/set_X）；构造函数 → `MethodType.Constructor`（.ctor）；async → `MethodType.Async`（进状态机 `MoveNext`）。
- 重载签名：`[HarmonyPatch(typeof(ScoreBoard), nameof(ScoreBoard.Add), [typeof(int)], [ArgumentType.Ref])]`。

**Patch Async 方法（重要）**
- 用 ILSpy 打开 async 方法（如卡牌 `OnPlay`），下拉语言选 **C# 4.0** 看编译后状态机 `<OnPlay>d__5`；
- 不加 `MethodType.Async` 只 patch 表层壳函数；加了进 `<OnPlay>d__5.MoveNext`；
- 此时 `object __instance` 是**编译器生成的状态机**，通过反射拿原对象：

```csharp
[HarmonyPatch(typeof(Wallet), nameof(Wallet.FetchGoldAsync), MethodType.Async)]
class PatchFetchGoldAsync {
    static void Prefix(object __instance) {
        var wallet = Traverse.Create(__instance).Field("<>4__this").GetValue<Wallet>();
        wallet.Gold += 10;
    }
}
```

**Patch 方法参数（按参数名注入；Transpiler 例外按类型）**
- `__instance`：非静态方法的 this（patch 静态方法别写）；
- 与原方法同名参数：类型、ref/out 须一致（如 `ref int damage`）；
- `__0`、`__1`…：按位置对应参数（原名不好写/统一处理多个方法时用）；
- `__result`：返回值；要改须写 `ref`（Prefix 里为 default）；
- `__resultRef`：原方法返回 `ref T` 时改引用本身；
- `___字段名`：三下划线+字段名，读写私有字段（写入须 ref）；
- `__args`：全部实参 `object[]`，改元素会回写（略有开销）；
- `__state`：同一补丁类 Prefix 写入（out）、Postfix 只读，跨阶段传数据；
- `__originalMethod`：`MethodBase`，仅元信息，**不能调用原方法**；
- `__runOriginal`：Prefix=原方法是否将执行；Postfix=是否已执行（被跳则为 false）；只读；
- `__exception`（Finalizer）：`Exception`，返回 null 吞异常，返回新异常替换。

**四种 Patch 方式**
- **Prefix**：改方法开头、改参数、跳过原方法（bool 返回 false + 设 `__result`；⚠️ 受 patch 加载顺序影响）、`out __state` 传状态给 Postfix；
- **Postfix**：改 void 结尾、`ref __result` 改返回值（每个 return 执行一次）；
- **Transpiler**：IL 层改指令序列（`IEnumerable<CodeInstruction>` 第一个参数，返回新序列；`OpCodes.Ret` 前插 `Ldc_I4_2`+`Mul` 等价 `__result *= 2`）。灵活但**能不用的地方别用**，复杂定位用 `CodeMatcher`；
- **Finalizer**：观察/替换/吞异常。

**Reverse Patch（拷贝原版逻辑到自己方法）**

```csharp
[HarmonyPatch]
public static class CombatMathBridge {
    [HarmonyReversePatch]
    [HarmonyPatch(typeof(CombatMath), "SecretScale")]
    // 签名必须按原样复制（非静态加 __instance + 所有参数）；函数体保持 throw 即可
    public static int SecretScale(CombatMath __instance, int value)
        => throw new NotImplementedException();
}
// 之后 CombatMathBridge.SecretScale(combat, 10) 等价调用原方法
```

**其他工具**
- `harmony.Patch(original, new HarmonyMethod(prefix), new HarmonyMethod(postfix))`；`harmony.UnpatchAll()`；`harmony.Unpatch(original, HarmonyPatchType.Prefix, "their.harmony.id")`；
- **Traverse**：反射访问/调用辅助类（带缓存、**空保护**）：`Traverse.Create(type/<T>/foo)` → `.Field("x")`/`.Property("y")`/`.Method("m", args)` → `.GetValue<T>()`/`.SetValue(v)`；`IterateFields/IterateProperties`；
- **AccessTools**：`TypeByName`、`Field`、`Property`、`Method`、`Constructor`、`Inner`、`FirstInner`、`FirstMethod`；
- **TargetMethod()/TargetMethods()**：目标方法不好用特性写死时（嵌套类/按名筛选/一次打多个）；类上仍须 `[HarmonyPatch]` 才会被 `PatchAll` 扫描；配套 `Prepare(MethodBase)`（返回 false 跳过本类）与 `Cleanup(original, ex)`（返回 Exception 可吞 patch 过程异常）；
- **HarmonyPriority(int)**：数值越大越先执行（默认 Normal=400；First=800、High=600、Low=200、Last=0）；多个 Postfix 改 `__result` 时**最后执行的生效**；
- **HarmonyBefore/HarmonyAfter(string[])**：按其它 Harmony 实例的 id（`new Harmony("这个字符串")`）排序，而非 int 优先级。

**提醒**
- 不要滥用 Transpiler 和 bool-Prefix 跳代码；保证 patch 健壮、避免与其他 mod 冲突；
- 使用 ritsulib 时可通过其 patch 封装系统补丁，逻辑类似。
