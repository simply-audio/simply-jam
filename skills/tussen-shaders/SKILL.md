---
name: tussen-shaders
description: Write or improve a shader (ISF GLSL, `.fs`) that runs in tussen, the live audio/video hub, and test it against the person's running tussen before it goes into simply-jam. Covers the header tussen reads, the GLSL that compiles, the uniforms and inputs (time, beat, bar, audio bands, the kick, level, camera, depth map, previous frame, passes), `amount` and `react`, fallbacks without a camera or depth model, silence, cost, naming and the traps. Use when someone asks for a new look, a shader for tussen, or wants to contribute one to simply-jam; for loading shaders during a show use the `tussen` skill.
---

# Writing shaders for tussen

A tussen shader is one ISF-style GLSL file (`.fs`): a JSON header in its first
comment, then a fragment `main()`. tussen compiles it with naga, runs it on a
video mix's chain on the GPU, reloads it about four times a second when the file
changes, and keeps the last good version running when a save doesn't compile.

Start from [`template.fs`](template.fs): it follows every rule below, compiles,
and costs about 0.3 ms a frame at 1080p. The full reference (header keys, every
uniform, the audio textures, passes, the GLSL that compiles and what doesn't) is
[references/reference.md](references/reference.md).

## The house rules

A simply-jam shader:

1. **Is one file**, `shaders/<name>.fs`. Its name is lowercase letters, digits
   and `-` (`figure-pop.fs`). tussen names the look after the file
   (`melt.fs` → `melt`), and that name goes into every action that drives it
   (`video/<mix>/melt/<param>`), so keep it short. Don't take a name tussen's
   standard shaders have (`ascii bloom contours duotone edges feedback
   figure-pop glitch grade halftone kaleido liquid over-plate shockwave sparks
   spectrum thermal vhs wipe`) or one already in `shaders/`.
2. **Opens with its header**, `/*{ … }*/`, as the very first thing in the file,
   in strict JSON. It has a `DESCRIPTION` (what it looks like and what it reacts
   to: tussen shows it as the look's title), a `CREDIT` (your name, and anyone
   whose work it builds on), and `CATEGORIES` from the tags in
   [`shaders/README.md`](../../shaders/README.md).
3. **Starts its parameters with `amount` and `react`**, both 0..1:
   - `amount` (default 1): how strong the look is. 0 is the picture as it came
     in. On a tussen whose looks have their own amount (its tool list has
     `set_shader_amount`), that one fades the whole look in and out and skips it
     at 0; the shader's `amount` is then its strength, so for a look that is
     more than a colour change (a distortion's depth, a glitch's tear), scale
     the effect with it, not only the mix.
   - `react` (default 0.5): how much the music moves it. Multiply every
     music-driven part by it, so 0 holds the look still and a pad on `react`
     brings the music in and out.
4. **Moves with the music** through the shared music block (in the template,
   word for word from tussen's standard shaders): `bass()`, `mids()`,
   `highs()` (0..1, smoothed), `hit()` (1 on a kick, gone in about half a
   second), `level()`, and `pulse()`, the kick or the beat's own pulse while
   music plays. The sound is the `main` mix: a source not in `main` moves
   nothing.
5. **Sits still in silence.** `pulse()` and the bands are 0 when nothing plays.
   Never flash on `PHASE` alone (`exp(-5.0 * PHASE)`): the beat clock runs in
   silence too, so that flashes on every beat with no music. A slow drift the
   performer sets with a parameter (a `spin`) may keep going.
6. **Works without a camera and without the depth model.** No camera: the
   chain runs over black at 1920×1080; the look should be calm there (black, or
   what it draws itself), never noise. Depth: declare one `depth` input and use
   the template's depth block: `depthAt(uv)` is the model's map while one is
   bound (`DEPTH_READY` 1), else a guess from brightness and the centre of the
   frame, so a figure look still finds a figure. Without the depth parts, delete
   the `depth` input, the `near` pass and the depth block together.
7. **Is cheap.** tussen's own shaders cost 0.4–0.9 ms a frame at 1080p on an
   M4 Max, and several looks stack in one frame. Stay near that: do soft or
   slow things (blurs, noise fields, the depth guess) in a half- or
   quarter-size pass, keep loops short with constant bounds, and read
   `list_shaders`' `time` (below). Say in your pull request what it costs.
8. **Gives every parameter a default that looks good**, and its range in `MIN`
   and `MAX`. A missing `DEFAULT` is the `MIN`: an `amount` without one starts
   at 0, invisible.
9. **Is MIT**, like the repo, and your own work or work whose licence allows
   MIT. Code from Shadertoy is under Shadertoy's non-commercial licence unless
   its author says otherwise: write it yourself.

## The traps

Each comes from tussen's code, and the compile ones were tried through its own compile path.

- **Every parameter is a `float` in the shader**, whatever its `TYPE`: `bool`,
  `long` and `event` arrive as 0/1 or a whole number in a float. `if (on)` and
  `switch (mode)` fail with only `Function 'main' is invalid`; write
  `on > 0.5` and `switch (int(mode))`.
- **Parameters are read-only.** Assigning to one fails the same way: copy it
  into a local first.
- **The header must come first.** A comment line above `/*{` means no header:
  no parameters, and the first use of one is `Unknown variable`. Trailing
  commas in the JSON are an error that names the header's line.
- **Declare every texture in the header**, never with `uniform sampler2D` or a
  `uniform` of your own (both fail). The audio is `"TYPE": "audioFFT"` or
  `"audio"`, the depth map `"TYPE": "depth"`, extra pictures `"TYPE": "image"`.
- **ISF's old names don't exist**: no `texture2D` (use `IMG_NORM_PIXEL`), no
  `vv_FragNormCoord` (use `isf_FragNormCoord`). No recursion.
- **`isf_FragNormCoord` runs up, `gl_FragCoord` runs down.** (0,0) is the
  bottom left in `isf_FragNormCoord`, the top left in `gl_FragCoord`. For pixel
  coordinates use `isf_FragNormCoord * RENDERSIZE`; `IMG_PIXEL` expects them
  y-up, so `IMG_PIXEL(img, gl_FragCoord.xy)` comes out upside down. Shapes from
  Shadertoy or Inigo Quilez assume y up and come out right in
  `isf_FragNormCoord`.
- **Keep the aspect.** Multiply x by `RENDERSIZE.x / RENDERSIZE.y` before any
  distance or shape, and remember `RENDERSIZE` is the size of the pass that
  runs, a buffer's own in a buffer pass.
- **Never store `TIME` in a buffer.** Buffers hold 16-bit floats at best, so
  absolute time loses its precision within minutes: store `fract(TIME)` and
  ages, as the music block does.
- **A kept buffer starts empty**: when the shader loads, when the picture
  changes size, and (on a tussen with look amounts) when the look comes back
  after more than a second at 0. Treat an all-zero buffer as the first frame,
  as the music block's `seen` flag does.
- **Eight extra textures at most**: every pass buffer (once), imported image,
  `audio` or `audioFFT` input, the `depth` input and every image after the
  first count. The template uses three.
- **Don't name a parameter like a built-in** (`TIME`, `PHASE`, `RENDERSIZE`…)
  or a function of the shared blocks (`hue`, `level`, `noise`, `picture`…).
  Names starting `tussen` are refused.
- **An unknown input type is left out**, not refused: `list_shaders` lists it in
  the shader's `ignored`, and using it fails to compile. One `depth` input per
  shader; a second is ignored.
- **A naga error points at a line, often `main`'s first.** When the message is
  only `Function 'main' is invalid`, look for the first three traps above, then
  bisect by commenting out halves.

## Test it in your own tussen

The person runs tussen; you never start, install or download it. Connect as the
[`tussen` skill](../tussen/SKILL.md) says (MCP at `http://127.0.0.1:9097/mcp`,
or `ctl` through the app: `"/Applications/Simply Tussen.app/Contents/MacOS/SimplyTussen" ctl <method> '<json>' </dev/null`).
If tussen isn't open, say what you'd test and ask them to open it.

1. **Compile it without touching anything.** `list_shaders`: pick any shader on
   any mix whose `file` ends in `.fs`. Then
   `check_shader {mix, slot, source}` with the whole text of your file. It
   compiles exactly as a save would and changes nothing: `{ok: true}`, or
   `error: {line, message}` with the line in your source. Fix and check again
   until `ok`. (If no `.fs` shader is on any mix, go to step 2 first.)
2. **See it run: ask first.** It is their show. Say: *"I'd like to add a video
   mix called `shader-test` fed by your camera, with this shader on it. Your
   outputs won't change, and I'll remove it afterwards."* With a yes:
   - `get_state`: the camera's channel id (`local·<camera>`).
   - `add_mix {name: "shader-test", kind: "video", inputs: ["<camera id>"]}`.
   - `add_shader {mix: "shader-test", file: "<absolute path to shaders/<name>.fs in this clone>"}`:
     linked, so every save in the clone reloads it.
   - Ask them to show `shader-test` on the **Video** tab (its pill): a mix
     renders only while something shows it, and they see it privately there.
3. **Read what it does.** `list_shaders {mix: "shader-test"}`: the shader's
   `running: true` and no `error`, nothing in `ignored`, `missing` empty (or
   only what you expect), and its `time.ms`, the GPU's time a frame (judge it
   at the size they run; tussen's own looks are under 1 ms at 1080p). The
   engine's `fps` should stay at the display's rate.
4. **Play it.** With music: does it hit on the kick, follow the bands? Pause the
   music: does it sit still? `set_shader_param` `react` to 0: still? `amount` to
   0: the picture untouched? Without the depth model (`list_shaders`' `depth`
   says if it is ready), or with `DEPTH_READY` replaced by `0.0` in a scratch
   copy: does the guess still find the figure?
5. **Clean up**: `remove_mix {kind: "video", name: "shader-test"}` takes the mix
   and its look away (undoable). Never edit their own looks or files to test.

## Contributing it

Read [CONTRIBUTING.md](../../CONTRIBUTING.md). In short: a pull request adds or
changes **one** file in `shaders/` and touches nothing else (the catalogue row
in `shaders/README.md` is written when it is merged). Put in its description
what it does, what it reacts to, how you tested it (`check_shader` ok, what you
saw with music, in silence, without depth) and its `time.ms`.

## Keep it growing

When tussen surprises you while writing a shader (an error message that misled,
a trick that worked), add it to the traps here or to the reference in the same
session, as one line with its reason.
