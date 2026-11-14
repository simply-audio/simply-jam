# Sound, outputs and control

Where the sound lives, how to pick what you hear, and how to drive tussen over OSC. The rules and the ways to reach the hub are in [the skill](../SKILL.md).

## The sound is the routing, not the scene

- **Everything you hear is saved in the routing**, `~/Music/tussen/routing/default/routing.toml`, whatever scene plays: gains, mutes, mixes, each output's mix and filter, rewrites and remaps, plugins with their bindings and state.
- **A scene is the look**: `~/Music/tussen/scenes/<name>/scene.toml` holds the tempo, what it offers controllers, and its video. **A scene switch changes the look, the offers and the tempo, never the sound.**
- `scene_edit {scene, edit}` takes only `tempo`, `offers` and the video edits (`add_video_output`, `add_shader`, `shader_param`, …). Sound uses its own calls: `set_mix_gain`, `add_to_mix`, `update_destination`, `remap_upsert`, `update_input`, `plugin_load`.
- A breakdown-style move (the mic up, a send narrowed) is a gain or a pad, not a scene: `set_mix_gain`, or map a pad to `mix/main/channel/<input>/gain`.
- **`plugin_binds {slot, binds}`** sets a plugin slot's OSC bindings (`[{param, from, range?, curve?}]`), the list replaced, one undoable edit.
- A destination's `timing`, an announced device's follow switch, an OSC input's `filter`/`prefix`/rewrites and an output's `mute` are the machine's, in `tussen.toml`.

## Pick what you hear: mute an output

Main can play on several devices at once (laptop speakers, headphones, the PA). Mute the ones you don't want:

- `update_destination {name, mute: true}` on an `audio_out` output: it fades out and plays silence with its device open, so unmuting is instant. Saved in `tussen.toml`, undoable.
- A pad or OSC can do it live: the action `out/<output>/mute` (or `out/#n/mute`), a switch; over OSC `/tussen/action/out/<output>/mute` with 1 or 0. Live means not saved; an `update_destination {mute}` takes over from it.
- `get_state` shows `destinations[].output.mute` and, while a controller holds it, `live_mute`.

## Effects on one input or one output

`plugin_load {channel: "local·mic", …}` puts an effect on the mic only; on an output, its chain point. The window offers the same from each box's `+ effect`.

## Every action over OSC

- **Fire:** `/tussen/action/<action>` with a float (or int), where `<action>` is any string `list_actions` gives.
  - Switches (mute, solo, `out/…/mute`, `fx/…` switches): on above 0.5, off at or below.
  - Values (gains, pans, `plugin/<slot>/<param>`, `video/<output>/<shader>/<param>`, `knob/<name>`, `osc/<output>/<address>`): follow 0..1.
  - `scene/…` and `swap/…`: fire on the rising edge. A scene action goes through what the running scene offers.
- **State back:** a destination subscribed to `/tussen/action/` gets `/tussen/action/<action>/active` and `/armed` (1 or 0) on change, and once when added or on a device's hello. TouchOSC: bind a pad to `/tussen/action/<a>` and its LED to `/tussen/action/<a>/active`.
- **Takes:** actions sent over OSC are recorded and fire again on replay. Controller moves are not recorded yet.
