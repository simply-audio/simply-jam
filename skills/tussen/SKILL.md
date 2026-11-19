---
name: tussen
description: Drive a running tussen hub (Simply Tussen, the live audio/video/OSC/MIDI hub) from an agent, and set someone up to jam with it. Write and load shaders (ISF GLSL, multi-pass, audio-reactive, ML depth), map controllers such as the Launchpad X to effects, add inputs, route mixes and send video out, live during a performance, through tussen's MCP server or `ctl`. Use whenever someone asks you to "use tussen", to set them up with tussen, sets up visuals or a show in tussen, or performs with it; not for changing tussen's own code.
metadata:
  source: https://simply.audio/tussen/agent
---

**Before your first call to tussen in a session**, read tussen's version:
`defaults read "/Applications/Simply Tussen.app/Contents/Info" CFBundleShortVersionString`
(`Simply Tussen Pro.app` for Pro). If it isn't the `tussen` in `.installed.json`
beside this file, refresh this skill: run the one command for your agent on
https://simply.audio/tussen/agent (it saves every file again and updates
`.installed.json`), then re-read this file. If the version is older than the
`min` there, tell the person to update tussen from https://simply.audio/tussen
before anything else.

# Using tussen

tussen is a hub that runs on the performer's Mac. It takes inputs (audio
devices, app taps such as Spotify, OSC, MIDI, cameras, screens, NDI), lines
them up in time, mixes the audio (`main`, `replays`), runs shader chains on
video channels, and sends everything on (speakers, NDI, a fullscreen window,
OSC and MIDI destinations, other hubs). Everything it does is a **plane
call**: the window, MCP and `ctl` all call the same methods, every edit is
undoable; the sound is saved in the routing and the look in the vibe.

This skill is the operator's knowledge. If tussen isn't open or you aren't
connected yet, start with *Getting someone set up* below.

**The hub's own tool list wins.** `tools/list` (or `ctl methods`) always
matches the running app. A call this skill names that the hub doesn't list
is one this tussen predates: say *"update tussen from simply.audio/tussen for
this"* and carry on with what exists.

## The rules that keep a show alive

1. **Work on the hub that's running.** A request to *use* tussen is a
   live operation. Do it, check it, report it. If tussen can't do it yet,
   say so and keep going with what exists.
2. **Never start a second hub, never tap real apps or open real devices
   from a hub you started, never write to real outputs yourself.** Two
   hubs tapping one Spotify and playing to one speaker made a show's audio
   fizz. Talk to the performer's hub with `ctl`. Don't run another.
3. **Never launch the app.** The performer does.
4. **Ask before anything that appears on someone's screen or asks for a
   permission**: a fullscreen window output, a camera or screen capture, an
   app audio tap. NDI outputs are invisible until received: the safe
   default.
5. **Check every change** (`list_shaders`, `get_state`). A shader that
   fails to compile keeps the last good version running, so a save never
   blacks the screen, but your change didn't happen either.

## Getting someone set up

The person may never have used a terminal. They don't type commands: you do.
Say what you're doing in plain words.

1. **Is tussen open?** `curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:9097/mcp`:
   any code means yes. If not, ask them to open Simply Tussen from Applications
   themselves (no tussen yet: they click **Download** on https://simply.audio/tussen
   and drag it into Applications), then wait, checking every few seconds.
   **Never download, install or open tussen yourself (the person clicks the
   download; an agent's fetch would count as one), never set `TUSSEN_REAL_IO`,
   and never start a hub of your own.**
2. **Until your harness lists tussen's MCP tools** (from the next session), use
   `ctl` (below). Ask the person once to let you run it without asking each time.
3. **Say hello**: `get_state`, one line on what plays and what they see.
4. **Nothing tapped, no picture?** A fresh tussen taps nothing. Before they
   press anything, tell them: *"tussen will ask macOS for two things: to hear the
   music your Mac plays, and to use your camera. Click **Allow** on both. If you
   click Don't Allow, tussen shows you where to switch it on."* Then ask them to
   press **Start jamming** in tussen's Settings › Setup. It taps every app but
   tussen (`music`, muted), plays on the Mac's own output, adds the camera, makes
   the `stage` output and plays the `starter` vibe. Wait, reading `get_state`.
5. **Offer one next step** in plain words: another look, a look that changes on
   the bar, the picture fullscreen on a second screen.
6. **More looks?** `install_shader_pack {pack: "simply-jam"}` (`list_shader_packs`
   says what exists). On a tussen without that tool, copy the one shader you need
   from https://simply.audio/tussen/packs/ into the playing vibe's `shaders/`.

### When something's wrong

- **Nothing answers on 9097:** tussen isn't open, MCP is off (Settings ›
  Advanced › Hub), or another program holds the port.
- **The music input says it heard nothing** while an app plays: system audio was
  refused. tussen shows the person where to switch it on; their Mac asks for
  their password. Point them at it; never try to get round a prompt.
- **The tools appear but calls fail:** the hub is restarting; wait and try
  `get_state` again.
- **Help from people:** the simply.audio Discord, https://discord.gg/bAEAFkDQFX.
- Vibes, routings, takes and shaders live in `~/Music/simply.audio/tussen/`;
  mappings, inputs and outputs in `~/.config/tussen/`.

## Talking to the hub

**Use the hub's MCP server first** (`http://127.0.0.1:9097/mcp`; Settings can switch it on or off). If your harness has it, every plane call is a typed tool (`mcp__tussen__get_state`, `…__add_shader`, `…__update_input`). In pi they're unlisted tools: find them with `searchTools("tussen")` in codemode. `ctl` is the fallback, for a shell without MCP, and is the same plane:

```sh
B="/Applications/Simply Tussen.app/Contents/MacOS/SimplyTussen"   # the app is its own CLI
$B ctl methods                          # every method with a one-line description
$B ctl get_state                        # inputs, mixes, outputs, layers (vibe, routing), history
$B ctl <method> '<json>' </dev/null     # always </dev/null
```

Names you will get wrong otherwise:

- **Channels carry their hub**: `local·macbook-pro-camera`, `local·spotify`
  (`·` is U+00B7). `get_state` → `inputs[].channels[].id`.
- **A shader's slot is its name** (`"slot": "hearts"`), not an index.
- **MIDI devices are added by device name**, with no port:
  `add_input {"name":"launchpad-x-lpx-midi-out","kind":"midi"}`
  (names from `list_midi_inputs`).
- **The vibe's folder** is `~/Music/simply.audio/tussen/vibes/<vibe>/` (`get_state` →
  `layers.vibe.current`); its shaders live in `shaders/`, and paths are relative to
  the vibe's folder (`shaders/x.fs`).
- **The Shaders tab** (⌘7, once an output has a shader) is where the operator edits shader files in the app: Apply (⌘S) writes only once it compiles. Scene files and their shader library are editable; Standard shaders are read-only.
- **What the hub has seen** (a controller's real addresses): have someone
  press, then `filter_preview {"filter":["/tussen/local/midi/"]}`.

## Recipes

| goal | calls |
|---|---|
| an output from the camera | `add_input {name:"cam", kind:"camera"}` feeds a video mix by itself (the one an output shows, else the first, else a new `Video 1`); then `add_video_output {name:"stage", ndi?}` (shaders live on the mix, see [sound and control](references/sound-and-control.md)) |
| two pictures combined | `set_mix_inputs {mix, inputs:["cam", {"name":"plate","channel":"td"}]}`; a shader's `image` inputs named `cam` and `plate` read them by name (`set_shader_bind` to say otherwise) |
| a shader on the picture | write `shaders/x.fs`, then `add_shader {mix, file, index?}`, then `list_shaders` (its `missing` says what reads black) |
| change a shader | read it (`list_shaders` gives each shader's `path`), then `check_shader {mix, slot, source}` with the whole new text: write the file only if `ok` (the hub reloads it within ¼ s); if not, fix `error.line` (a line in the source you sent) and check again. A broken save never stops the picture, but checking first means the operator never sees an error |
| a slider follows a pad's pressure | `set_controller_mapping {controller:"launchpad-x", rows:[{control:"pad/4/3/pressure", action:"video/<mix>/<slot>/<param>", response:"jump"}]}` (or source `{"osc":"/tussen/local/midi/…"}` for the exact address) |
| a slider follows the music | source `"beat"`, `"bar"`, `{"level":"main"}` (+ `set_shader_param_level` for its span and smoothing), or a mapping row: `set_controller_mapping {controller:"local", rows:[{control:"/tussen/local/audio/main/level", action:"video/<mix>/<slot>/<param>", range:[0.2,0.6]}]}` |
| switch shaders from a pad | map the pad to `swap/video/<mix>/shader/<a>/<b>…/<step>/<when>` (`set_controller_mapping`) |
| cut or fade an input from a pad | `swap/video/<mix>/input/<slot>/<channels…>/<step>/<when>`; `swap/video/<mix>/fade/<slot>/<beats>/<channels…>/<step>/<when>` |
| picture out | `set_video_output {output, ndi:"name"}`; `window:true, display:N` is fullscreen (ask first) |
| check a picture privately | the Video tab's pills show any output in the app's window, sending nothing |
| a source into a mix | `add_to_mix {mix:"main", input}` / `remove_from_mix` (audio inputs join `main` at unity by default; a clip's sound too) |
| an edit mix for my headphones | `add_mix {name:"edit", kind:"audio", inputs:["local·djay"]}`, then `set_output_mixes {output:"phones", mixes:[{mix:"main", gain_db:-12}, {mix:"edit"}]}` (your headphones play both; see [sound and control](references/sound-and-control.md)) |
| change vibes | `layer_switch {layer:"vibe", name, at:"next_bar"}`: changes the looks, offers and tempo, never the sound |
| studio rig to the PA | `layer_load {layer:"outputs", name:"pa"}` (what would change, in words), then `layer_switch {layer:"outputs", name:"pa"}`: at once, only what differs moves, never an undo step (inputs the same; see [sound and control](references/sound-and-control.md)) |
| choose which speakers you hear | `update_destination {name, mute}` on the others (see [sound and control](references/sound-and-control.md)) |
| drive tussen over OSC | `/tussen/action/<action>`, any action `list_actions` gives ([sound and control](references/sound-and-control.md)) |
| a simply-jam shader on a look pad | `bin/jam pad <row>/<col> <shader> [--mode N]` from the simply-jam folder (row 1 = bottom): copies it into the vibe and `set_shader_file`s the pad's look |
| undo | `history` first, then `undo {expect: <the position history gave>}` / `redo {expect}`; an agent can't undo, redo or `goto_history` past an edit to Main's mix (its gains, mutes, which channels it holds, its limiter, a plugin on it): ask the performer to undo it in the window |

## Mappings

- **Everything that drives something is a mapping row**, in the running mapping set `~/.config/tussen/mappings/<set>/`. A controller has `<controller>.toml`. **An OSC input has `<input>.toml`** (`model = "osc"`; the shared port's input is `osc`): its `control` is an OSC address pattern (`*` = one segment), matching only messages arriving on that input.
- **Drive a plugin parameter**: a row `action = "plugin/<slot>/<param-id>"`, with `response = "jump"` for a sender that should move it at once (the default is pickup), an optional `range` (part of the parameter) and `curve` (`linear` | `exp` | `log`). From an OSC sender: `set_controller_mapping {controller: "osc", rows: [{control: "/td/slider/1", action: "plugin/bus/1", response: "jump", curve: "exp"}]}`.
- **`response = "reset"`** puts the action back where it is saved on the press: a gain, pan, mute or solo (in `main`, or `mix/<name>/channel/<input>/…` in a mix of your own), an output mute, or a plugin/shader parameter. Pushing a MIDI Fighter Twister knob resets what it drives (the knob goes back to its fixed value, the ring follows); any push your mapping leaves unmapped does this.
- **Send an address on to an output as another**: a row on the OSC input, `action = "osc/<output>/<address>"`, optional `range` (the first number `v` becomes `lo + v·(hi − lo)`). Every argument goes along, timed like the original, whatever the output's filter says. To drop the original, exclude it in the output's filter: `update_destination {name, filter: ["/flux/", "-/flux/beat"]}`.
- **The hub's own signals have a mapping file too**: `~/.config/tussen/mappings/<set>/local.toml`, `model = "local"`, with the same rows (`range`, `curve`, `response`). Its `control` is one of the hub's own addresses: Main's features `/tussen/local/audio/main/level|peak|onset`, the clock `/tussen/local/clock/bpm|beat|phase|running|onbeat`, a vibe's offered knob `/tussen/local/knob/<name>`. A plugin following Main's level: `set_controller_mapping {controller: "local", rows: [{control: "/tussen/local/audio/main/level", action: "plugin/bus/1", response: "jump", range: [0.2, 0.6]}]}`. A shader parameter following a vibe's knob: `{control: "/tussen/local/knob/drop", action: "video/<mix>/<shader>/<param>"}`.
- Rows switch with the set (`layer_switch {layer: "mappings", name}`) and are undoable edits. `get_controller_mapping {controller}` reads them; `list_controllers` lists an OSC input or `local` once it has a file.

## Shaders

- **Rename a shader** with `rename_shader {"mix": "stage", "slot": "look7", "to": "hearts"}`. It keeps its place and settings, and everything that names it follows in one undoable edit: mapping rows `video/stage/look7/<param>`, the lists and picks of `swap/video/stage/shader/…`, and later shaders' bindings that read it. Mid-show, renaming keeps the shader's swap pick and knob's live value.
- **New shaders are named after their file**: `add_shader` without `slot` names `melt.fs` `melt`, then `melt-2`. Give `slot` only when you want another name.
- **Which shader a swap is running** shows in `get_state` and `list_shaders`. On each shader a swap holds, `live_bypass` is `true` (waiting) or `false` (the swap's pick). On its mix, `swaps` lists each shader swap with `running`. Absent `live_bypass` means the shader runs as set (`bypass`). This is playing state: never saved, never undone.
- **Where a controller holds a shader parameter** shows as `live` on the parameter in `get_state` and `list_shaders`, 0..1 of its range. Absent while the parameter runs at its fixed `value`. `set_shader_param` sets the fixed value (where a reset goes back to).

## Audio mixes

- **Mixes of your own**: `add_mix {name:"edit", kind:"audio", inputs:["local·djay"]}` makes an audio mix holding only those channels (empty = silent); `rename_mix {kind:"audio", name, to}`, `remove_mix {kind:"audio", name}` (refused while an output plays it). `add_to_mix`/`remove_from_mix`/`set_mix_gain`/`set_mix_mute`/`set_mix_limiter {mix:"<name>"}` work on it as on `main`. `set_mix_timing {mix, timing:"room"|"live"}`: live = what arrives now (a player's headphones); `main` is always the room's.
- **An output device plays several mixes**: `set_output_mixes {output, mixes:[{mix:"main", gain_db:-12}, {mix:"edit"}]}` (−60..12 dB, each once; an RTP stream plays one). `get_state` shows them in `destinations[].mixes`; `update_destination {mix}` still sets one mix at 0 dB.
- **OSC/controller actions**: `mix/<mix>/channel/<input>/gain|pan|mute` for any mix (live, never saved); `solo` only on `mix/main/…`.
- An agent should not change `main` without the user; a mix of its own is the place to build.

## Inputs and outputs

- **Switch off an input or output** with `set_enabled {input | output, enabled}`. Off, it opens nothing, captures or plays nothing and shows no error; `get_state` shows `disabled: true`, and the inputs or outputs file holds `enabled = false`.
- **An input or output whose device isn't plugged in** shows `unplugged: true`, and its status or error reads `<device> isn't plugged in`. That is not a failure: it comes back by itself.

## Pro: recording and replaying video

- **Record video**: `record_start {name?, video?}` in Pro records each video output's picture with the take, one `video-<output>.mp4` per output (H.264, main's sound in it); `video: false` skips it for one take, `set_record_video {on}` switches it for all (saved as `[record] video`; `get_state` → `record.video`). `record_status` → `video` (each file's `frames`, `dropped`, `size_bytes`) and `size_bytes` (the take so far). `list_takes` → each take's `videos`. The free edition ignores `video` and plays a Pro take without its pictures.
- **Replay**: replaying a take (`replay_start`) shows its recorded pictures on the outputs of the same name instead of the live picture, in time with the sound; `replay_stop` brings the live picture back. `replay_start {take, video: false}` replays over the live picture instead: the outputs show the live engine, driven by the replayed sound, controls and beat (to loop a take while editing a shader). Omitted or `true`: the recorded pictures.

## Deeper

- [references/sound-and-control.md](references/sound-and-control.md): where the sound lives (routing, not vibes), inputs and outputs files and switching them, muting outputs, effects on one input, every action over OSC and its state.
- [references/performing.md](references/performing.md): shaders in
  depth (coordinates, the GLSL subset that compiles, sound, the beat,
  multi-pass and depth buffers), controllers and pressure, video outputs.

## Keep this skill growing

Every time something surprised you while operating tussen (a name it
wanted, a call that refused, a shader that wouldn't compile, a trick that
worked), add it here or to a reference **in the same session**, as one
line with its reason. Keep this file short: details go in `references/`.
This file is the "how"; the details go in `references/`.
