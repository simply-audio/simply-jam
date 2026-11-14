# Sound, outputs and control

Where the sound lives, how to pick what you hear, and how to drive tussen over OSC. The rules and the ways to reach the hub are in [the skill](../SKILL.md).

## The layers: the sound is the routing, the look is the vibe

- **Everything you hear is saved in the routing**, `~/Music/tussen/routing/default/routing.toml`, whatever scene plays: gains, mutes, mixes, each output's mix and filter, rewrites and remaps, plugins with their bindings and state.
- **A vibe is the look**: `~/Music/tussen/vibes/<name>/vibe.toml` holds the looks, the tempo and what it offers controllers. **A vibe switch changes the look, the offers and the tempo, never the sound.** `tussen.toml`'s `[layers]` names which vibe and routing run (`vibe = "…"`, `routing = "…"`).
- **The layer calls:** `list_layers {layer?}`; `layer_switch {layer: "vibe", name, at: "next_bar"}`; `layer_edit {layer: "vibe", name, edit}` (only `tempo`, `offers` and the looks); `layer_save|layer_lock|layer_revert {layer, name?}`; `layer_new|layer_rename|layer_delete|layer_save_as`; `vibe_try {take, vibe?}`; `reload {config?, vibe?, routing?, inputs?, outputs?}`. Sound uses its own calls: `set_mix_gain`, `add_to_mix`, `update_destination`, `remap_upsert`, `update_input`, `plugin_load`. A routing file edited by hand shows in `changed_on_disk` and `reload {routing}` takes it in.
- `get_state` shows `layers.vibe` (current, pending, dirty, locked) and `layers.routing` (its `missing` lists outputs and channels this machine lacks).
- A breakdown-style move (the mic up, a send narrowed) is a gain, a pad, or a **routing switch**, never a vibe switch (see below).
- **`plugin_binds {slot, binds}`** sets a plugin slot's OSC bindings (`[{param, from, range?, curve?}]`), the list replaced, one undoable edit.
- A destination's `timing`, an announced device's follow switch and an output's `mute` are in the outputs file; an OSC input's `filter`/`prefix`/rewrites in the inputs file (see *Inputs and outputs*, below).

## Pick what you hear: mute an output

Main can play on several devices at once (laptop speakers, headphones, the PA). Mute the ones you don't want:

- `update_destination {name, mute: true}` on an `audio_out` output: it fades out and plays silence with its device open, so unmuting is instant. Saved in the outputs file, undoable.
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
- Where each lives: the mix's cameras in the routing, the looks in the vibe, an output's NDI name and display in the outputs file. A vibe switch changes the looks, never the cameras.
- Actions keep their form: `video/<mix>/<look>/<param>`, `swap/video/<mix>/shader|input|fade/…`.

## Mapping sets

- Controller mappings come in **sets**: `~/.config/tussen/mappings/<set>/<controller>.toml`. `tussen.toml`'s `[layers] mappings = "<set>"` names the one running (`default` when absent).
- `layer_switch {layer: "mappings", name}` switches **at once** (never on the bar): live values stay, a Pickup control waits to meet its new target, and it isn't an undo step. Actions `mappings/<set>`, `mappings/next`, `mappings/prev`, from a pad or OSC.
- `list_layers {layer: "mappings"}`; `layer_new {layer: "mappings", name, from?}`, `layer_save_as`, `layer_rename`, `layer_delete` (never the running set). Rows are edited with `set_controller_mapping`, which writes the running set's file.
- `get_state`'s `layers.mappings.current` names the running set; `folders.mappings.problem` says when an old `~/.config/tussen/controllers/` folder still needs moving to `mappings/default/` (until then controllers play their model's defaults and edits aren't saved).

## Routings: switch the sound on the bar

- **Routings are a library**: `~/Music/tussen/routing/<name>/routing.toml`, plugin states in `plugins/` beside each. One runs (`[layers] routing`, absent: `default`). `list_layers {layer: "routing"}` lists them.
- **Switch by hand, on the grid**: `layer_switch {layer: "routing", name, at: "next_bar"}` (or `"next_beat"`, `"now"`). It answers `preparing: true`; `layers.routing.pending` shows it until it lands. Clickless: gains cross a seam, plugin chains swap on the line, and a plugin both routings share keeps playing. `layer_cancel {layer: "routing"}` calls it off. **Never an undo entry**: switch back to undo it. Nothing switches a routing by itself (no vibe, no jam).
- **A breakdown that changes the sound** is a routing: `layer_new {layer: "routing", name: "breakdown"}` copies the running one as it is live (`from: "empty"` or another routing's name otherwise); switch to it, edit with the usual sound calls (they write the running routing), switch back. Also `layer_save_as`, `layer_rename`, `layer_delete` (not the running one), `layer_load`, `layer_lock`. A routing is edited only while it runs.
- **Actions**: `routing/next`, `routing/prev`, `routing/<name>` switch on the next bar. **One pad, vibe and routing together**: two mapping rows on the control, `vibe/<name>` and `routing/<name>`; both land on the same bar.
- Undo follows the routing an edit was made in. `vibe_try {take, vibe?, routing?}` tries a take through any routing.

## Inputs and outputs: this machine's rig, switched at once

- **The files.** Inputs live in `~/.config/tussen/inputs/<name>.toml`, outputs in `~/.config/tussen/outputs/<name>.toml`; `tussen.toml` is only the machine (ports, sample rate, folders, and `[layers] inputs`/`outputs`, `default` unless set). Edits find their file: `add_input`, `update_input`, `set_input_offset` write the inputs; `add_destination`, `update_destination` (target, timing, offset, channels, mute), `set_signal_delay`, `set_device_delay`, `add_video_output` write the outputs. A destination's filter or mix still writes the routing.
- **The library.** `list_layers {layer: "inputs"|"outputs"}`; `layer_new {layer, name, from?}` (`current` as live, `empty`, or another file's name), `layer_save_as` (then runs), `layer_rename`, `layer_delete` (never the running one), `layer_lock {layer, locked, name?}`, `layer_save`, `layer_revert`. No `layer_open`/`layer_edit`: these files are edited while they run.
- **Look before you switch**: `layer_load {layer: "outputs", name}` changes nothing and lists `changes`, each with `what` in words (*room: Fake Speakers → Fake Headphones*, *td: unchanged*).
- **Switch**: `layer_switch {layer: "outputs", name: "pa"}` (or `inputs`), at once; `at` is refused, since opening a device isn't sample-exact. Only what differs changes: an output with the same name and target keeps running (td is never reconnected), one with new channels, mute or offset changes in place, one on another device opens there first while the old one fades out over a second. Dropped channels fade out, new ones fade in; the sample clock follows the outputs file. The answer lists the changes plus `skipped` (`[midi]`, `[pad]`, `[ndi]` take at the next start). `[layers]` is written, so the hub starts in it.
- **Never an undo entry**: switch back. An edit made while another inputs or outputs file ran can't be undone until you switch back to it.
- **Actions**: `outputs/<name>`, `outputs/next`, `outputs/prev`, and `inputs/…` the same, at once (`list_actions`, kind `layer`).
- `get_state`'s `layers.inputs` and `layers.outputs` say which file runs, whether it is a file yet (`origin`), locked or dirty. A file edited by hand shows in `changed_on_disk`; `reload {inputs?, outputs?}` takes it in.

