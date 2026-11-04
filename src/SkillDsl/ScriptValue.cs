using System.Globalization;

namespace SkillDsl;

/// <summary>脚本值的类型标签。</summary>
public enum ScriptValueType
{
	/// <summary>空值：变量不存在，或显式 <c>null</c>。</summary>
	Null = 0,

	/// <summary>数值。内部一律 <see cref="double"/>，避免 int / float 混用带来的取整歧义。</summary>
	Number = 1,

	/// <summary>布尔。</summary>
	Bool = 2,

	/// <summary>字符串。</summary>
	String = 3,
}

/// <summary>
/// DSL 中流转的唯一值类型（区分联合）。
/// </summary>
/// <remarks>
/// <para>刻意只支持四种类型。单位引用、坐标、列表等复杂类型由方法注册表的参数表在边界上转换，
/// 不进表达式内核——否则 <c>==</c> 的语义、序列化、以及 LLM 对数据的理解成本会同时失控。</para>
/// <para><b>没有隐式转换</b>：数值不能当布尔用（<c>hp &amp;&amp; x</c> 是错误，而不是"非零为真"）。
/// 这条约束保证同一份数据的语义不会随实现漂移。</para>
/// </remarks>
public readonly struct ScriptValue : IEquatable<ScriptValue>
{
	private readonly double _number;
	private readonly bool _boolean;
	private readonly string? _text;

	private ScriptValue(ScriptValueType type, double number, bool boolean, string? text)
	{
		Type = type;
		_number = number;
		_boolean = boolean;
		_text = text;
	}

	/// <summary>值的类型标签。</summary>
	public ScriptValueType Type { get; }

	/// <summary>空值（等价于 <c>default</c>）。</summary>
	public static ScriptValue Null => default;

	/// <summary>是否为空值。</summary>
	public bool IsNull => Type == ScriptValueType.Null;

	/// <summary>是否为数值。</summary>
	public bool IsNumber => Type == ScriptValueType.Number;

	/// <summary>是否为布尔。</summary>
	public bool IsBool => Type == ScriptValueType.Bool;

	/// <summary>是否为字符串。</summary>
	public bool IsString => Type == ScriptValueType.String;

	/// <summary>构造数值。</summary>
	public static ScriptValue FromNumber(double value)
		=> double.IsFinite(value)
			? new ScriptValue(ScriptValueType.Number, value, false, null)
			: throw new ScriptRuntimeException($"数值必须是有限数，实际为 {value}");

	/// <summary>构造布尔。</summary>
	public static ScriptValue FromBool(bool value)
		=> new(ScriptValueType.Bool, 0d, value, null);

	/// <summary>构造字符串。</summary>
	public static ScriptValue FromString(string value)
		=> value is null
			? throw new ArgumentNullException(nameof(value))
			: new ScriptValue(ScriptValueType.String, 0d, false, value);

	/// <summary>取数值；类型不符时抛 <see cref="ScriptRuntimeException"/>。</summary>
	public double AsNumber()
		=> Type == ScriptValueType.Number
			? _number
			: throw new ScriptRuntimeException($"期望数值，实际是 {Type}");

	/// <summary>取布尔；类型不符时抛 <see cref="ScriptRuntimeException"/>。</summary>
	public bool AsBool()
		=> Type == ScriptValueType.Bool
			? _boolean
			: throw new ScriptRuntimeException($"期望布尔，实际是 {Type}（本项目不做\"非零为真\"的隐式转换）");

	/// <summary>取字符串；类型不符时抛 <see cref="ScriptRuntimeException"/>。</summary>
	public string AsString()
		=> Type == ScriptValueType.String
			? _text!
			: throw new ScriptRuntimeException($"期望字符串，实际是 {Type}");

	/// <summary>按类型 + 值比较；类型不同即不相等（不做跨类型提升）。</summary>
	public bool Equals(ScriptValue other)
		=> Type == other.Type && Type switch
		{
			ScriptValueType.Null => true,
			ScriptValueType.Number => _number.Equals(other._number),
			ScriptValueType.Bool => _boolean == other._boolean,
			ScriptValueType.String => string.Equals(_text, other._text, StringComparison.Ordinal),
			_ => false,
		};

	/// <inheritdoc />
	public override bool Equals(object? obj) => obj is ScriptValue other && Equals(other);

	/// <inheritdoc />
	public override int GetHashCode()
		=> Type switch
		{
			ScriptValueType.Null => 0,
			ScriptValueType.Number => _number.GetHashCode(),
			ScriptValueType.Bool => _boolean.GetHashCode(),
			ScriptValueType.String => _text?.GetHashCode(StringComparison.Ordinal) ?? 0,
			_ => 0,
		};

	/// <summary>返回可读文本，用于日志与错误信息（<c>null</c> / <c>12.5</c> / <c>true</c> / <c>abc</c>）。</summary>
	public override string ToString()
		=> Type switch
		{
			ScriptValueType.Null => "null",
			ScriptValueType.Number => _number.ToString("0.####", CultureInfo.InvariantCulture),
			ScriptValueType.Bool => _boolean ? "true" : "false",
			ScriptValueType.String => _text!,
			_ => "?",
		};
}

/// <summary>DSL 求值期的错误（类型不符、除零、未知变量 / 函数等）。</summary>
public sealed class ScriptRuntimeException : Exception
{
	/// <summary>用一条说明构造。</summary>
	public ScriptRuntimeException(string message)
		: base(message)
	{
	}
}

/// <summary>DSL 语法错误，带出错位置（源码字符下标，0 基）。</summary>
public sealed class ScriptParseException : Exception
{
	/// <summary>用一条说明与出错位置构造。</summary>
	public ScriptParseException(string message, int position)
		: base($"{message}（位置 {position}）")
	{
		Position = position;
	}

	/// <summary>出错位置（源码字符下标，0 基）。</summary>
	public int Position { get; }
}
