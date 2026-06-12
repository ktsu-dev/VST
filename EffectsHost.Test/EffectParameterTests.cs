// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.EffectsHost.Test;

using ktsu.EffectsHost.Core;
using ktsu.Semantics;

/// <summary>
/// Tests for the <see cref="EffectParameter"/> descriptor and its taper round-trips.
/// </summary>
[TestClass]
public sealed class EffectParameterTests
{
	[TestMethod]
	public void DecibelParameterRoundTripsThroughNormalizedDomain()
	{
		EffectParameter parameter = EffectParameter.FromDecibels(
			"Gain",
			Decibels<double>.Create(-60.0),
			Decibels<double>.Create(12.0),
			Decibels<double>.Unity);

		Assert.AreEqual("dB", parameter.Units);
		Assert.AreEqual(0.0, parameter.DefaultValue);

		double normalizedDefault = parameter.Range.Normalize(parameter.DefaultValue);
		Assert.AreEqual(60.0 / 72.0, normalizedDefault, 1e-12);
		Assert.AreEqual(parameter.DefaultValue, parameter.Range.Denormalize(normalizedDefault), 1e-12);

		Assert.AreEqual(-60.0, parameter.Range.Denormalize(0.0), 1e-12);
		Assert.AreEqual(12.0, parameter.Range.Denormalize(1.0), 1e-12);
	}

	[TestMethod]
	public void LogarithmicParameterGivesEqualRatiosForEqualSteps()
	{
		EffectParameter parameter = EffectParameter.Logarithmic("Frequency", 20.0, 20000.0, 1000.0, "Hz");

		double quarter = parameter.Range.Denormalize(0.25);
		double half = parameter.Range.Denormalize(0.5);
		double threeQuarters = parameter.Range.Denormalize(0.75);

		// On a constant-ratio taper, equal normalized steps multiply the value by the same factor.
		Assert.AreEqual(half / quarter, threeQuarters / half, 1e-9);
		Assert.AreEqual(20.0, parameter.Range.Denormalize(0.0), 1e-9);
		Assert.AreEqual(20000.0, parameter.Range.Denormalize(1.0), 1e-6);
	}

	[TestMethod]
	public void PercentParameterUsesWholeNumberDisplayPrecision()
	{
		EffectParameter parameter = EffectParameter.FromPercent(
			"Mix",
			Percent<double>.Create(0.0),
			Percent<double>.Create(100.0),
			Percent<double>.Create(50.0));

		Assert.AreEqual(0, parameter.DisplayPrecision);
		Assert.AreEqual("%", parameter.Units);
		Assert.AreEqual(0.5, parameter.Range.Normalize(50.0), 1e-12);
	}

	[TestMethod]
	public void NormalizedValuesOutsideUnitRangeAreClamped()
	{
		EffectParameter parameter = EffectParameter.Linear("Drive", 0.0, 10.0, 5.0);

		Assert.AreEqual(0.0, parameter.Range.Denormalize(-0.5), 1e-12);
		Assert.AreEqual(10.0, parameter.Range.Denormalize(1.5), 1e-12);
	}
}
