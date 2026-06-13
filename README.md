# EffectsHost

A .NET VST3 audio **effects host** built on [NPlug](https://github.com/xoofx/NPlug) and the
[ktsu](https://github.com/ktsu-dev) ecosystem. Each plugin instance runs exactly one effect;
writing a new DSP effect is one C# class implementing `IAudioEffect`, and the host shell provides
the parameters, automation, state persistence, and Dear ImGui editor for free.

## Projects

| Project | What it is |
| --- | --- |
| `EffectsHost.Core` | The effect abstraction: `IAudioEffect`, `EffectParameter` (backed by `ktsu.Semantics` audio quantities + `NormalizedParameter` tapers), `EffectBlock`. |
| `EffectsHost.Effects` | Concrete effects (`GainEffect`, `DelayEffect`). |
| `EffectsHost.Plugin` | The NPlug shell: model/processor/controller/plugin auto-built from an effect definition, the real-time `AudioEngine`, the embedded ImGui editor, and `.vstpreset` I/O. |
| `EffectsHost.Test` | DSP unit tests and the no-allocation soak test for the audio callback path. |

## Real-time safety contract

- The audio thread never allocates, locks, blocks, or performs I/O. All buffers are allocated in
  `Prepare`, and the soak test asserts zero GC allocations on the steady-state callback path.
- UI → audio parameter changes are marshalled with `ktsu.Invoker`'s non-blocking
  `TryBeginInvoke`, pumped at the top of each audio block.
- Audio → UI telemetry (metering) goes through `ktsu.Containers`' lock-free SPSC ring buffer.
- Delay effects use the denormal-aware `DelayLine` so feedback tails cannot cause CPU spikes.

## Editor

The editor is a themed (Catppuccin Mocha via `ktsu.ImGui.Styler`) Dear ImGui panel: taper-correct
knobs per parameter (`ktsu.ImGui.Widgets`), L/R dB meters with peak hold fed by the telemetry
ring, bypass, and `.vstpreset` save/load. On Windows it docks into the host's editor window via
`ImGuiApp.StartEmbedded`; on macOS/Linux it falls back to a floating window driven by the same
non-blocking session API until upstream embedded hosting covers those platforms.

## Building & installing

```sh
dotnet build
```

NPlug emits a native proxy that loads the CLR, so a Debug build is enough to load the plugin in a
host while developing — no NativeAOT publish required. To build and copy the plugin into a VST3
folder in one step:

```pwsh
./build/install-vst3.ps1                  # Windows: %CommonProgramFiles%\VST3\EffectsHost
./build/install-vst3.sh                   # macOS/Linux: user VST3 folder
```

See the [NPlug documentation](https://github.com/xoofx/NPlug/blob/main/doc/readme.md) for
NativeAOT publishing details.

## License

MIT — see [LICENSE.md](LICENSE.md). Note that distributing a VST3 plugin additionally falls under
[Steinberg's VST3 licensing](https://steinbergmedia.github.io/vst3_dev_portal/pages/VST+3+Licensing/Index.html).
