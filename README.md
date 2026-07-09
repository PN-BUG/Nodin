# Nodin - No Odin Inspector

轻量级 Odin Inspector 替代方案，通过反射式自动绘制器提供 `[FoldoutGroup]`、`[LabelText]`、`[Button]`、`[ShowIf]` 等常用特性，无需安装任何第三方插件。

## 版本信息

- **版本**: 1.0.0
- **Unity 版本要求**: 2021.3+
- **许可证**: Apache-2.0
- **作者**: zko

## 功能特性

### 分组 & 布局
- **`[FoldoutGroup]`** - 将字段/方法归入可折叠分组，支持 '/' 分隔的子分组
- **`[BoxGroup]`** - 将字段归入带标题的盒子分组

### 标签 & 显示
- **`[LabelText]`** - 自定义 Inspector 中显示的标签文本
- **`[HideLabel]`** - 隐藏字段标签，仅显示值控件
- **`[InfoBox]`** - 在字段上方显示信息提示框（支持 Info/Warning/Error 类型）
- **`[MultiLineProperty]`** - 将字符串字段绘制为多行文本区域

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
- **`[ListDrawerSettings]`** - List 字段的绘制设置

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

## 支持的数据类型

Nodin 支持以下 Unity 数据类型的自动绘制：

- **基本类型**: `bool`, `int`, `long`, `float`, `double`, `string`
- **向量类型**: `Vector2`, `Vector3`, `Vector4`
- **颜色类型**: `Color`
- **矩形类型**: `Rect`
- **枚举类型**: 所有枚举类型
- **Unity 对象**: 所有继承自 `UnityEngine.Object` 的类型
- **列表类型**: `List<T>` 支持上述所有类型

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

## 注意事项

1. **命名空间**: 所有特性都在 `Nodin` 命名空间下，使用时需要添加 `using Nodin;`

2. **条件显示**: `[ShowIf]`、`[HideIf]` 等条件特性支持字段、属性和无参布尔方法作为条件源

3. **性能考虑**: 由于使用反射，大量字段的绘制可能会有轻微性能影响，但对于常规使用完全足够

4. **兼容性**: 与 Unity 原生特性（如 `[Header]`、`[Tooltip]`、`[Range]`）完全兼容，可以混合使用

5. **编辑器脚本**: 编辑器相关代码（`NodinDrawer`、`NodinEditor` 等）必须放在 `Editor` 文件夹中

6. **子分组**: 使用 '/' 分隔符创建子分组，例如 `[FoldoutGroup("设置/高级")]`

7. **按钮参数**: `[Button]` 标记的方法支持默认参数，无参方法可直接调用

## 常见问题

### Q: 为什么我的字段没有显示？
A: 确保字段是 `public` 的，或者标记了 `[ShowInInspector]` 特性。同时检查是否有 `[HideInInspector]` 特性。

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

## 更新日志

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