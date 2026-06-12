// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.EffectsHost.Core;

/// <summary>
/// Describes the processing environment an <see cref="IAudioEffect"/> is about to run in.
/// </summary>
/// <remarks>
/// The host calls <see cref="IAudioEffect.Prepare"/> with this setup before any call to
/// <see cref="IAudioEffect.ProcessBlock"/>, and again whenever the environment changes. All
/// buffers and coefficients that depend on the sample rate or block size must be allocated
/// there — <see cref="IAudioEffect.ProcessBlock"/> is not allowed to allocate.
/// </remarks>
/// <param name="SampleRate">The sample rate, in hertz, audio will be processed at.</param>
/// <param name="MaxBlockSamples">The maximum number of samples any single block can carry.</param>
public readonly record struct AudioEffectSetup(double SampleRate, int MaxBlockSamples);
