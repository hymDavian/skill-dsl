# skill-dsl

**把技能写成 JSON，而不是写成代码。**

一个可独立复用的声明式技能内核：技能引用效果，效果调用原子方法，参数是沙箱表达式。

这套内核在一套正在开发的游戏里已经跑了一段时间，抽成独立库后：**0 编译警告**（`TreatWarningsAsErrors` 开着）、**24 个单元测试全绿**。

---

## 它解决什么问题

技能系统的常见死法：每加一个技能就写一个类。三个月后 200 个类，谁也不敢动，平衡性调整要发版。

这里的做法是把技能拆成两层数据：

```jsonc
// 一个技能：什么时候能放、放一次花什么、放到第几层有什么效果
{
  "schema_version": 1,
  "id": "skill_backstep",
  "name": "后跃",
  "kind": "Active",
  "cast_time": 0.2,
  "interruptible": true,
  "cooldown": 5.0,
  "targeting": { "type": "Self" },
  "costs": [{ "resource": "stamina", "amount": 5 }],
  "levels": [
    { "level": 1, "effects": ["eff_turn_back", "eff_dash_3"] }
  ]
}
```

```jsonc
// 一个效果：调哪个原子方法、参数怎么算、什么时候生效
{
  "schema_version": 1,
  "id": "eff_damage_phys_30",
  "method": "unit.apply_damage",
  "params": {
    "amount": { "$expr": "0.8 * caster.str + 12" },
    "damage_type": "physical"
  },
  "delay": 0.25,
  "condition": "target.alive",
  "on_error": "Continue"
}
```

**加一门新功法，就是写两个 JSON 文件。不改代码、不发版、不需要重启服务器（热加载由上层决定）。**

---

## 三个核心物件

| 类型 | 职责 |
|---|---|
| `MethodSpec` | 一个原子方法：`unit.apply_damage`。带参数声明、所需能力位、副作用标记、说明文本 |
| `MethodRegistry` | 方法表。注册期校验，并按身份投影出"可调用方法列表" |
| `ScriptValue` / `Expression` | 沙箱表达式内核：只支持四种值，不允许调用任意 CLR 方法 |

效果与技能的数据结构见 `examples/`，解析与校验在宿主项目里（见下方「边界」）。

---

## 三条关键取舍

### 1. 错误在注册期爆炸，不在运行期爆炸

`MethodRegistry.Register` 会当场拒绝这些写法：

- Id 不是 `<域>.<动作>` 格式（如 `unit.apply_damage`）
- 参数重名
- **必填参数排在可选参数后面**——按位置绑定的话，这个参数永远传不进去，但编译器不会报错，只会在运行时永远返回 `E3212`
- `RequiredCapability` 声明了多个能力位——"需要 A 或 B"和"需要 A 且 B"会产生歧义，宁可让你拆成两个方法

最后两条是真实踩过的坑。它们属于"语法合法、语义必错"的类别，唯一划算的处理方式是启动即崩。

### 2. 一张方法表，两个消费者

同一份方法表既供运行时调用，也供大模型生成动作——后者是"工具表"。

```csharp
IReadOnlyList<MethodSpec> GetInvocableBy(ExecutionPrincipal principal);
```

返回结果**按 Id 排序**是刻意的：顺序一变，提示词缓存就失效，回归测试也开始飘。

### 3. 表达式是沙箱，不是脚本

`$expr` 只支持数字、布尔、字符串、null 四种值，支持四则运算和比较，**不允许**任意方法调用、循环、赋值。

原因是可回放：一个效果在服务器和客户端必须算出同一个数，否则存档对账永远对不上。随机走注入的种子服务，不走 `Random`。

---

## 跑起来

```bash
git clone <repo>
cd skill-dsl
dotnet test
```

预期：`通过: 24，失败: 0`。

---

## 目录

```
src/SkillDsl/          内核（方法表、表达式、脚本值）
tests/SkillDsl.Tests/  24 个单元测试
examples/skills/       技能数据示例
examples/effects/      效果数据示例
docs/为什么技能是数据.md  设计长文
```

---

## 边界：这个库不包含什么

这是**内核切片**，不是完整技能系统。以下部分留在宿主项目里：

- 世界状态与单位（`IScriptContext` 的实现方）
- 效果链执行与战斗结算
- JSON → 强类型的数据加载器与 Schema 迁移（依赖世界类型）
- 存档、回放、网络同步

这样切的原因很简单：能独立编译、能独立测试的东西才值得开源；拖着一整个世界类型图出来的"框架"，克隆下来跑不起来，只会浪费彼此时间。

---

## License

MIT，见 [LICENSE](./LICENSE)。

*by HYM*
