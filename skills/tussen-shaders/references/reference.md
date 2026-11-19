# tussen shader reference

What tussen gives an ISF GLSL shader (`.fs`), as its video engine does it. The
rules and the testing loop are in [the skill](../SKILL.md).

## The header

The file's first comment, `/*{ … }*/`, with only whitespace before it. Strict
JSON; keys in any case; tussen's spellings (`name`, `inputs`, `type`, `range`,
`default`) and ISF's (`DESCRIPTION`, `INPUTS`, `NAME`, `TYPE`, `MIN`, `MAX`,
`DEFAULT`) both read. Keys tussen doesn't know (`CREDIT`, `CATEGORIES`,
`LABELS`, a parameter's `DESCRIPTION`) are kept in the file and ignored. A
file with no header is a shader with no parameters.

| `TYPE` | What it is |
|---|---|
| `image` | a picture. The first one (none declared: `inputImage`) reads the picture so far; each other one binds by `set_shader_bind`, then by name (an image `plate` reads the mix's input `plate`, or an earlier look called `plate`), else it reads black and `list_shaders`' `missing` says so |
| `float` | a parameter, `MIN`..`MAX` (absent: 0..1), `DEFAULT` (absent: the `MIN`) |
| `bool` | a parameter, 0 or 1 |
| `long` (`int`) | a parameter, a whole number `MIN`..`MAX`: give both, or it is 0..1 |
| `event` | a parameter, 1 for the one frame its drive rises past a half |
| `point2D`, `color` | one parameter per component (`center_x`, `center_y`; `tint_r` … `tint_a`), put back together under the input's name as a `vec2` / `vec4` |
| `audioFFT` | the spectrum of the `main` mix: 256 bins, low first, each bin's amplitude (a full-scale sine reads 1), bin `k` at `k × rate / 512` Hz |
| `audio` | the waveform of the `main` mix: 512 samples, `0.5 + s / 2` (0.5 is silence) |
| `depth` | the depth map of the mix's first input (or the one `bind` names), one per shader |

Every parameter, whatever its type, is a `float` uniform under its bare name.
Each drive (a pad, OSC, the beat, a level) gives 0..1, put on the range.

An audio input's `MAX` narrows it to that many columns, each its share's peak:
`MAX: 1` reads the level. Rows: the left channel at y 0.25, the right at 0.75;
`y = 0.5` reads both at once.

## What every pass sees

| GLSL | |
|---|---|
| `isf_FragNormCoord` | where, 0..1, (0,0) bottom left |
| `gl_FragCoord` | where in pixels, (0,0) **top** left |
| `gl_FragColor` | what you write |
| `RENDERSIZE` | the size this pass renders at: its buffer's, else the picture's |
| `TIME` | seconds since the video engine started |
| `BEAT` | the timeline's beat (Link's), fractional, counting up |
| `PHASE` | 0..1 within the beat |
| `BAR`, `BAR_PHASE` | the bar number, and 0..1 within it |
| `TEMPO`, `QUANTUM` | BPM; beats a bar (4 when none is set) |
| `FRAMEINDEX` | pictures this mix has made |
| `PASSINDEX` | which pass runs, from 0 (a float: `PASSINDEX == 1.0`) |
| `DEPTH_READY` | 1 while a real depth map is bound to the `depth` input, else 0 (the input then reads 0) |
| `TUSSEN_DEPTH` | defined when the shader declares a `depth` input (`#ifdef`) |
| `IMG_NORM_PIXEL(t, uv)` | texture `t` at `uv` (y up) |
| `IMG_THIS_PIXEL(t)`, `IMG_THIS_NORM_PIXEL(t)` | `t` here |
| `IMG_PIXEL(t, xy)` | `t` at pixel `xy`, y up |
| `IMG_SIZE(t)` | `t`'s size in pixels |

The beat clock runs whether music plays or not; the audio textures are silence
(zeros, 0.5 for the waveform) when nothing plays.

## The music block

tussen's standard shaders hear the music once a frame in a 2 × 1 pass and keep
it in a persistent float buffer, `sig`. The block (in `template.fs`, word for
word, between `// ── shared: the music` and `// ── end of the shared music
block`) needs an `audioFFT` input named `spectrum` and its pass first:
`{ "TARGET": "sig", "WIDTH": "2", "HEIGHT": "1", "FLOAT": true, "PERSISTENT": true }`.
Pass 0 writes `listen()`; every later pass reads:

| | |
|---|---|
| `bass()`, `mids()`, `highs()` | each band's loudness, 0..1, smoothed (bass bins 0–2, mids 4–40, highs 40–160) |
| `hit()` | 1 when the bass jumps above its own recent average, decaying over about 0.18 s |
| `level()` | the loudest band now |
| `pulse()` | `max(hit(), the beat's pulse)`, the beat's pulse only while `level()` says music plays |
| `luma`, `hash`, `noise`, `hue`, `picture` | helpers |

Keep the block word for word, so a fix to tussen's copy can be pasted over
yours. A parameter can also follow the level without the block:
`set_shader_param_source {…, source: {"level": "main"}}`.

## The depth block

`depth` holds the model's relative inverse depth in `.r`: 1 is the nearest
point of this picture, smaller is farther, no metres. 518 × 392, stretched over
the picture. About a frame behind; the shader never waits for it. With no model
(not installed, loading, not a Mac) it reads 0 and `DEPTH_READY` is 0.

The standard depth block (after the music block, between `// ── shared: depth`
and `// ── end of the shared depth block`) needs a quarter-size float pass
after `sig`: `{ "TARGET": "near", "WIDTH": "$WIDTH / 4", "HEIGHT": "$HEIGHT / 4", "FLOAT": true }`,
written by `nearPass(uv)`. Then `depthAt(uv)` is the map, or the guess
(blurred brightness mixed with nearness to the centre) without one, and
`figureAt(uv, cut, soft)` is 1 inside the figure. The guess reads a lit window
as near; it is the price of a look that still works.

## Passes and the previous frame

`PASSES` run top to bottom every frame, each into its `TARGET` buffer; the last
one, with no target, is the picture. Later passes read a buffer by its name.

- **Size**: `WIDTH`, `HEIGHT` are expressions over `$WIDTH`, `$HEIGHT` (the
  picture's) and `$<param>`: `"$WIDTH / 2"`. Absent: the picture's size.
- **`PERSISTENT: true`** keeps the buffer from frame to frame. Each buffer is
  two pictures: a pass reading its own buffer reads the **last frame's** while
  it draws this one. That is the previous frame: feedback, trails, smoothing.
  A buffer not kept starts every frame cleared.
- **`FLOAT: true`** is 16-bit float; without it, 8 bits a channel, like the
  picture.
- **At most 8** textures past the first image: buffers (each once), `IMPORTED`
  images, audio inputs, the depth input, other images.
- `IMPORTED`: a PNG or JPEG beside the file, a texture by its name. A
  simply-jam shader is one file, so don't use it.

## The GLSL that compiles

naga's GLSL front end, at `#version 450` (a file's own `#version` line is
blanked). Checked through tussen's own compile path:

| Compiles | Fails |
|---|---|
| `const` globals and arrays, mutable globals, structs | `if (aBoolParam)`, `switch (aLongParam)`: parameters are floats |
| `out` and `inout` parameters, overloads | assigning to a parameter |
| `for` with constant or computed bounds, `while`, `break`, an early `return` | recursion |
| `switch` on an `int`, ternaries, `int` ↔ `float` mixing | `texture2D`, `vv_FragNormCoord` |
| `fwidth`, `atan(y, x)`, `tanh`, bit operations on `uint` | `uniform sampler2D x;`, `uniform float x;` |
| `#define`, `#ifdef TUSSEN_DEPTH`, `precision` lines | anything before the header |

An error carries the line in your file; `Function 'main' is invalid` with no
more is usually one of the first two rows on the right.

## Through the hub

| Call | |
|---|---|
| `check_shader {mix, slot, source}` | compiles `source` as that look's file would be (language by its extension) and changes nothing: `{ok, error?: {line?, message}, path}` |
| `list_shaders {mix?}` | each look: `running`, `error`, `params` (each `now`), `ignored`, `time` (`ms` a frame, `gpu` true when the GPU timed it), `reads`; each mix's `missing`; the engine's `fps`; `depth`, the model's state |
| `add_shader {mix, file}` | a look on a mix; an absolute `file` is linked, so saving it reloads it |
| `set_shader_param {mix, slot, param, value}` | a parameter's fixed value |
| `install_depth_model` | fetch and load the depth model (about 50 MB) |
