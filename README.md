# Nodin - No Odin Inspector

轻量级 Odin Inspector 替代方案，通过反射式自动绘制器提供 `[FoldoutGroup]`、`[LabelText]`、`[Button]`、`[ShowIf]` 等常用特性，无需安装任何第三方插件。

## 版本信息

- **版本**: 1.7.0
- **Unity 版本要求**: 2021.3+
- **许可证**: Apache-2.0
- **作者**: zko

## 功能特性

### 分组 & 布局
- **`[FoldoutGroup]`** - 将字段/方法归入可折叠分组，支持 '/' 分隔的子分组
- **`[BoxGroup]`** - 将字段归入带标题的盒子分组（无 FoldoutGroup 时自动作为后备分组）
- **`[ToggleGroup]`** - 布尔开关分组，bool 字段作为标题开关，控制组内其他字段的显示/隐藏。可单独使用（自动创建顶层分组），也可与 FoldoutGroup 子分组组合（如 `[ToggleGroup("贴图")]` + `[FoldoutGroup("贴图/参数")]`）
- **`[HorizontalGroup]`** - 将多个字段水平排列在同一行

### 标签 & 显示
- **`[LabelText]`** - 自定义 Inspector 中显示的标签文本
- **`[HideLabel]`** - 隐藏字段标签，仅显示值控件
- **`[InfoBox]`** - 在字段上方显示信息提示框（支持 Info/Warning/Error 类型）
- **`[MultiLineProperty]`** - 将字符串字段绘制为多行文本区域
- **`[Title]`** - 在字段上方显示标题文本（支持粗体/下划线样式）
- **`[DisplayAsString]`** - 将字段值以纯文本形式显示（不可编辑）
- **`[Required]`** - 标记 Object 引用字段为必填，空值时显示警告

### 条件显示 & 启用
- **`[ShowIf]`** - 当指定成员值等于目标值时显示字段
- **`[HideIf]`** - 当指定成员值等于目标值时隐藏字段
- **`[EnableIf]`** - 当指定成员值等于目标值时启用字段
- **`[DisableIf]`** - 当指定成员值等于目标值时禁用字段
- **`[ReadOnly]`** - 将字段标记为只读（不可编辑）

### 按钮 & 动作
- **`[Button]`** - 将方法绘制为 Inspector 按钮（支持 Small/Medium/Large 尺寸）
- **`[GUIColor]`** - 设置按钮或字段的 GUI 颜色
- **`[OnInspectorGUI]`** - 在 Inspector 中插入自定义 GUI 绘制回调
- **`[ShowInInspector]`** - 强制在 Inspector 中显示非 public 字段

### 字段行为
- **`[ValueDropdown]`** - 下拉列表选项来源（方法名或字段名）
- **`[FolderPath]`** - 文件夹路径选择器
- **`[AssetsOnly]`** - 限制 Object 引用仅允许 Asset（非场景对象）
- **`[OnValueChanged]`** - 字段值改变后回调指定方法
- **`[MinValue]`** - 设置数值字段的最小值约束
- **`[ListDrawerSettings]`** - List 字段的绘制设置
- **`[DictionaryDrawerSettings]`** - Dictionary 字段的绘制设置（自定义 Key/Value 列标签）
- **`[Serializable]` 自动内联绘制** - 标记了 `[Serializable]` 的类作为字段时自动内联绘制，带折叠头部和脚本定位
- **`[ShowInInspector]` 属性支持** - 标记了 `[ShowInInspector]` 的属性（含表达式体属性）自动被收集绘制

## 基类

### NodinMonoBehaviour
继承此类即可让 MonoBehaviour 自动支持 Nodin 属性绘制，同时**自动处理 Dictionary 字段的序列化**（Unity 原生不支持 Dictionary 序列化）。

```csharp
using UnityEngine;
using Nodin;
using System.Collections.Generic;

public class EnemyManager : NodinMonoBehaviour
{
    [LabelText("敌人等级表")]
    [DictionaryDrawerSettings(KeyLabel = "敌人名", ValueLabel = "等级")]
    public Dictionary<string, int> enemyLevels = new Dictionary<string, int>
    {
        { "史莱姆", 1 },
        { "哥布林", 5 },
        { "巨龙", 50 }
    };
}
```

> **注意**: 使用 `Dictionary` 字段时**必须继承 `NodinMonoBehaviour`**，否则序列化数据会在 Play Mode 切换时丢失。`NodinMonoBehaviour` 实现了 `ISerializationCallbackReceiver`，通过扁平列表自动持久化所有 Dictionary 字段。

### MonoBehaviour 自动支持（v1.3.0+）
从 v1.3.0 起，所有 `MonoBehaviour`（包括通过 `MonoSingleton<T>` 等模式间接继承的类型）**无需修改继承关系**即可使用 Nodin 属性。编辑器会自动检测字段上是否使用了 `[LabelText]`、`[FoldoutGroup]` 等特性，有则使用 NodinDrawer 绘制，否则回退到 Unity 默认 Inspector。

```csharp
// 即使继承 MonoSingleton（→ MonoBehaviour），[LabelText] 也能正常渲染
public class BattleFlowManager : MonoSingleton<BattleFlowManager>
{
    [LabelText("关卡列表（按顺序循环）")]
    public List<LevelConfig> levelConfigs;
}
```

## 安装方法

### 方式一：直接复制
将整个 `Nodin` 文件夹复制到项目的 `Assets` 目录下任意位置。

### 方式二：Unity Package Manager
1. 打开 Unity Package Manager
2. 点击左上角的 "+" 按钮
3. 选择 "Add package from disk..."
4. 选择 `Nodin` 文件夹中的 `package.json` 文件

## 使用方法

### 1. 基本使用

在 MonoBehaviour 或 ScriptableObject 中使用 Nodin 特性：

```csharp
using UnityEngine;
using Nodin;

public class PlayerController : MonoBehaviour
{
    [FoldoutGroup("基础设置")]
    [LabelText("移动速度")]
    public float moveSpeed = 5f;
    
    [FoldoutGroup("基础设置")]
    [LabelText("跳跃高度")]
    public float jumpHeight = 2f;
    
    [FoldoutGroup("高级设置")]
    [ShowIf("useGravity")]
    [LabelText("重力倍率")]
    public float gravityMultiplier = 1f;
    
    [FoldoutGroup("高级设置")]
    [LabelText("启用重力")]
    public bool useGravity = true;
    
    [Button("重置位置")]
    public void ResetPosition()
    {
        transform.position = Vector3.zero;
    }
}
```

### 2. 条件显示示例

```csharp
using UnityEngine;
using Nodin;

public class EnemyAI : MonoBehaviour
{
    public enum AIState { Idle, Patrol, Chase, Attack }
    
    [LabelText("AI 状态")]
    public AIState currentState = AIState.Idle;
    
    [ShowIf("currentState", AIState.Patrol)]
    [LabelText("巡逻路径")]
    public Transform[] patrolPoints;
    
    [ShowIf("currentState", AIState.Chase)]
    [LabelText("追击速度")]
    public float chaseSpeed = 8f;
    
    [ShowIf("currentState", AIState.Attack)]
    [LabelText("攻击范围")]
    public float attackRange = 2f;
}
```

### 3. 分组布局示例

```csharp
using UnityEngine;
using Nodin;

public class WeaponSettings : MonoBehaviour
{
    [FoldoutGroup("基础属性")]
    [LabelText("伤害值")]
    public int damage = 10;
    
    [FoldoutGroup("基础属性")]
    [LabelText("攻击速度")]
    public float attackSpeed = 1f;
    
    [FoldoutGroup("特效设置")]
    [LabelText("攻击特效")]
    public GameObject attackEffect;
    
    [FoldoutGroup("特效设置/音效")]
    [LabelText("攻击音效")]
    public AudioClip attackSound;
    
    [FoldoutGroup("特效设置/音效")]
    [LabelText("音量")]
    [Range(0, 1)]
    public float volume = 1f;
}
```

### 3b. ToggleGroup 开关分组

`[ToggleGroup]` 可单独使用，bool 字段自动成为折叠头+开关，组内其他字段随开关显隐：

```csharp
using UnityEngine;
using Nodin;

public class EffectSettings : MonoBehaviour
{
    // ToggleGroup 单独使用 —— 不需要搭配 FoldoutGroup
    [ToggleGroup("粒子特效")]
    [LabelText("启用粒子特效")]
    public bool enableParticles = true;

    [ToggleGroup("粒子特效")]
    [LabelText("粒子颜色")]
    public Color particleColor = Color.cyan;

    [ToggleGroup("粒子特效")]
    [LabelText("粒子大小")]
    public float particleSize = 1.5f;
}
```

ToggleGroup 还可与 FoldoutGroup 子分组组合，实现开关内嵌套可折叠子分组：

```csharp
using UnityEngine;
using Nodin;
using UnityEditor;

public class TextureRules : MonoBehaviour
{
    // 开关头
    [ToggleGroup("贴图导入规则")]
    [LabelText("启用贴图规则")]
    public bool enableTextureRule;

    [ToggleGroup("贴图导入规则")]
    [LabelText("目标扩展名")]
    public string extensions = ".png,.jpg";

    // 子分组 —— 自动跟随 ToggleGroup 开关状态
    [FoldoutGroup("贴图导入规则/公共参数")]
    [LabelText("压缩方式")]
    public TextureImporterCompression compression;

    [FoldoutGroup("贴图导入规则/UI 参数")]
    [LabelText("Sprite 模式")]
    public SpriteImportMode spriteMode;
}
```

### 4. 按钮和回调示例

```csharp
using UnityEngine;
using Nodin;

public class GameManager : MonoBehaviour
{
    [LabelText("游戏分数")]
    public int score;
    
    [Button("增加分数", ButtonSizes.Medium)]
    [GUIColor(0.3f, 0.8f, 0.3f)]
    public void AddScore()
    {
        score += 100;
        Debug.Log($"当前分数: {score}");
    }
    
    [Button("重置游戏")]
    [GUIColor("#FF6B6B")]
    public void ResetGame()
    {
        score = 0;
        Debug.Log("游戏已重置");
    }
    
    [OnValueChanged("OnHealthChanged")]
    [LabelText("生命值")]
    [Range(0, 100)]
    public int health = 100;
    
    private void OnHealthChanged()
    {
        Debug.Log($"生命值变更为: {health}");
    }
}
```

### 5. 列表和下拉菜单示例

```csharp
using UnityEngine;
using Nodin;
using System.Collections.Generic;

public class InventorySystem : MonoBehaviour
{
    [LabelText("物品列表")]
    [ListDrawerSettings(ShowFoldout = true, DraggableItems = true)]
    public List<string> items = new List<string>();
    
    [LabelText("选择武器")]
    [ValueDropdown("GetWeaponList")]
    public string selectedWeapon;
    
    private string[] GetWeaponList()
    {
        return new string[] { "剑", "弓", "法杖", "匕首" };
    }
    
    [FolderPath]
    [LabelText("资源路径")]
    public string resourcePath = "Assets/Resources";
}
```

### 6. Dictionary 序列化示例

```csharp
using UnityEngine;
using Nodin;
using System.Collections.Generic;

public class GameConfig : NodinMonoBehaviour
{
    [FoldoutGroup("敌人配置")]
    [LabelText("敌人等级表")]
    [DictionaryDrawerSettings(KeyLabel = "敌人名", ValueLabel = "等级")]
    public Dictionary<string, int> enemyLevels = new Dictionary<string, int>
    {
        { "史莱姆", 1 },
        { "哥布林", 5 },
        { "巨龙", 50 }
    };

    [FoldoutGroup("武器配置")]
    [LabelText("武器伤害表")]
    [DictionaryDrawerSettings(KeyLabel = "武器", ValueLabel = "伤害")]
    public Dictionary<string, float> weaponDamage = new Dictionary<string, float>
    {
        { "铁剑", 15f },
        { "长弓", 18f },
        { "法杖", 25f }
    };
}
```

## 支持的数据类型

Nodin 支持以下 Unity 数据类型的自动绘制：

- **基本类型**: `bool`, `int`, `long`, `float`, `double`, `string`
- **向量类型**: `Vector2`, `Vector3`, `Vector4`
- **颜色类型**: `Color`
- **矩形类型**: `Rect`
- **枚举类型**: 所有枚举类型
- **Unity 对象**: 所有继承自 `UnityEngine.Object` 的类型
- **列表类型**: `List<T>` 支持上述所有类型
- **字典类型**: `Dictionary<TKey, TValue>` 需继承 `NodinMonoBehaviour` 以支持序列化

## 高级功能

### 1. 自定义编辑器窗口

继承 `NodinEditorWindow` 创建自定义编辑器窗口：

```csharp
using UnityEditor;
using Nodin;
using Nodin.Editor;

public class MyEditorWindow : NodinEditorWindow
{
    [MenuItem("工具/我的编辑器")]
    public static void ShowWindow()
    {
        GetWindow<MyEditorWindow>("我的编辑器");
    }
    
    [FoldoutGroup("设置")]
    [LabelText("配置名称")]
    public string configName = "默认配置";
    
    [FoldoutGroup("设置")]
    [LabelText("启用调试")]
    public bool enableDebug = false;
    
    [Button("保存配置")]
    public void SaveConfig()
    {
        // 保存逻辑
    }
}
```

### 2. ScriptableObject 自动编辑器

Nodin 会自动为所有 `ScriptableObject` 生成编辑器，无需额外代码：

```csharp
using UnityEngine;
using Nodin;

[CreateAssetMenu(fileName = "EnemyData", menuName = "数据/敌人数据")]
public class EnemyData : ScriptableObject
{
    [FoldoutGroup("基础属性")]
    [LabelText("敌人名称")]
    public string enemyName;
    
    [FoldoutGroup("基础属性")]
    [LabelText("生命值")]
    public int health = 100;
    
    [FoldoutGroup("战斗属性")]
    [LabelText("攻击力")]
    public int attack = 10;
    
    [FoldoutGroup("战斗属性")]
    [LabelText("防御力")]
    public int defense = 5;
}
```

### 3. ValueDropdown 高级用法

```csharp
using UnityEngine;
using Nodin;
using Nodin.Editor;
using System.Collections.Generic;

public class DropdownExample : MonoBehaviour
{
    [LabelText("选择项目")]
    [ValueDropdown("GetProjectList")]
    public string selectedProject;
    
    [LabelText("选择数字")]
    [ValueDropdown("GetNumberList")]
    public int selectedNumber;
    
    private ValueDropdownList<string> GetProjectList()
    {
        var list = new ValueDropdownList<string>();
        list.Add("项目 A", "project_a");
        list.Add("项目 B", "project_b");
        list.Add("项目 C", "project_c");
        return list;
    }
    
    private IEnumerable<int> GetNumberList()
    {
        for (int i = 1; i <= 10; i++)
        {
            yield return i;
        }
    }
}
```

## 测试脚本

`Runtime/Test/NodinTest.cs` 覆盖了 Nodin 所有标签效果的测试，挂载到任意 GameObject 即可在 Inspector 中查看效果：

| 分组 | 测试内容 |
|------|----------|
| 绘制模式切换 | `useNodinDrawing` toggle — 按实例切换 Nodin/Odin 绘制 |
| 标签 & 显示 | `[LabelText]`、`[HideLabel]` |
| 信息提示 | `[InfoBox]` Info/Warning/Error + 条件显示 |
| 折叠分组 | `[FoldoutGroup]` 展开/折叠 + 子分组 |
| 开关分组 | `[ToggleGroup]` bool 开关 + 组内字段显隐 + 子分组联动 |
| 盒子分组 | `[BoxGroup]` |
| 条件显示 | `[ShowIf]`、`[HideIf]` 布尔和枚举条件 |
| 条件启用 | `[EnableIf]`、`[DisableIf]` |
| 只读字段 | `[ReadOnly]` |
| 多行文本 | `[MultiLineProperty]` |
| 按钮 | `[Button]` 普通/小/大/带颜色 |
| GUI 颜色 | `[GUIColor]` RGB 和 Hex |
| 非 Public 字段 | `[ShowInInspector]` |
| 下拉列表 | `[ValueDropdown]` |
| 路径选择 | `[FolderPath]` |
| 值改变回调 | `[OnValueChanged]` |
| 列表设置 | `[ListDrawerSettings]`（含 ▲▼ 排序按钮） |
| 字典设置 | `[DictionaryDrawerSettings]` |
| 标题装饰 | `[Title]` |
| 水平排列 | `[HorizontalGroup]` |
| 必填校验 | `[Required]` |
| 纯文本显示 | `[DisplayAsString]` |
| 最小值约束 | `[MinValue]` |
| 自定义 GUI | `[OnInspectorGUI]` |
| 资产限制 | `[AssetsOnly]` |

## 注意事项

1. **命名空间**: 所有特性都在 `Nodin` 命名空间下，使用时需要添加 `using Nodin;`

2. **条件显示**: `[ShowIf]`、`[HideIf]` 等条件特性支持字段、属性和无参布尔方法作为条件源

3. **性能优化**: `NodinDrawer` 在构造时一次性缓存所有字段/方法的 Attribute 元数据（`FieldMeta`/`MethodMeta`），后续 OnGUI 仅查询缓存，避免每帧重复反射调用。分组排序与子分组映射也在构造时预计算。ValueDropdown 选项首次解析后缓存。`GUIContent` / `GUIStyle` 复用静态实例，消除每帧 GC 分配

4. **兼容性**: 与 Unity 原生特性（如 `[Header]`、`[Tooltip]`、`[Range]`）完全兼容可混合使用，但项目规范推荐统一使用 Nodin 特性（`[LabelText]` 替代 `[Header]`/`[Tooltip]`，`[MinValue]` 替代 `[Range]`）

5. **编辑器脚本**: 编辑器相关代码（`NodinDrawer`、`NodinEditor` 等）必须放在 `Editor` 文件夹中

6. **子分组**: 使用 '/' 分隔符创建子分组，例如 `[FoldoutGroup("设置/高级")]`

7. **按钮参数**: `[Button]` 标记的方法支持默认参数，无参方法可直接调用

8. **Dictionary 序列化**: 使用 `Dictionary` 字段时必须继承 `NodinMonoBehaviour`，否则数据会在序列化时丢失。普通 `MonoBehaviour` 的 Nodin 属性绘制（`[LabelText]` 等）从 v1.3.0 起自动支持，无需修改继承

9. **Odin Inspector 共存（ScriptableObject）**: 当项目中同时安装了 Sirenix Odin Inspector 时，Odin 会为每个 `ScriptableObject` 子类型动态注册 `OdinEditor`，覆盖 Nodin 的通用注册。Nodin 通过 `EditorApplication.delayCall` 在 Odin 之后扫描所有含 Nodin 属性的 `ScriptableObject` 类型，直接在 `CustomEditorAttributes.kSCustomEditors` 中注册 `NodinEditor` 覆盖 Odin 的条目。此机制为一次性启动操作，无运行时性能开销。未安装 Odin 时跳过扫描（`HasOdin()` 守卫），零开销

10. **Odin Inspector 共存（MonoBehaviour 按实例切换）**: 继承 `NodinMonoBehaviour` 的类型可通过添加 `bool useNodinDrawing` 字段（配合 `[OnValueChanged]`）实现**按实例**切换 Nodin/Odin 绘制。`NodinMonoBehaviourEditor.OnInspectorGUI` 每帧读取该字段值：为 `true` 时使用 `NodinDrawer` 绘制，为 `false` 时委托给缓存的 `OdinEditor` 实例绘制。此机制仅影响当前选中的实例，不修改全局编辑器注册表，不影响其他实例

## 常见问题

### Q: 为什么我的字段没有显示？
A: 确保字段是 `public` 的，或者标记了 `[ShowInInspector]` 特性。同时检查是否有 `[HideInInspector]` 特性。从 v1.3.0 起，所有 MonoBehaviour（包括 `MonoSingleton<T>`）均自动支持 Nodin 属性绘制。

### Q: 如何让字段在特定条件下显示？
A: 使用 `[ShowIf]` 特性，并指定条件字段或方法名：
```csharp
[ShowIf("isEnabled")]
public float value;
```

### Q: 如何创建多级分组？
A: 使用 '/' 分隔符：
```csharp
[FoldoutGroup("父分组/子分组")]
public float value;
```

### Q: 按钮支持哪些尺寸？
A: 支持 `ButtonSizes.Small`、`ButtonSizes.Medium`、`ButtonSizes.Large` 三种尺寸。

### Q: 如何自定义下拉菜单选项？
A: 使用 `[ValueDropdown]` 特性，并指定返回选项列表的方法名：
```csharp
[ValueDropdown("GetOptions")]
public string selected;

private string[] GetOptions() => new[] { "选项1", "选项2", "选项3" };
```

### Q: Dictionary 字段数据丢失怎么办？
A: 必须继承 `NodinMonoBehaviour` 而非 `MonoBehaviour`。`NodinMonoBehaviour` 实现了 `ISerializationCallbackReceiver`，会在序列化时自动保存 Dictionary 数据。

### Q: 项目中安装了 Odin Inspector，Nodin 特性的 ScriptableObject 不生效？
A: Nodin v1.7.0+ 已自动处理 Odin 共存。启动时扫描所有含 Nodin 属性的 `ScriptableObject` 类型，在 `CustomEditorAttributes.kSCustomEditors` 中注册 `NodinEditor` 覆盖 Odin 的动态注册。控制台会输出 `[Nodin] 已为 N 个 ScriptableObject 类型注册 NodinEditor（覆盖 Odin）` 确认注册成功。未安装 Odin 时跳过扫描。如未生效，检查控制台是否有 `[Nodin] 注册 NodinEditor 失败` 警告。

### Q: 如何在 Nodin 和 Odin 之间按实例切换绘制？
A: 在继承 `NodinMonoBehaviour` 的类中添加 `bool useNodinDrawing` 字段（配合 `[OnValueChanged]`）。`NodinMonoBehaviourEditor` 会每帧读取该字段：为 `true` 时使用 Nodin 绘制，为 `false` 时委托给 Odin Editor 绘制。仅影响当前实例，不影响全局或其他实例。未安装 Odin 时关闭 toggle 会回退到原生绘制。参考 `NodinTest.cs` 中的实现。

### Q: 如何自定义 Dictionary 的列标签？
A: 使用 `[DictionaryDrawerSettings]` 特性：
```csharp
[DictionaryDrawerSettings(KeyLabel = "名称", ValueLabel = "数值")]
public Dictionary<string, int> data;
```

## 更新日志

### v1.7.0 (2026-07-25)
- **Odin 共存重写**: 彻底重写 Odin Inspector 共存机制。旧方案通过 `finishedDefaultHeaderGUI` 钩子在 header 区域绘制 Nodin 内容并用 `ExitGUI` 阻止 Odin body，存在两个问题：header 区域的下拉列表不可点击、`ExitGUI` 无法完全阻止 Odin 继续绘制导致重复显示。新方案在 `EditorApplication.delayCall`（确保在 Odin 注册之后）扫描所有含 Nodin 属性的 `ScriptableObject` 子类型，直接在 `CustomEditorAttributes.kSCustomEditors` 内部字典中注册 `NodinEditor` 覆盖 Odin 的 `OdinEditor` 条目，使 Unity 直接选中 `NodinEditor` 作为活跃编辑器
- **移除 finishedDefaultHeaderGUI 钩子**: `NodinEditor` 不再通过 header 钩子绘制 ScriptableObject，改为通过 `kSCustomEditors` 注册直接接管 `OnInspectorGUI`
- **移除 ExitGUI 机制**: 不再需要 `GUIUtility.ExitGUI()` 或 `ExitGUIException` 来阻止 Odin 绘制，消除布局状态损坏风险
- **移除 EnsureNodinPriority 旧方法**: 旧的 `Clear`/`RegisterEditorForType` 反射调用在目标 Unity 版本中不存在，已替换为直接操作 `kSCustomEditors` 字典
- **HasNodinAttributes 增加属性检测**: `NodinEditor.HasNodinAttributes` 新增对 `[ShowInInspector]`/`[LabelText]`/`[BoxGroup]`/`[FoldoutGroup]` 属性的检测，确保仅使用属性特性的 ScriptableObject 也能被正确识别
- **一次性启动开销，零运行时开销**: 注册仅在编辑器启动时执行一次（有 `_registered` 守卫），之后每帧 Inspector 绘制走原生 `CustomEditor` 路径，无回调、无反射、无异常
- **MonoBehaviour 按实例切换 Nodin/Odin 绘制**: `NodinMonoBehaviourEditor` 新增按实例委托机制。当目标对象含 `bool useNodinDrawing` 字段且值为 `false` 时，通过 `Editor.CreateEditor(target, OdinEditorType)` 创建缓存的 Odin Editor 实例委托绘制，`OnDisable` 时销毁。仅影响当前实例，不修改全局 `kSCustomEditors` 注册表，不影响其他实例。未安装 Odin 时回退到 `DrawDefaultInspector()`
- **未安装 Odin 时跳过启动扫描**: `RegisterNodinForNodinAttributedTypes` 新增 `HasOdin()` 守卫，无 Odin 时直接 return，避免不必要的全程序集扫描
- **`NodinCompat.OdinEditorType`**: 暴露 `OdinEditor` 类型供外部按需创建编辑器实例

### v1.6.0 (2026-07-25)
- **`[Serializable]` 自动内联绘制**: 标记了 `[Serializable]` 的类作为字段时自动内联绘制，带折叠头部 + helpBox 容器，无需额外添加 `[InlineProperty]`
- **`[ShowInInspector]` 属性支持**: 除字段外，标记了 `[ShowInInspector]` 的属性（如表达式体属性）也会被收集并绘制，支持 `[BoxGroup]`/`[LabelText]`/`[ReadOnly]` 等特性
- **`ValueDropdown` 枚举修复**: 枚举类型使用 `Enum.Parse` 替代 `Convert.ChangeType`，修复 `InvalidCastException`
- **内联绘制器缓存**: 内联 `[Serializable]` 对象的绘制器按字段名缓存，避免每帧重建丢失折叠状态
- **分组稳定排序**: 分组排序使用插入顺序作为 tiebreaker，保证 `Order` 相同时保持代码声明顺序
- **无分组按钮后置**: 未指定分组的 `[Button]` 方法绘制在所有分组之后，不再出现在数据上方
- **`[Serializable]` 脚本定位**: 内联绘制的 `[Serializable]` 类自动查找并显示对应的 MonoScript 引用，点击可在 Project 窗口中定位脚本文件
- **修复外部编辑器重复绘制**: `OnFinishedHeaderGUI` 钩子的跳过判断从命名空间字符串比较改为 `is NodinEditor` 类型检查，修复继承 `NodinEditor` 但不在 `Nodin.Editor` 命名空间的自定义编辑器（如 `FolderRuleConfigEditor`）被重复绘制的问题
- **Odin 共存兼容**: 检测到项目中存在 Odin Inspector 时，通过 `[InitializeOnLoadMethod]` 重新注册 NodinEditor 覆盖 Odin 的编辑器注册，确保 Nodin 优先于 Odin 成为活跃编辑器。hook 中保留 OdinEditor 跳过作为兜底

### v1.5.0 (2026-07-24)
- **BoxGroup 分组修复**: `[BoxGroup]` 现在正确作为分组绘制（此前仅检测但未参与分组渲染），当字段没有 `[FoldoutGroup]` 时自动作为后备分组
- **ToggleGroup 独立使用**: `[ToggleGroup]` 可单独使用，无需搭配 `[FoldoutGroup]`，自动创建带折叠头+开关的顶层分组
- **ToggleGroup + 子分组**: ToggleGroup 支持包含同名 FoldoutGroup 子分组（如 `[ToggleGroup("贴图导入规则")]` 内嵌 `[FoldoutGroup("贴图导入规则/公共参数")]`），子分组自动跟随开关状态
- **代码顺序绘制**: 所有分组（FoldoutGroup / BoxGroup / ToggleGroup）按源码字段首次出现顺序绘制，不再分批渲染

### v1.4.0 (2026-07-19)
- **List 拖拽排序**: 列表项新增左侧 `≡` 拖拽把手，按住拖动可自由排序，拖拽过程中显示蓝色插入指示线，被拖拽行半透明显示。拖拽放下即刻生效
- **Undo 撤回支持**: 所有列表和字典的修改操作（添加、删除、排序、拖拽、值编辑）均可通过 Ctrl+Z 撤回
- **修复拖拽覆盖 bug**: 修复拖拽时因绘制顺序导致行内容被意外覆盖的问题
- 保留原有 ▲▼ 按钮排序和 ✕ 删除功能

### v1.3.0 (2026-07-19)
- **MonoBehaviour 自动支持**: 新增通用 MonoBehaviour 编辑器（`NodinMonoBehaviourFallbackEditor`），自动检测 Nodin 属性并绘制。`MonoSingleton<T>` 等非 `NodinMonoBehaviour` 子类无需修改继承即可使用 `[LabelText]`、`[FoldoutGroup]` 等特性
- **新增 `[ToggleGroup]`**: 布尔开关分组，bool 字段作为标题开关控制组内其他字段的显隐
- **新增 `[Title]`**: 标题装饰，支持粗体/下划线样式
- **新增 `[Required]`**: Object 引用必填校验，空值时显示警告
- **新增 `[DisplayAsString]`**: 纯文本显示模式
- **新增 `[MinValue]`**: 数值最小值约束
- **新增 `[HorizontalGroup]`**: 字段水平排列
- **List UI 改进**: 列表项改为折叠式表头布局，新增 ▲▼ 排序按钮和 ✕ 删除按钮
- **修复列表排序 bug**: 修复上移/下移操作后因字段绘制覆盖导致交换失效的问题

### v1.2.0 (2026-07-18)
- 新增 `[DictionaryDrawerSettings]` 特性，支持自定义 Dictionary 的 Key/Value 列标签和只读模式
- 新增 `NodinMonoBehaviour` 基类，实现 Dictionary 字段的自动序列化（`ISerializationCallbackReceiver`）
- 新增 `NodinTest.cs` 测试脚本，覆盖所有 18 种标签效果

### v1.1.0 (2026-07-11)
- NodinDrawer 重构：构造时缓存所有字段/方法的 Attribute 元数据（`FieldMeta`/`MethodMeta`），消除每帧重复反射调用
- 预计算分组排序与子分组映射，避免每帧 LINQ 遍历
- 缓存 ValueDropdown 选项结果，首次解析后复用
- 复用静态 `GUIContent` / `GUIStyle` 实例，消除每帧 GC 分配

### v1.0.0 (2026-07-09)
- 初始版本发布
- 实现所有基础特性
- 支持自动绘制器
- 支持编辑器窗口和 ScriptableObject 自动编辑器

## 许可证

本项目采用 Apache-2.0 许可证，详情请参阅 [LICENSE](LICENSE) 文件。

## 联系方式

- **作者**: zko
- **GitHub**: https://github.com/PN-BUG

## 致谢

感谢所有为这个项目提供反馈和建议的开发者们。