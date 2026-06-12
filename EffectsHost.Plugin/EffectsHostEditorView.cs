// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.EffectsHost.Plugin;

using System.Globalization;
using System.Numerics;

using Hexa.NET.ImGui;

using ktsu.ImGui.App;
using ktsu.ImGui.Styler;
using ktsu.ImGui.Widgets;

using NPlug;

/// <summary>
/// The plugin's VST3 editor: a themed Dear ImGui parameter panel rendered through
/// <see cref="ImGuiApp.StartEmbedded"/>.
/// </summary>
/// <typeparam name="TModel">The model type describing the effect's parameters.</typeparam>
/// <remarks>
/// On Windows the editor renders as a docked child reparented into the window the host provides
/// (<see cref="ImGuiAppWindowHost.EmbeddedChild"/>). On other platforms, where embedded hosting is
/// not yet implemented upstream, it falls back to a floating standalone window driven by the same
/// non-blocking session API. Host resize/focus callbacks are forwarded to the session, and closing
/// the editor disposes it.
///
/// <para>
/// Continuous parameters render as <see cref="ImGuiWidgets"/> knobs with taper-correct plain
/// values; output level is shown on <c>DbMeter</c>s fed by the engine's lock-free telemetry ring;
/// presets are saved/loaded as <c>.vstpreset</c> files through <see cref="PresetCodec"/>.
/// Parameter edits are wrapped in the controller's begin/perform/end edit protocol so the host
/// sees them as automation gestures.
/// </para>
/// </remarks>
public sealed class EffectsHostEditorView<TModel> : IAudioPluginView
	where TModel : EffectsHostModel, new()
{
	private const int DefaultWidth = 480;
	private const int DefaultHeight = 320;
	private const int MinimumWidth = 320;
	private const int MinimumHeight = 200;
	private const string ThemeName = "Catppuccin Mocha";
	private const float MeterFloorDb = -60.0f;
	private const float MeterCeilDb = 6.0f;
	private const float PeakHoldDecayDbPerSecond = 20.0f;

	private readonly EffectsHostController<TModel> controller;
	private readonly string[] sliderFormats;
	private readonly PresetCodec presetCodec;
	private readonly string presetDirectory;
	private IImGuiAppSession? session;
	private MeterFrame lastMeterFrame;
	private float peakHoldLeftDb = float.NegativeInfinity;
	private float peakHoldRightDb = float.NegativeInfinity;
	private string[] presetFiles = [];
	private int selectedPresetIndex;
	private string presetStatus = string.Empty;

	/// <summary>
	/// Initializes a new instance of the <see cref="EffectsHostEditorView{TModel}"/> class.
	/// </summary>
	/// <param name="controller">The controller whose model this editor edits.</param>
	internal EffectsHostEditorView(EffectsHostController<TModel> controller)
	{
		this.controller = controller;
		presetCodec = new PresetCodec(controller.ProcessorClassId);
		presetDirectory = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			"ktsu.EffectsHost",
			"Presets",
			controller.Model.Effect.Name);

		IReadOnlyList<EffectAudioParameter> parameters = controller.Model.EffectParameters;
		sliderFormats = new string[parameters.Count];
		for (int i = 0; i < parameters.Count; i++)
		{
			EffectAudioParameter parameter = parameters[i];
			string units = string.IsNullOrEmpty(parameter.Descriptor.Units) ? string.Empty : $" {parameter.Descriptor.Units}";
			sliderFormats[i] = string.Create(CultureInfo.InvariantCulture, $"%.{parameter.Descriptor.DisplayPrecision}f{units}");
		}
	}

	/// <inheritdoc/>
	public ViewRectangle Size { get; private set; } = new(0, 0, DefaultWidth, DefaultHeight);

	/// <inheritdoc/>
	public bool IsPlatformTypeSupported(AudioPluginViewPlatform platform) =>
		(platform == AudioPluginViewPlatform.Hwnd && OperatingSystem.IsWindows())
		|| (platform == AudioPluginViewPlatform.NSView && OperatingSystem.IsMacOS())
		|| (platform == AudioPluginViewPlatform.X11EmbedWindowID && OperatingSystem.IsLinux());

	/// <inheritdoc/>
	public void Attached(nint parent, AudioPluginViewPlatform type)
	{
		// Docked child on Windows; floating standalone window elsewhere until upstream embedded
		// hosting covers those platforms.
		bool embed = type == AudioPluginViewPlatform.Hwnd && OperatingSystem.IsWindows();

		ImGuiAppConfig config = new()
		{
			Title = $"EffectsHost - {controller.Model.Effect.Name}",
			WindowHost = embed ? ImGuiAppWindowHost.EmbeddedChild : ImGuiAppWindowHost.Standalone,
			ParentWindowHandle = embed ? parent : 0,
			SaveIniSettings = false,
			OnStart = () => _ = Theme.Apply(ThemeName),
			OnRender = RenderFrame,
		};

		RefreshPresetList();
		session = ImGuiApp.StartEmbedded(config);
		session.Resize(Size.Size.Width, Size.Size.Height);
	}

	/// <inheritdoc/>
	public void Removed()
	{
		session?.Dispose();
		session = null;
	}

	/// <inheritdoc/>
	public void OnWheel(float distance)
	{
		// Input is delivered natively to the editor window.
	}

	/// <inheritdoc/>
	public void OnKeyDown(ushort key, short keyCode, short modifiers)
	{
		// Input is delivered natively to the editor window.
	}

	/// <inheritdoc/>
	public void OnKeyUp(ushort key, short keyCode, short modifiers)
	{
		// Input is delivered natively to the editor window.
	}

	/// <inheritdoc/>
	public void OnSize(ViewRectangle newSize)
	{
		Size = newSize;
		session?.Resize(newSize.Size.Width, newSize.Size.Height);
	}

	/// <inheritdoc/>
	public void OnFocus(bool state) => session?.Focus(state);

	/// <inheritdoc/>
	public void SetFrame(IAudioPluginFrame frame)
	{
		// The editor never initiates a resize, so the frame is not retained.
	}

	/// <inheritdoc/>
	public bool CanResize() => true;

	/// <inheritdoc/>
	public bool CheckSizeConstraint(ref ViewRectangle rect)
	{
		int width = Math.Max(rect.Size.Width, MinimumWidth);
		int height = Math.Max(rect.Size.Height, MinimumHeight);
		rect = new ViewRectangle(rect.Left, rect.Top, rect.Left + width, rect.Top + height);
		return true;
	}

	/// <inheritdoc/>
	public void SetContentScaleFactor(float factor)
	{
		// ImGuiApp applies its own DPI handling to the editor window.
	}

	/// <inheritdoc/>
	public bool TryFindParameter(int xPos, int yPos, out AudioParameterId parameterId)
	{
		parameterId = default;
		return false;
	}

	private void RenderFrame(float deltaSeconds)
	{
		ImGuiViewportPtr viewport = ImGui.GetMainViewport();
		ImGui.SetNextWindowPos(viewport.WorkPos);
		ImGui.SetNextWindowSize(viewport.WorkSize);

		if (ImGui.Begin("EffectsHost", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings))
		{
			ImGui.TextUnformatted(controller.Model.Effect.Name);
			ImGui.SameLine();
			DrawBypassToggle();
			ImGui.Separator();

			DrawParameterKnobs();
			ImGui.SameLine();
			DrawMeters(deltaSeconds);

			ImGui.Separator();
			DrawPresetControls();
		}

		ImGui.End();
	}

	private void DrawBypassToggle()
	{
		if (controller.Model.ByPassParameter is not { } byPassParameter)
		{
			return;
		}

		bool bypass = byPassParameter.NormalizedValue > 0.5;
		if (ImGui.Checkbox("Bypass", ref bypass))
		{
			controller.BeginEditParameter(byPassParameter);
			byPassParameter.NormalizedValue = bypass ? 1.0 : 0.0;
			controller.EndEditParameter();
		}
	}

	private void DrawParameterKnobs()
	{
		IReadOnlyList<EffectAudioParameter> parameters = controller.Model.EffectParameters;
		for (int i = 0; i < parameters.Count; i++)
		{
			if (i > 0)
			{
				ImGui.SameLine();
			}

			EffectAudioParameter parameter = parameters[i];
			float plainValue = (float)parameter.ToPlain(parameter.NormalizedValue);
			float min = (float)parameter.Descriptor.Range.Min;
			float max = (float)parameter.Descriptor.Range.Max;

			bool changed = parameter.StepCount == 1
				? DrawToggle(parameter, ref plainValue)
				: ImGuiWidgets.Knob(parameter.Title, ref plainValue, min, max, 0, sliderFormats[i]);

			// VST3 edit protocol: a knob drag is one begin/perform.../end automation gesture.
			if (ImGui.IsItemActivated())
			{
				controller.BeginEditParameter(parameter);
			}

			if (changed && ReferenceEquals(controller.EditedParameter, parameter))
			{
				parameter.NormalizedValue = parameter.ToNormalized(plainValue);

				// Also post straight to the audio thread's mailbox so the edit lands on the next
				// block instead of waiting for the host's parameter round-trip.
				_ = controller.Engine?.TryPostParameterChange(i, plainValue);
			}

			if (ImGui.IsItemDeactivated() && ReferenceEquals(controller.EditedParameter, parameter))
			{
				controller.EndEditParameter();
			}
		}
	}

	private static bool DrawToggle(EffectAudioParameter parameter, ref float plainValue)
	{
		bool enabled = parameter.NormalizedValue > 0.5;
		if (ImGui.Checkbox(parameter.Title, ref enabled))
		{
			plainValue = (float)parameter.ToPlain(enabled ? 1.0 : 0.0);
			return true;
		}

		return false;
	}

	private void DrawMeters(float deltaSeconds)
	{
		if (controller.Engine is not { } engine)
		{
			return;
		}

		// Drain everything the audio thread published since the last frame; display the newest.
		while (engine.TryReadTelemetry(out MeterFrame frame))
		{
			lastMeterFrame = frame;
		}

		float leftDb = ToDb(lastMeterFrame.PeakLeft);
		float rightDb = ToDb(lastMeterFrame.PeakRight);

		float decay = PeakHoldDecayDbPerSecond * deltaSeconds;
		peakHoldLeftDb = MathF.Max(leftDb, peakHoldLeftDb - decay);
		peakHoldRightDb = MathF.Max(rightDb, peakHoldRightDb - decay);

		ImGuiWidgets.DbMeter("L", leftDb, Vector2.Zero, MeterFloorDb, MeterCeilDb, peakHoldLeftDb);
		ImGui.SameLine();
		ImGuiWidgets.DbMeter("R", rightDb, Vector2.Zero, MeterFloorDb, MeterCeilDb, peakHoldRightDb);
	}

	private static float ToDb(float linearPeak) =>
		linearPeak <= 0.0f ? float.NegativeInfinity : 20.0f * MathF.Log10(linearPeak);

	private void DrawPresetControls()
	{
		ImGui.TextUnformatted("Presets");

		if (ImGui.Button("Save"))
		{
			SavePreset();
		}

		ImGui.SameLine();

		if (presetFiles.Length > 0)
		{
			selectedPresetIndex = Math.Clamp(selectedPresetIndex, 0, presetFiles.Length - 1);
			string preview = Path.GetFileNameWithoutExtension(presetFiles[selectedPresetIndex]);
			if (ImGui.BeginCombo("##preset", preview))
			{
				for (int i = 0; i < presetFiles.Length; i++)
				{
					if (ImGui.Selectable(Path.GetFileNameWithoutExtension(presetFiles[i]), i == selectedPresetIndex))
					{
						selectedPresetIndex = i;
					}
				}

				ImGui.EndCombo();
			}

			ImGui.SameLine();
			if (ImGui.Button("Load"))
			{
				LoadPreset(presetFiles[selectedPresetIndex]);
			}
		}
		else
		{
			ImGui.TextDisabled("No presets saved yet");
		}

		if (presetStatus.Length > 0)
		{
			ImGui.TextDisabled(presetStatus);
		}
	}

	private void SavePreset()
	{
		string fileName = string.Create(
			CultureInfo.InvariantCulture,
			$"{controller.Model.Effect.Name}-{DateTime.Now:yyyyMMdd-HHmmss}.vstpreset");
		string path = Path.Combine(presetDirectory, fileName);

		try
		{
			Directory.CreateDirectory(presetDirectory);
			presetCodec.Save(controller.CapturePresetState(), path);
			presetStatus = $"Saved {fileName}";
			RefreshPresetList();
		}
		catch (IOException exception)
		{
			presetStatus = $"Save failed: {exception.Message}";
		}
		catch (UnauthorizedAccessException exception)
		{
			presetStatus = $"Save failed: {exception.Message}";
		}
	}

	private void LoadPreset(string path)
	{
		try
		{
			controller.ApplyPresetState(presetCodec.Load(path));
			presetStatus = $"Loaded {Path.GetFileNameWithoutExtension(path)}";
		}
		catch (IOException exception)
		{
			presetStatus = $"Load failed: {exception.Message}";
		}
		catch (UnauthorizedAccessException exception)
		{
			presetStatus = $"Load failed: {exception.Message}";
		}
		catch (FormatException exception)
		{
			presetStatus = $"Load failed: {exception.Message}";
		}
		catch (InvalidDataException exception)
		{
			presetStatus = $"Load failed: {exception.Message}";
		}
		catch (System.Text.Json.JsonException exception)
		{
			presetStatus = $"Load failed: {exception.Message}";
		}
	}

	private void RefreshPresetList()
	{
		try
		{
			presetFiles = Directory.Exists(presetDirectory)
				? [.. Directory.EnumerateFiles(presetDirectory, "*.vstpreset").Order(StringComparer.OrdinalIgnoreCase)]
				: [];
		}
		catch (IOException)
		{
			presetFiles = [];
		}
		catch (UnauthorizedAccessException)
		{
			presetFiles = [];
		}
	}
}
