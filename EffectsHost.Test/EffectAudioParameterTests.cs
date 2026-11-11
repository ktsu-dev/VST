// Copyright (c) 2026 ktsu-dev contributors

namespace ktsu.EffectsHost.Test;

using ktsu.EffectsHost.Plugin;

/// <summary>
/// Tests for how <see cref="EffectAudioParameter"/> turns host-supplied plain values and text into
/// normalized values.
/// </summary>
[TestClass]
public sealed class EffectAudioParameterTests
{
	private static void ForEachContinuousParameter(Action<EffectAudioParameter> check)
	{
		using GainModel gain = new();
		using DelayModel delay = new();
		foreach (EffectAudioParameter parameter in gain.EffectParameters.Concat(delay.EffectParameters))
		{
			if (parameter.StepCount != 1)
			{
				check(parameter);
			}
		}
	}

	[TestMethod]
	[DataRow("-5")]
	[DataRow("nan")]
	[DataRow("NaN")]
	[DataRow("Infinity")]
	[DataRow("-Infinity")]
	[DataRow("1e400")]
	[DataRow("-1e400")]
	public void FromStringAlwaysGivesAFiniteNormalizedValue(string text)
	{
		ForEachContinuousParameter(parameter =>
		{
			double normalized = parameter.FromString(text);

			Assert.IsTrue(double.IsFinite(normalized), $"{parameter.Title}: FromString(\"{text}\") gave {normalized}");
			Assert.IsTrue(normalized is >= 0.0 and <= 1.0, $"{parameter.Title}: FromString(\"{text}\") gave {normalized}");
		});
	}

	[TestMethod]
	public void APlainValueOutsideTheRangeIsClampedToItsEnds()
	{
		ForEachContinuousParameter(parameter =>
		{
			Assert.AreEqual(0.0, parameter.ToNormalized(parameter.Descriptor.Range.Min - 1e6), 1e-12, parameter.Title);
			Assert.AreEqual(0.0, parameter.ToNormalized(double.NegativeInfinity), 1e-12, parameter.Title);
			Assert.AreEqual(1.0, parameter.ToNormalized(parameter.Descriptor.Range.Max + 1e6), 1e-12, parameter.Title);
			Assert.AreEqual(1.0, parameter.ToNormalized(double.PositiveInfinity), 1e-12, parameter.Title);
		});
	}

	[TestMethod]
	public void NaNLeavesTheParameterWhereItWas()
	{
		ForEachContinuousParameter(parameter =>
		{
			parameter.NormalizedValue = 0.25;

			Assert.AreEqual(0.25, parameter.ToNormalized(double.NaN), 1e-12, parameter.Title);
			Assert.AreEqual(0.25, parameter.FromString("nan"), 1e-12, parameter.Title);
		});
	}

	[TestMethod]
	public void AnInRangeValueStillMapsThroughTheTaper()
	{
		ForEachContinuousParameter(parameter =>
		{
			double plain = parameter.Descriptor.DefaultValue;

			Assert.AreEqual(parameter.Descriptor.Range.Normalize(plain), parameter.ToNormalized(plain), 1e-12, parameter.Title);
		});
	}
}
