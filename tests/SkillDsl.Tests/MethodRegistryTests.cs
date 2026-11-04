using SkillDsl;
using SkillDsl.Methods;
using Xunit;

namespace SkillDsl.Tests;

public class MethodRegistryTests
{
	private sealed class DictContext : IScriptContext
	{
		private readonly Dictionary<string, ScriptValue> _values = new(StringComparer.Ordinal);

		public DictContext With(string path, ScriptValue value)
		{
			_values[path] = value;
			return this;
		}

		public bool TryGetVariable(string path, out ScriptValue value)
			=> _values.TryGetValue(path, out value);
	}

	private static readonly DictContext EmptyContext = new();

	private static ParamSpec Required(string name, ParamType type)
		=> new() { Name = name, Type = type };

	private static ParamSpec Optional(string name, ParamType type, ScriptValue defaultValue)
		=> new() { Name = name, Type = type, Default = defaultValue };

	private static MethodSpec Method(
		string id,
		MethodCapability capability,
		MethodSideEffects sideEffects,
		Func<IReadOnlyList<ScriptValue>, ScriptValue> invoke,
		IReadOnlyList<ParamSpec>? parameters = null,
		ParamType? returnType = null)
		=> new()
		{
			Id = id,
			DisplayName = id,
			Parameters = parameters ?? Array.Empty<ParamSpec>(),
			ReturnType = returnType,
			RequiredCapability = capability,
			SideEffects = sideEffects,
			Description = id,
			Invoke = (args, _) => invoke(args),
		};

	private static MethodDispatcher BuildDispatcher(MethodRegistry registry)
		=> new(registry, ScriptFunctionTable.CreateDefault());

	// ── 注册校验 ────────────────────────────────────────────────────

	[Fact]
	public void Register_RejectsDuplicateId()
	{
		MethodRegistry registry = new();
		registry.Register(Method("unit.dummy", MethodCapability.None, MethodSideEffects.None, _ => ScriptValue.Null));

		Assert.Throws<InvalidOperationException>(() =>
			registry.Register(Method("unit.dummy", MethodCapability.None, MethodSideEffects.None, _ => ScriptValue.Null)));
	}

	[Fact]
	public void Register_RejectsIdWithoutDomainPrefix()
	{
		MethodRegistry registry = new();

		Assert.Throws<ArgumentException>(() =>
			registry.Register(Method("dummy", MethodCapability.None, MethodSideEffects.None, _ => ScriptValue.Null)));
	}

	[Fact]
	public void Register_RejectsMultiBitCapability()
	{
		MethodRegistry registry = new();

		Assert.Throws<ArgumentException>(() => registry.Register(Method(
			"unit.ambiguous",
			MethodCapability.Combat | MethodCapability.Economy,
			MethodSideEffects.StateChange,
			_ => ScriptValue.Null)));
	}

	// ── 基本派发 ────────────────────────────────────────────────────

	[Fact]
	public void Dispatch_ExecutesMethodAndReturnsValue()
	{
		MethodRegistry registry = new();
		registry.Register(Method(
			"math.double",
			MethodCapability.None,
			MethodSideEffects.None,
			args => ScriptValue.FromNumber(args[0].AsNumber() * 2d),
			new[] { Required("value", ParamType.Number) },
			ParamType.Number));

		MethodDispatchResult result = BuildDispatcher(registry).Dispatch(
			"math.double",
			new MethodArgument[] { new MethodArgument.Constant(ScriptValue.FromNumber(21d)) },
			ExecutionPrincipal.System(),
			EmptyContext);

		Assert.True(result.Ok);
		Assert.Equal(42d, result.Return.AsNumber(), 6);
	}

	[Fact]
	public void Dispatch_EvaluatesComputedArgument()
	{
		MethodRegistry registry = new();
		registry.Register(Method(
			"math.double",
			MethodCapability.None,
			MethodSideEffects.None,
			args => ScriptValue.FromNumber(args[0].AsNumber() * 2d),
			new[] { Required("value", ParamType.Number) },
			ParamType.Number));

		DictContext context = new DictContext()
			.With("skill.level", ScriptValue.FromNumber(2d))
			.With("bonus", ScriptValue.FromNumber(3d));

		MethodDispatchResult result = BuildDispatcher(registry).Dispatch(
			"math.double",
			new MethodArgument[] { new MethodArgument.Computed(ExpressionParser.Parse("skill.level * 2 + bonus")) },
			ExecutionPrincipal.System(),
			context);

		Assert.True(result.Ok);
		Assert.Equal(14d, result.Return.AsNumber(), 6); // (2*2+3) * 2
	}

	[Fact]
	public void Dispatch_UsesDefaultWhenArgumentOmitted()
	{
		MethodRegistry registry = new();
		registry.Register(Method(
			"unit.heal",
			MethodCapability.Combat,
			MethodSideEffects.StateChange,
			args => ScriptValue.FromNumber(args[0].AsNumber() + args[1].AsNumber()),
			new[] { Required("amount", ParamType.Number), Optional("bonus", ParamType.Number, ScriptValue.FromNumber(5d)) },
			ParamType.Number));

		MethodDispatchResult result = BuildDispatcher(registry).Dispatch(
			"unit.heal",
			new MethodArgument[] { new MethodArgument.Constant(ScriptValue.FromNumber(10d)) },
			ExecutionPrincipal.Rule(),
			EmptyContext);

		Assert.True(result.Ok);
		Assert.Equal(15d, result.Return.AsNumber(), 6);
	}

	[Fact]
	public void Dispatch_ReportsMissingRequiredArgument()
	{
		MethodRegistry registry = new();
		registry.Register(Method(
			"unit.damage",
			MethodCapability.Combat,
			MethodSideEffects.StateChange,
			_ => ScriptValue.Null,
			new[] { Required("amount", ParamType.Number) }));

		MethodDispatchResult result = BuildDispatcher(registry).Dispatch(
			"unit.damage",
			Array.Empty<MethodArgument>(),
			ExecutionPrincipal.Rule(),
			EmptyContext);

		Assert.False(result.Ok);
		Assert.Equal(MethodErrorCodes.MissingRequiredArgument, result.ErrorCode);
	}

	[Fact]
	public void Dispatch_ReportsUnknownMethod()
	{
		MethodDispatchResult result = BuildDispatcher(new MethodRegistry()).Dispatch(
			"unit.does_not_exist",
			null,
			ExecutionPrincipal.System(),
			EmptyContext);

		Assert.False(result.Ok);
		Assert.Equal(MethodErrorCodes.UnknownMethod, result.ErrorCode);
	}

	[Fact]
	public void Dispatch_ReportsTypeMismatch()
	{
		MethodRegistry registry = new();
		registry.Register(Method(
			"unit.damage",
			MethodCapability.Combat,
			MethodSideEffects.StateChange,
			_ => ScriptValue.Null,
			new[] { Required("amount", ParamType.Number) }));

		MethodDispatchResult result = BuildDispatcher(registry).Dispatch(
			"unit.damage",
			new MethodArgument[] { new MethodArgument.Constant(ScriptValue.FromBool(true)) },
			ExecutionPrincipal.Rule(),
			EmptyContext);

		Assert.False(result.Ok);
		Assert.Equal(MethodErrorCodes.ArgumentTypeMismatch, result.ErrorCode);
		Assert.Contains("amount", result.Message);
	}

	[Fact]
	public void Dispatch_ReportsEvaluationFailure()
	{
		MethodRegistry registry = new();
		registry.Register(Method(
			"unit.damage",
			MethodCapability.Combat,
			MethodSideEffects.StateChange,
			_ => ScriptValue.Null,
			new[] { Required("amount", ParamType.Number) }));

		MethodDispatchResult result = BuildDispatcher(registry).Dispatch(
			"unit.damage",
			new MethodArgument[] { new MethodArgument.Computed(ExpressionParser.Parse("missing + 1")) },
			ExecutionPrincipal.Rule(),
			EmptyContext);

		Assert.False(result.Ok);
		Assert.Equal(MethodErrorCodes.ArgumentEvaluationFailed, result.ErrorCode);
	}

	[Fact]
	public void Dispatch_ReportsTooManyArguments()
	{
		MethodRegistry registry = new();
		registry.Register(Method(
			"unit.damage",
			MethodCapability.Combat,
			MethodSideEffects.StateChange,
			_ => ScriptValue.Null,
			new[] { Required("amount", ParamType.Number) }));

		MethodDispatchResult result = BuildDispatcher(registry).Dispatch(
			"unit.damage",
			new MethodArgument[]
			{
				new MethodArgument.Constant(ScriptValue.FromNumber(1d)),
				new MethodArgument.Constant(ScriptValue.FromNumber(2d)),
			},
			ExecutionPrincipal.Rule(),
			EmptyContext);

		Assert.False(result.Ok);
		Assert.Equal(MethodErrorCodes.TooManyArguments, result.ErrorCode);
	}

	[Fact]
	public void Dispatch_ReportsInvocationFailure()
	{
		MethodRegistry registry = new();
		registry.Register(Method(
			"unit.explode",
			MethodCapability.Combat,
			MethodSideEffects.StateChange,
			_ => throw new InvalidOperationException("boom")));

		MethodDispatchResult result = BuildDispatcher(registry).Dispatch(
			"unit.explode",
			null,
			ExecutionPrincipal.Rule(),
			EmptyContext);

		Assert.False(result.Ok);
		Assert.Equal(MethodErrorCodes.InvocationFailed, result.ErrorCode);
		Assert.Contains("boom", result.Message);
	}

	// ── 授权矩阵 ────────────────────────────────────────────────────

	[Fact]
	public void NpcAgent_IsDeniedCombatMethod()
	{
		MethodRegistry registry = new();
		registry.Register(Method("unit.damage", MethodCapability.Combat, MethodSideEffects.StateChange, _ => ScriptValue.Null));

		MethodDispatchResult result = BuildDispatcher(registry).Dispatch(
			"unit.damage", null, ExecutionPrincipal.NpcAgent("npc_qingxi_chenjiujin"), EmptyContext);

		Assert.False(result.Ok);
		Assert.Equal(MethodErrorCodes.CapabilityDenied, result.ErrorCode);
	}

	[Fact]
	public void NpcAgent_IsDeniedEconomyMethod()
	{
		MethodRegistry registry = new();
		registry.Register(Method("econ.transfer_item", MethodCapability.Economy, MethodSideEffects.StateChange, _ => ScriptValue.Null));

		MethodDispatchResult result = BuildDispatcher(registry).Dispatch(
			"econ.transfer_item", null, ExecutionPrincipal.NpcAgent("npc_x"), EmptyContext);

		Assert.False(result.Ok);
		Assert.Equal(MethodErrorCodes.CapabilityDenied, result.ErrorCode);
	}

	[Fact]
	public void NpcAgent_IsAllowedMindMethod()
	{
		MethodRegistry registry = new();
		registry.Register(Method("agent.write_memory", MethodCapability.NpcMind, MethodSideEffects.StateChange, _ => ScriptValue.Null));

		MethodDispatchResult result = BuildDispatcher(registry).Dispatch(
			"agent.write_memory", null, ExecutionPrincipal.NpcAgent("npc_x"), EmptyContext);

		Assert.True(result.Ok);
	}

	[Fact]
	public void Rule_IsAllowedCombatAndEconomy()
	{
		MethodRegistry registry = new();
		registry.Register(Method("unit.damage", MethodCapability.Combat, MethodSideEffects.StateChange, _ => ScriptValue.Null));
		registry.Register(Method("econ.transfer_item", MethodCapability.Economy, MethodSideEffects.StateChange, _ => ScriptValue.Null));

		MethodDispatcher dispatcher = BuildDispatcher(registry);

		Assert.True(dispatcher.Dispatch("unit.damage", null, ExecutionPrincipal.Rule(), EmptyContext).Ok);
		Assert.True(dispatcher.Dispatch("econ.transfer_item", null, ExecutionPrincipal.Rule(), EmptyContext).Ok);
	}

	[Fact]
	public void WorldAgent_IsDeniedCombatButAllowedWorldState()
	{
		MethodRegistry registry = new();
		registry.Register(Method("unit.damage", MethodCapability.Combat, MethodSideEffects.StateChange, _ => ScriptValue.Null));
		registry.Register(Method("world.set_flag", MethodCapability.WorldState, MethodSideEffects.StateChange, _ => ScriptValue.Null));

		MethodDispatcher dispatcher = BuildDispatcher(registry);
		ExecutionPrincipal worldAgent = ExecutionPrincipal.WorldAgent("wuchang");

		Assert.False(dispatcher.Dispatch("unit.damage", null, worldAgent, EmptyContext).Ok);
		Assert.True(dispatcher.Dispatch("world.set_flag", null, worldAgent, EmptyContext).Ok);
	}

	[Fact]
	public void Debug_IsDeniedAnySideEffectingMethod()
	{
		MethodRegistry registry = new();
		registry.Register(Method("world.set_flag", MethodCapability.WorldState, MethodSideEffects.StateChange, _ => ScriptValue.Null));

		MethodDispatchResult result = BuildDispatcher(registry).Dispatch(
			"world.set_flag", null, ExecutionPrincipal.Debug(), EmptyContext);

		Assert.False(result.Ok);
		Assert.Equal(MethodErrorCodes.CapabilityDenied, result.ErrorCode);
	}

	[Fact]
	public void Debug_IsAllowedPureQuery()
	{
		MethodRegistry registry = new();
		registry.Register(Method(
			"world.get_flag",
			MethodCapability.WorldState,
			MethodSideEffects.None,
			_ => ScriptValue.FromBool(true),
			returnType: ParamType.Bool));

		MethodDispatchResult result = BuildDispatcher(registry).Dispatch(
			"world.get_flag", null, ExecutionPrincipal.Debug(), EmptyContext);

		Assert.True(result.Ok);
		Assert.True(result.Return.AsBool());
	}

	[Fact]
	public void CapabilityFreeQuery_IsOpenToEveryPrincipal()
	{
		MethodRegistry registry = new();
		registry.Register(Method("math.pi", MethodCapability.None, MethodSideEffects.None, _ => ScriptValue.FromNumber(Math.PI), returnType: ParamType.Number));

		MethodDispatcher dispatcher = BuildDispatcher(registry);

		foreach (ExecutionPrincipal principal in new[]
		{
			ExecutionPrincipal.System(),
			ExecutionPrincipal.WorldAgent("w"),
			ExecutionPrincipal.NpcAgent("n"),
			ExecutionPrincipal.Rule(),
			ExecutionPrincipal.Debug(),
		})
		{
			Assert.True(dispatcher.Dispatch("math.pi", null, principal, EmptyContext).Ok);
		}
	}

	[Fact]
	public void DeniedCall_DoesNotExecuteTheMethod()
	{
		int executions = 0;
		MethodRegistry registry = new();
		registry.Register(Method(
			"unit.damage",
			MethodCapability.Combat,
			MethodSideEffects.StateChange,
			_ =>
			{
				executions++;
				return ScriptValue.Null;
			}));

		BuildDispatcher(registry).Dispatch("unit.damage", null, ExecutionPrincipal.NpcAgent("npc_x"), EmptyContext);

		Assert.Equal(0, executions);
	}

	// ── 工具表投影 ──────────────────────────────────────────────────

	[Fact]
	public void GetInvocableBy_FiltersByCapabilityAndSortsById()
	{
		MethodRegistry registry = new();
		registry.Register(Method("a.query", MethodCapability.None, MethodSideEffects.None, _ => ScriptValue.Null));
		registry.Register(Method("b.combat", MethodCapability.Combat, MethodSideEffects.StateChange, _ => ScriptValue.Null));
		registry.Register(Method("c.econ", MethodCapability.Economy, MethodSideEffects.StateChange, _ => ScriptValue.Null));
		registry.Register(Method("d.mind", MethodCapability.NpcMind, MethodSideEffects.StateChange, _ => ScriptValue.Null));

		IReadOnlyList<MethodSpec> forNpc = registry.GetInvocableBy(ExecutionPrincipal.NpcAgent("npc_x"));
		IReadOnlyList<MethodSpec> forRule = registry.GetInvocableBy(ExecutionPrincipal.Rule());

		Assert.Equal(new[] { "a.query", "d.mind" }, forNpc.Select(method => method.Id));
		Assert.Equal(new[] { "a.query", "b.combat", "c.econ" }, forRule.Select(method => method.Id));

		// 这条就是"模型根本看不见越权方法"的证据。
		Assert.DoesNotContain(forNpc, method => method.Id == "b.combat");
		Assert.DoesNotContain(forNpc, method => method.Id == "c.econ");
	}

	// ── 参数声明校验 ────────────────────────────────────────

	[Fact]
	public void Register_RejectsRequiredParameterAfterOptional()
	{
		// 参数按位置绑定，跳过可选参数去传必填参数是不可能的：
		// 这种声明不会报错，但永远无法被正确调用（总是 E3212）。
		MethodRegistry registry = new();

		Assert.Throws<ArgumentException>(() => registry.Register(Method(
			"unit.bad_signature",
			MethodCapability.Combat,
			MethodSideEffects.StateChange,
			_ => ScriptValue.Null,
			new[]
			{
				Optional("amount", ParamType.Number, ScriptValue.FromNumber(1d)),
				Required("target", ParamType.String),
			})));
	}

	[Fact]
	public void Register_AcceptsRequiredParametersBeforeOptional()
	{
		MethodRegistry registry = new();

		registry.Register(Method(
			"unit.good_signature",
			MethodCapability.Combat,
			MethodSideEffects.StateChange,
			_ => ScriptValue.Null,
			new[]
			{
				Required("target", ParamType.String),
				Optional("amount", ParamType.Number, ScriptValue.FromNumber(1d)),
			}));

		Assert.Equal(1, registry.Count);
	}
}
