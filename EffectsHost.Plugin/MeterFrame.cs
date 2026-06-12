// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.EffectsHost.Plugin;

/// <summary>
/// One block's worth of metering telemetry published from the audio thread to the UI.
/// </summary>
/// <param name="PeakLeft">The peak absolute sample value of the left output channel.</param>
/// <param name="PeakRight">The peak absolute sample value of the right output channel.</param>
public readonly record struct MeterFrame(float PeakLeft, float PeakRight);
