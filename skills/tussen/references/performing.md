# Performing live with tussen

For an agent driving a **running** tussen during a performance: writing
shaders, mapping controllers, wiring outputs, while people are on stage.
Nothing here changes tussen's code. You edit files the hub watches and call
its plane. The rules and the ways to reach the hub are in
[the skill](../SKILL.md).

## Ground rules on stage

- **Live means live.** Work on the hub that is running. Don't open a lane,
  don't change code, don't restart the app. If something needs a code
  change, say so and keep the show going with what exists.
- **Never break the picture.** A shader file that doesn't compile keeps the
  last good version running, so a save is safe. Still, check after every
  save (below), and add new work as a new shader or bypassed, not by
  gutting the one on screen.
- **Every plane edit is undoable** (`undo`) and saved with the vibe (the look) or the routing (the sound).
- **Ask before anything that pops up on someone's screen**: a fullscreen
  window output, a permission dialog (camera, screen recording, app audio
  taps). An NDI output is invisible until someone receives it, so it's the
  safe default.

## Talking to the hub

From the repo's default workspace:

```sh
B=target/release/simply-tussen          # or: cargo xtask hub ctl …
$B ctl methods                           # every method, one line each
$B ctl get_state                         # inputs, mixes, outputs, layers
$B ctl <method> '<json>' </dev/null      # always </dev/null: nothing may wait for input
```

- **A channel is named with its hub**: `local·macbook-pro-camera`, not
  `macbook-pro-camera` (the `·` is U+00B7). `get_state` → `inputs[].channels[].id`.
- **A shader's slot is its name** (`"slot": "hearts"`), not an index.
- **A MIDI device is added by its device name** from `list_midi_inputs`,
  with no port or source: `add_input {"name":"launchpad-x-lpx-midi-out","kind":"midi"}`.
- **What the hub has seen** (to find a controller's addresses): press the
  pads, then `filter_preview {"filter":["/tussen/local/midi/"]}` lists every address seen.

## Shaders

The vibe's folder is `~/Music/simply.audio/tussen/vibes/<vibe>/` (`get_state` →
`layers.vibe.current`); shaders live in its `shaders/`. **Saving the file
reloads it.**

Shaders live on **video mixes**: a mix reads named inputs (each a video
channel) through its chain, then an output shows the mix.

```sh
# add_input with a camera feeds a mix by itself (the one an output shows, else a new Video 1)
$B ctl add_input '{"name":"cam","kind":"camera"}'
$B ctl add_video_output '{"name":"stage","ndi":"tussen stage"}'
$B ctl add_shader '{"mix":"Video 1","file":"shaders/x.fs","index":0}'   # index 0: first in the chain
$B ctl list_shaders '{"mix":"Video 1"}'     # running? error + line? what each image reads, what is missing
```

A shader's `image` inputs bind by `bind` (`set_shader_bind`), then by
name (an image `plate` reads the input `plate`, or an earlier shader of
that name), then the first one left reads the picture so far. An input
whose channel is missing reads black and is said in `missing`; nothing
is refused. A mix renders only while an output shows it (NDI, a window,
the cue, its inspector), or the Video tab is open.

Always read `list_shaders` after a save: `running: true` and no `error`.
The engine's `fps` is there too: if it drops below the display's rate, the
shader is too heavy.

### Coordinates: the trap

- **ISF GLSL (`.fs`): `isf_FragNormCoord` has (0,0) at the bottom left, y
  runs up.** WGSL's `in.uv` has (0,0) at the top left, y runs down.
- Shapes from Shadertoy or Inigo Quilez (heart, star SDFs) usually assume
  y up. Don't flip y "to be safe": a heart drawn with the wrong sign comes
  out upside down.
- Keep aspect: multiply x by `RENDERSIZE.x / RENDERSIZE.y` before any
  distance or shape maths.

### What compiles

The GLSL goes through naga, which is stricter than a browser. In
practice:

- No mutable global variables (write a parameter into a function instead),
  no `out` parameters, no `bool` inputs (use a `float` 0..1 and `mix` or
  `step`). One of these made a shader fail with only
  `Function 'main' is invalid`, so stay inside this subset.
- `for` loops with constant bounds, `break` and an early `return` from
  `main` are fine.
- A naga error points at a line, often `main`'s first line. Bisect by
  simplifying rather than trusting the line.

### Sound into a shader

- Declare `{ "NAME": "spectrum", "TYPE": "audioFFT" }` (256 bins, low notes
  first; read `IMG_NORM_PIXEL(spectrum, vec2(x, 0.25)).r`) or `"audio"`
  (a 512-sample waveform, 0.5 is silence). Both are the `main` mix: **a
  source that isn't in `main` doesn't move the shader.**
- Raw bins are small. Average a band and multiply by a `gain` parameter
  (about 5), then clamp.
- The beat: `PHASE` (0..1 within the beat; `exp(-5.0 * PHASE)` is a flash
  on every beat), `BEAT`, `BAR`, `BAR_PHASE`, `TEMPO`, `TIME`.

### Multiple passes and depth

`PASSES` run in order, each into a `TARGET` buffer, read by later passes
by name. `FLOAT: true` buffers hold real numbers, which a depth buffer
needs, and `PERSISTENT: true` keeps a buffer from frame to frame (trails,
feedback). Branch on `PASSINDEX`. A pass can run at a lower resolution
(`"WIDTH": "$WIDTH / 2"`).

A camera gives no depth. Until there is an ML depth input, estimate it
(blurred brightness: bright = near) in a half-size pass, ray-march 3D
shapes in the next pass and depth-test them against it, writing colour and
z (`vec4(rgb, z)`) into a float buffer. Then do depth of field or fog from
that z in the last pass.

## Controllers to effects

The pattern that works: give the shader one `float` parameter (0..1, default
0) per effect, `mix` it in, then drive each parameter from a pad:

```sh
$B ctl set_controller_mapping '{"controller":"launchpad-x","rows":[{"control":"pad/4/3/pressure","action":"video/stage/hearts/fx_glitch","response":"jump"}]}'
# or source: {"osc":"/tussen/local/midi/launchpad-x-lpx-midi-out/1/note/43/pressure"}
```

- **MIDI arrives as OSC**, one normalized float 0..1 per control, under
  the device's name and the channel it played on
  (`/tussen/local/midi/<device>/<ch>/…`): `…/note/<n>` (velocity; 0 on
  release), `…/note/<n>/pressure` (polyphonic aftertouch), `…/cc/<n>`. A
  source is one exact address: find the device and channel with
  `filter_preview`.
- **Pressure gives variable effects** (harder = stronger). Velocity alone
  is fixed for the whole hold. A pad sends pressure only once it is pushed
  past its threshold, so a quick tap does little.
- **A Launchpad X in Note mode** sends notes in a scale layout (36 upward),
  not one per grid square. Find the real numbers with `filter_preview`
  rather than guessing.
- Other sources: `"beat"`, `"bar"`, `"controller"` (the mapping editor),
  `{"level":"main"}` (a mix's loudness; `set_shader_param_level` sets its
  span and smoothing).

## Getting the picture out

- `set_video_output {"output":"stage","ndi":"name"}`: an NDI source.
  It's safe, and invisible until someone receives it.
- `"window": true, "display": 1`: fullscreen on a display, which takes
  over that screen (ask first). Escape in it takes the window off.
- The app's Video tab shows any output's picture (its pills, one per output), sending
  nothing: a private check.
- A swap from a pad cuts an input to another channel on the bar
  (`swap/video/stage/input/plate/td/ndi-2/next/bar`) or fades it
  (`swap/video/stage/fade/plate/4/td/ndi-2/next/bar`: 4 beats).

## Learned on stage

- Show shaders react to the music, performance effects included: pressure sets *how much*, the music (onset-detected kicks, band energies, PHASE/BEAT/BAR) drives *what it does*. Exception: a `freeze` pad (a PERSISTENT buffer that stops updating while held, pressure blending live → frozen). Give each shader a `react` amount, never only on/off.
- Launchpad X driver addresses: `/tussen/local/surface/launchpad-x/pad/<row>/<col>` (row 1 = bottom), `…/pad/<row>/<col>/pressure`, `button/up|down|left|right|session|note|custom|capture`, `button/scene/1..8`. Without the driver: Note mode, `/tussen/local/midi/<device>/<channel>/note/<n>[/pressure]`.
- A depth shader declares `{"NAME":"depth","TYPE":"depth"}` and reads it inside `#ifdef TUSSEN_DEPTH`, blending to a luma fallback by `1 - DEPTH_READY`.
- `gl_FragCoord` runs y-down in tussen (unlike `isf_FragNormCoord`, which runs y-up): for a pixel grid mapped back to uv, use `isf_FragNormCoord * RENDERSIZE`, or the picture flips.
- Pass buffers are 16-bit floats: never store absolute TIME in one (it loses precision after a minute); store `fract(TIME)` and ages.
- With a `depth` input a shader has 5 buffers left (8 extra textures minus images, audio, depth).
- Live pressure pads: map BOTH `pad/<r>/<c>` (velocity on the hit) and `pad/<r>/<c>/pressure` to the same `video/<mix>/<look>/<param>` with response `jump`, and set the param's source to `controller`. The hit sets the amount at once, and pressure moves it while held. A scene param on `{osc = …/pressure}` lights no pad and ignores velocity.
- Swaps: `/now` is live, `/beat` and `/bar` wait for the line.
