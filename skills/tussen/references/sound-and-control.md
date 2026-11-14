# Sound, outputs and control

Where the sound lives, how to pick what you hear, and how to drive tussen over OSC. The rules and the ways to reach the hub are in [the skill](../SKILL.md).

## The layers: the sound is the routing, the look is the vibe

- **Everything you hear is saved in the routing**, `~/Music/tussen/routing/default/routing.toml`, whatever scene plays: gains, mutes, mixes, each output's mix and filter, rewrites and remaps, plugins with their bindings and state.
- **A vibe is the look**: `~/Music/tussen/vibes/<name>/vibe.toml` holds the looks, the tempo and what it offers controllers. **A vibe switch changes the look, the offers and the tempo, never the sound.** `tussen.toml`'s `[layers]` names which vibe and routing run (`vibe = "…"`, `routing = "…"`).
- **The layer calls:** `list_layers {layer?}`; `layer_switch {layer: "vibe", name, at: "next_bar"}`; `layer_edit {layer: "vibe", name, edit}` (only `tempo`, `offers` and the looks); `layer_save|layer_lock|layer_revert {layer, name?}`; `layer_new|layer_rename|layer_delete|layer_save_as`; `vibe_try {take, vibe?}`; `reload {config?, vibe?, routing?}`. Sound uses its own calls: `set_mix_gain`, `add_to_mix`, `update_destination`, `remap_upsert`, `update_input`, `plugin_load`. Switching routings isn't built yet; a routing file edited by hand shows in `changed_on_disk` and `reload {routing}` takes it in.
- `get_state` shows `layers.vibe` (current, pending, dirty, locked) and `layers.routing` (its `missing` lists outputs and channels this machine lacks).
- A breakdown-style move (the mic up, a send narrowed) is a gain or a pad, not a vibe: `set_mix_gain`, or map a pad to `mix/main/channel/<input>/gain`.
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
  - `vibe/…` (`vibe/next`, `vibe/prev`, `vibe/<name>`) and `swap/…`: fire on the rising edge. A vibe action goes through what the running vibe offers.
- **The set list** orders the show: `set_list {"vibes": ["intro", "verse", "drop"]}` (the whole order; `[]` clears it; one undoable edit; `get_state` → `layers.vibe.set`; saved as `tussen.toml` `[set] vibes`). `vibe/next` and `vibe/prev` step through it in that order and **stop at its ends**; playing a vibe not in the set, `next` goes to its first and `prev` to its last. **With no set they step through every vibe alphabetically and wrap.** `vibe/<name>` switches to any vibe. `layer_rename` keeps a vibe's place in the set, `layer_delete` takes it out.
- **State back:** a destination subscribed to `/tussen/action/` gets `/tussen/action/<action>/active` and `/armed` (1 or 0) on change, and once when added or on a device's hello. TouchOSC: bind a pad to `/tussen/action/<a>` and its LED to `/tussen/action/<a>/active`.
- **Takes:** actions sent over OSC, and everything a claimed controller does, are recorded as `/tussen/action/<action>` messages and fire again on replay, on time. A latched value is recorded as held. A take started mid-run doesn't fire earlier moves.
- **A controller's live mute:** while a pad holds a mute over the saved one, `get_state` shows `inputs[].channels[].live_mute` (and `destinations[].output.live_mute` for outputs). `update_input {mute}` / `update_destination {mute}` takes it back.

## Video goes through a mix

- A picture goes **input → video mix → output**. Make a mix (`add_mix {name, kind: "video", inputs: ["cam"]}`), then an output that shows it (`add_video_output {name, mix, ndi?, window?, display?}`). `add_to_mix {mix, input}` adds a camera; `remove_mix {kind: "video", name}` / `rename_mix {kind: "video", name, to}` take one out or rename it.
- The shaders are the vibe's **looks**, on a mix: shader calls take `mix` (`add_shader {mix, file}`, `set_shader_param {mix, slot, param, value}`, `list_shaders`, `check_shader {mix, slot, source}`). `set_mix_inputs {mix, inputs}` sets a mix's cameras; `set_video_output {output, mix?, ndi?, window?, display?}` picks what an output shows.
- Where each lives: the mix's cameras in the routing, the looks in the vibe, an output's NDI name and display in `tussen.toml`. A vibe switch changes the looks, never the cameras.
- Actions keep their form: `video/<mix>/<look>/<param>`, `swap/video/<mix>/shader|input|fade/…`.

## Mapping sets

- Controller mappings come in **sets**: `~/.config/tussen/mappings/<set>/<controller>.toml`. `tussen.toml`'s `[layers] mappings = "<set>"` names the one running (`default` when absent).
- `layer_switch {layer: "mappings", name}` switches **at once** (never on the bar): live values stay, a Pickup control waits to meet its new target, and it isn't an undo step. Actions `mappings/<set>`, `mappings/next`, `mappings/prev`, from a pad or OSC.
- `list_layers {layer: "mappings"}`; `layer_new {layer: "mappings", name, from?}`, `layer_save_as`, `layer_rename`, `layer_delete` (never the running set). Rows are edited with `set_controller_mapping`, which writes the running set's file.
- `get_state`'s `layers.mappings.current` names the running set; `folders.mappings.problem` says when an old `~/.config/tussen/controllers/` folder still needs moving to `mappings/default/` (until then controllers play their model's defaults and edits aren't saved).

