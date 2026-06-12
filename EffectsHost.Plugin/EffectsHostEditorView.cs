// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.EffectsHost.Plugin;

using System.Globalization;

using Hexa.NET.ImGui;

using ktsu.ImGui.App;

using NPlug;

/// <summary>
/// The plugin's VST3 editor: a Dear ImGui parameter panel rendered through
/// <see cref="ImGuiApp.StartEmbedded"/> into the window the host provides.
/// </summary>
/// <typeparam name="TModel">The model type describing the effect's parameters.</typeparam>
/// <remarks>
/// The host owns the parent window; when it attaches the view, an embedded
/// <see cref="IImGuiAppSession"/> reparents an ImGui child window under the host's handle and runs
/// the render loop on its own UI thread. Host resize/focus callbacks are forwarded to the session,
/// and closing the editor disposes it. Parameter edits are wrapped in the controller's
/// begin/perform/end edit protocol so the host sees them as automation gestures.
/// </remarks>
internal sealed class EffectsHostEditorView<TModel> : IAudioPluginView
	where TModel : EffectsHostModel, new()
{
	private const int DefaultWidth = 480;
	private const int DefaultHeight = 320;
	private const int MinimumWidth = 320;
	private const int MinimumHeight = 200;

	private readonly EffectsHostController<TModel> controller;
	private readonly string[] sliderFormats;
	private IImGuiAppSession? session;

	/// <summary>
	/// Initializes a new instance of the <see cref="EffectsHostEditorView{TModel}"/> class.
	/// </summary>
	/// <param name="controller">The controller whose model this editor edits.</param>
	internal EffectsHostEditorView(EffectsHostController<TModel> controller)
	{
		this.controller = controller;

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
		platform == AudioPluginViewPlatform.Hwnd && OperatingSystem.IsWindows();

	/// <inheritdoc/>
	public void Attached(nint parent, AudioPluginViewPlatform type)
	{
		ImGuiAppConfig config = new()
		{
			Title = $"EffectsHost - {controller.Model.Effect.Name}",
			WindowHost = ImGuiAppWindowHost.EmbeddedChild,
			ParentWindowHandle = parent,
			SaveIniSettings = false,
			OnRender = RenderFrame,
		};

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
		// Input is delivered natively to the embedded child window.
	}

	/// <inheritdoc/>
	public void OnKeyDown(ushort key, short keyCode, short modifiers)
	{
		// Input is delivered natively to the embedded child window.
	}

	/// <inheritdoc/>
	public void OnKeyUp(ushort key, short keyCode, short modifiers)
	{
		// Input is delivered natively to the embedded child window.
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
		// ImGuiApp applies its own DPI handling to the embedded window.
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
			DrawBypassToggle();
			ImGui.Separator();
			DrawParameterSliders();
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

	private void DrawParameterSliders()
	{
		IReadOnlyList<EffectAudioParameter> parameters = controller.Model.EffectParameters;
		for (int i = 0; i < parameters.Count; i++)
		{
			EffectAudioParameter parameter = parameters[i];
			float plainValue = (float)parameter.ToPlain(parameter.NormalizedValue);
			float min = (float)parameter.Descriptor.Range.Min;
			float max = (float)parameter.Descriptor.Range.Max;

			bool changed = ImGui.SliderFloat(parameter.Title, ref plainValue, min, max, sliderFormats[i]);

			// VST3 edit protocol: a slider drag is one begin/perform.../end automation gesture.
			if (ImGui.IsItemActivated())
			{
				controller.BeginEditParameter(parameter);
			}

			if (changed && ReferenceEquals(controller.EditedParameter, parameter))
			{
				parameter.NormalizedValue = parameter.ToNormalized(plainValue);
			}

			if (ImGui.IsItemDeactivated() && ReferenceEquals(controller.EditedParameter, parameter))
			{
				controller.EndEditParameter();
			}
		}
	}
}
