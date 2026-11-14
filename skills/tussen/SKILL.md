---
name: tussen
description: Drive a running tussen hub (Simply Tussen, the live audio/video/OSC/MIDI hub) from an agent. Write and load shaders (ISF GLSL, multi-pass, audio-reactive, ML depth), map controllers such as the Launchpad X to effects, add inputs, route mixes and send video out, live during a performance, through `simply-tussen ctl`. Use whenever someone asks you to "use tussen" to do something, sets up visuals or a show in tussen, or performs with it; not for changing tussen's own code.
---

# Using tussen

tussen is a hub that runs on the performer's Mac. It takes inputs (audio
devices, app taps such as Spotify, OSC, MIDI, cameras, screens, NDI), lines
them up in time, mixes the audio (`main`, `replays`), runs shader chains on
video channels, and sends everything on (speakers, NDI, a fullscreen window,
OSC and MIDI destinations, other hubs). Everything it does is a **plane
call**: the window, MCP and `ctl` all call the same methods, every edit is
undoable, and edits are saved with the current scene.

This skill is the operator's knowledge. If tussen isn't installed, running
or connected, use the `tussen-setup` skill first.

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

## Talking to the hub

**Use the hub's MCP server first** (`http://127.0.0.1:9097/mcp`; Settings can switch it on or off). If your harness has it, every plane call is a typed tool (`mcp__tussen__get_state`, `…__add_shader`, `…__update_input`). In pi they're unlisted tools: find them with `searchTools("tussen")` in codemode. `ctl` is the fallback, for a shell without MCP, and is the same plane:

```sh
B="/Applications/Simply Tussen.app/Contents/MacOS/SimplyTussen"   # the app is its own CLI
$B ctl methods                          # every method with a one-line description
$B ctl get_state                        # inputs, mixes, outputs, scene, history
$B ctl <method> '<json>' </dev/null     # always </dev/null
```

Names you will get wrong otherwise:

- **Channels carry their hub**: `local·macbook-pro-camera`, `local·spotify`
  (`·` is U+00B7). `get_state` → `inputs[].channels[].id`.
- **A shader's slot is its name** (`"slot": "hearts"`), not an index.
- **MIDI devices are added by device name**, with no port:
  `add_input {"name":"launchpad-x-lpx-midi-out","kind":"midi"}`
  (names from `list_midi_inputs`).
- **The scene's folder** is `~/Music/tussen/scenes/<scene>/` (`get_state` →
  `scene.current`); its shaders live in `shaders/`, and paths are relative to
  the scene's folder (`shaders/x.fs`).
- **What the hub has seen** (a controller's real addresses): have someone
  press, then `filter_preview {"filter":["/tussen/local/midi/"]}`.

## Recipes

| goal | calls |
|---|---|
| an output from the camera | `add_video_output {name:"stage", inputs:["cam"], ndi?}` (shaders live on outputs, never on a channel) |
| two pictures combined | `set_video_inputs {output, inputs:["cam", {"name":"plate","channel":"td"}]}`; a shader's `image` inputs named `cam` and `plate` read them by name (`set_shader_bind` to say otherwise) |
| a shader on the output | write `shaders/x.fs`, then `add_shader {output, file, index?}`, then `list_shaders` (its `missing` says what reads black) |
| change a shader | save the file (it reloads), then `list_shaders` for `running` and `error` |
| a slider follows a pad's pressure | `set_shader_param_source {output, slot, param, source:{"osc":"/tussen/local/midi/launchpad-x-lpx-midi-out/1/note/43/pressure"}}` (the exact address: device and channel from `filter_preview`) |
| a slider follows the music | source `"beat"`, `"bar"`, `{"level":"main"}` (+ `set_shader_param_level` for its span and smoothing) |
| switch shaders from a pad | map the pad to `swap/video/<output>/shader/<a>/<b>…/<step>/<when>` (`set_controller_mapping`) |
| cut or fade an input from a pad | `swap/video/<output>/input/<slot>/<channels…>/<step>/<when>`; `swap/video/<output>/fade/<slot>/<beats>/<channels…>/<step>/<when>` |
| picture out | `set_video_output {output, ndi:"name"}`; `window:true, display:N` is fullscreen (ask first) |
| check a picture privately | the Video tab's pills show any output in the app's window, sending nothing |
| a source into a mix | `add_to_mix {mix:"main", input}` / `remove_from_mix` |
| change scenes | `scene_switch {name, at:"next_bar"}` |
| a simply-jam shader on a look pad | `bin/jam pad <row>/<col> <shader> [--mode N]` from the simply-jam folder (row 1 = bottom): copies it into the scene and `set_shader_file`s the pad's look |
| undo | `history` first, then `undo {expect: <the position history gave>}` / `redo {expect}`; an agent can't undo, redo or `goto_history` past an edit to Main's mix (its gains, mutes, which channels it holds, its limiter, a plugin on it): ask the performer to undo it in the window |

## Deeper

- [references/performing.md](references/performing.md): shaders in
  depth (coordinates, the GLSL subset that compiles, sound, the beat,
  multi-pass and depth buffers), controllers and pressure, video outputs.

## Keep this skill growing

Every time something surprised you while operating tussen (a name it
wanted, a call that refused, a shader that wouldn't compile, a trick that
worked), add it here or to a reference **in the same session**, as one
line with its reason. Keep this file short: details go in `references/`.
This file is the "how"; the details go in `references/`.
