# Shaders

Shaders I believe in: ISF GLSL files (`.fs`) that tussen runs on a video output's chain. These are the ones I perform with, gathered from my scenes.

**To use one:** copy it into your vibe's `shaders/` folder (`~/Music/simply.audio/tussen/vibes/<vibe>/shaders/`), then ask your agent to add it to a mix, or call `add_shader {mix, file: "shaders/<name>.fs"}`. Most read the music on their own; `padfx` sits last on a chain and turns pad pressure into effects.

**Needs:** *depth* reads tussen's ML depth map (and falls back to brightness without it); *sound* reacts to the music; *two pictures* blends a second input (name it in the output's inputs).

**Tags** (each shader's `CATEGORIES`):

- *what it is*: `figure` (cuts or draws the person), `time` (shows the past), `colour`, `texture` (cells, dots, screens, film), `glitch`, `blur`, `overlay` (draws things over the picture), `whole-look` (a complete look on its own), `performance-pads` (effects for pads, last on a chain).
- *what it uses*: `depth` (the ML depth map, brightness without it), `sound` (the music in `main`), `beat` (the Link beat), `two-pictures` (a second input), `feedback` (its own last frame), `history` (holds older frames).
- *how it's played*: `modes` (a `mode` picks the look), `pressure` (an `amount` or `fx_*` parameter made for pad pressure).

## Figure and depth (needs the ML depth map, falls back to brightness)

| Shader | What it does | Needs | Tags | Parameters | Passes |
|---|---|---|---|---|---|
| [`djaybg`](djaybg.fs) | djaybg: you in front, djay Pro behind. The ML depth map cuts the figure (near) out of the camera and lays it over djay's picture; brightness depth stands in until the model is ready. | depth, sound, two pictures (cam, plate) | figure, two-pictures, depth, sound | 5 | 1 |
| [`figure`](figure.fs) | audiodev figure: the figure as a graphic, cut by depth. Outline, neon tubes, aura, key glow, lightning, hat strobe rim, beat halo, shadow puppet, paper cutout, stencil, x-ray, double exposure, ghost, ink on paper, shadow ink, light composite, ambient wrap. | depth, sound | figure, depth, sound, beat, modes, pressure | 12 | 5 |
| [`depthscape`](depthscape.fs) | audiodev depthscape: depth made visible. Keys (luma, hats, dual), the moving cut (pressure plane, clock tide, bass push, bass sink), parallax (wiggle, 3-plane dolly, paper slices), contours, depth rainbow, Joy Division topo lines, depth gradient map, blocky cut. `other` is what shows on the far side of a cut. | depth, sound | figure, depth, sound, beat, modes, pressure | 12 | 4 |
| [`hearts3d`](hearts3d.fs) | hearts3d: three passes around a depth buffer. 1: the picture's depth (the ML depth map when tussen has one, else blurred brightness; depthFallback blends to brightness). 2: ray-marched 3D hearts, depth-tested against it, colour + z into a float buffer. 3: depth of field from that z. | depth, sound | overlay, depth, sound, beat | 11 | 3 |
| [`lens`](lens.fs) | audiodev lens: blur and focus, depth-aware. Gaussian blur / rack focus / dreamy figure, pressure breath, mids focus pull, tilt-shift, zoom blur, motion blur, hex bokeh, depth fog, smoke room. `where` picks the layer that goes soft (0 all, 1 figure, 2 room). | depth, sound | blur, depth, sound, beat, modes, pressure | 11 | 6 |
| [`light`](light.fs) | light: the figure relit by a lighting rig, shaped by the depth map. Soft key (a soft box that moves to a new side each beat, a cool rim flaring on the kicks), venetian blinds (noir slats bending over the figure, sliding on the beat, opening on the kicks), pool caustics (water light, faster on the mids), leaf dapple (sun through leaves, a gust on each kick). The room sinks into the dark. | depth, sound | figure, colour, depth, sound, beat, modes, pressure | 15 | 3 |

## Time and memory

| Shader | What it does | Needs | Tags | Parameters | Passes |
|---|---|---|---|---|---|
| [`timeline`](timeline.fs) | audiodev timeline: the camera's own past from an atlas ring (16 quarter-size slots over `reach` seconds), as delays, scrubs, slit-scans, per-pixel time maps, ghosts and echoes of the figure, and the figure and the room on different clocks. | depth, sound | time, history, depth, sound, beat, modes, pressure | 14 | 4 |
| [`hold`](hold.fs) | audiodev hold: freezes, stutters and sample-and-hold. A persistent held frame grabbed on a trigger (kick, beat grid, bar, pressure edge) shown in place of the live picture, per region (where), per cell or behind a sweep line. | depth, sound | time, history, depth, sound, beat, modes, pressure | 14 | 6 |
| [`booth`](booth.fs) | audiodev booth: several snapshots at once. A persistent atlas of ½-size snapshots (2×2, or 3×2 for the crowd) written one at a time on kicks or beats, shown as fading layers, a photo-booth sheet, sliding prints, split panes or a crowd of past figures. | depth, sound | time, history, depth, sound, beat, modes, pressure | 11 | 6 |
| [`tunnel`](tunnel.fs) | Feedback tunnels: the last frame folded back into this one. mode 0 tunnel (FX 50: the room recedes into rings), 1 zoom-rotate spiral (FX 131: classic video feedback, colour rolling per echo), 2 kaleido tunnel (FX 139: an 8-wedge kaleidoscope diving inward). figureOnTop keeps the figure live and sharp over its own tunnel (modes 0, 1; needs depth). | depth, sound | time, feedback, depth, sound, beat, modes, pressure | 11 | 4 |
| [`melt`](melt.fs) | Things that flow: one feedback buffer advected. 0 the figure dissolves upward into smoke (FX 80), 1 pixel sort, bright streaks falling (81), 2 the room melts and drips (112), 3 the room flows like liquid on the mids (183), 4 pixel rain on the hats (228), 5 the figure's colours bleed into the room like ink (237). | depth, sound | time, feedback, depth, sound, beat, modes, pressure | 13 | 4 |
| [`exposure`](exposure.fs) | exposure: the shutter held open, in one buffer. Light painting (bright things, the pads, a lit face, leave trails of light, flaring on the kicks), motion glow (only what moves lights up, in a gel that changes each bar), afterimage (a ghost in the opposite colour wherever something just was), second curtain (a long exposure smearing every move, re-exposed sharp on each kick). | depth, sound | time, feedback, depth, sound, beat, modes, pressure | 15 | 5 |

## Colour, texture and screens

| Shader | What it does | Needs | Tags | Parameters | Passes |
|---|---|---|---|---|---|
| [`colour`](colour.fs) | audiodev colour: the colour and grade family (levels, posterize, duotone, screen print, hue, tint, complement split, solarize, thermal, colour cycle, bleach, teal-orange, sepia, sunset, infrared, sin city, silhouette, gradient fill, bar grade, palette swap, bands to RGB), each a mode, on the whole picture, the figure or the room | depth, sound | colour, depth, sound, beat, modes, pressure | 14 | 4 |
| [`print`](print.fs) | audiodev print: the picture as cells and dots (mosaic, halftone, dot swap, glyphs, matrix rain, dither, LED wall, stained glass, pointillism). | depth, sound | texture, depth, sound, beat, modes, pressure | 12 | 3 |
| [`screen`](screen.fs) | audiodev screen: screens and film stock — CRT, film print, VHS, night vision, hologram figure, hat static, hat scanline shove, hat grain. One mode per look, `where` puts it on the whole picture, the figure or the room. | depth, sound | texture, depth, sound, beat, modes, pressure | 12 | 5 |
| [`glitch`](glitch.fs) | audiodev glitch: RGB split, block datamosh, slice glitch, prism, pixel stretch, kick RGB burst, hat vibrate, edge crackle, kick katana slice, hat micro-glitch. Built to hit on the kick; `where` puts it on the whole picture, the figure (it moves over the room plate) or the room. | depth, sound | glitch, feedback, depth, sound, beat, modes, pressure | 12 | 6 |
| [`paint`](paint.fs) | paint: the camera as a wet oil painting. Strokes follow the picture's edges, finer on the figure and looser in the room, with canvas and relief. Kicks swell the brush and the colour, the mids make the paint run, the hats make the wet strokes shine. | depth, sound | texture, depth, sound, beat, pressure | 16 | 4 |
| [`glass`](glass.fs) | glass: a pane between the camera and the room. Reeded glass (fluted glass, each flute a flipped slice, bending harder on the kicks), rain on the window (beads that act as lenses on a misted pane, runners sliding a step each beat), breath on glass (the pane mists over on the mids and each kick wipes a stroke clear). figureClear lets the face show through. | depth, sound | texture, blur, depth, sound, beat, modes, pressure | 14 | 4 |
| [`thread`](thread.fs) | thread: the picture made of thread, finer on the figure, the darkest parts left as bare cloth. Cross-stitch (X stitches in a floss palette on aida), weave (a twill tapestry), knit (a jumper's V stitches). Kicks swell, slip or stretch the thread, the mids sway the rows, the hats run a sheen along it. | depth, sound | texture, depth, sound, beat, modes, pressure | 15 | 3 |
| [`crush`](crush.fs) | crush: the picture broken by the sound. JPEG crush (a real 8×8 DCT squeeze, harder on every kick and recovering between beats; the mids squeeze the colour, the hats flatten blocks, kicks drop in wrong-coloured blocks; the figure can stay cleaner) and wave tear (bands torn sideways by the actual shape of the sound wave, red, green and blue a moment apart). | depth, sound | glitch, depth, sound, beat, modes, pressure | 13 | 6 |

## Whole looks

| Shader | What it does | Needs | Tags | Parameters | Passes |
|---|---|---|---|---|---|
| [`hackathon`](hackathon.fs) | The hackathon look (tussen-composite v01–v21): the gel bench (now in magenta, the past in teal, summed as light), cel bands on the figure riding the hats, and on every kick the figure's outline frozen and dissolving into embers that radiate outward. Highs fringe the frame. Also FX 70 (dual gel), 47 (edge dust), 176/289 (hat glitter/shards), 284 (falling dust). | depth, sound | whole-look, figure, history, depth, sound, beat, pressure | 20 | 4 |
| [`hearts`](hearts.fs) | hearts: floating hearts over the picture that pump with the bass, glow with the highs and flash on the beat; fx_* are performance effects (0 = off, held pad = on) | sound | overlay, sound, beat, pressure | 20 | 1 |
| [`sparkle`](sparkle.fs) | sparkle: the hackathon look's embers on their own. On every kick the figure's outline freezes as a hot line, then breaks up into sparkles that twinkle, drift away from the figure and cool from yellow to magenta as they die (palette offers others). Draws over whatever comes in, so it plays alone (dim 1) or on top of another look; the mids blow the embers out, the hats stamp extra sparkles. | depth, sound | overlay, figure, depth, sound, beat, modes, pressure | 18 | 4 |
| [`beams`](beams.fs) | beams: a dark stage cut by gel-coloured beams falling through haze. The figure stands lit in them and throws its shadow down the shafts; the room sinks into the dark. The beams pan to new places on each beat and trade gels on each bar; kicks flare and widen them, the mids thicken the haze, the hats glint dust in the light. | depth, sound | whole-look, figure, depth, sound, beat, pressure | 18 | 3 |
| [`shadows`](shadows.fs) | shadows: three coloured lamps throw three shadows of the figure on the wall behind; where one lamp is blocked the wall takes the other two's colour, and the figure stands lit white in front. Kicks throw the shadows apart, the mids sway them, the outer two trail the figure's moves, and the lamps trade colours on each bar. | depth, sound | whole-look, figure, depth, sound, beat, pressure | 16 | 4 |

## Performance pads

| Shader | What it does | Needs | Tags | Parameters | Passes |
|---|---|---|---|---|---|
| [`padfx`](padfx.fs) | Pad FX: 40 performance effects, one per pad, each an fx_* float 0..1 (pad pressure: harder = stronger), all 0 = the picture untouched. Warps (zoom, liquid, mirror, tile, ripple, twirl, pixel, slices, patches, drip, tear, glitch, wave: rows swayed by the sound wave), reads (RGB split, blur, figure lift, chromatic bloom, neon edges, relief: embossed, lit from a side that changes each beat, contour: coloured brightness contours climbing with the beat, halftone: printed dots swelling on the kick, gels: now in magenta and a moment ago in teal, summed as light, glitter: yellow-to-magenta sparkles along the outlines), memory (trails, tunnel, datamosh drag, echo of the last beat, slit-scan: a horizontal slit by default, the picture pouring down below it (slit_dir, slit_pos, slit_jump), colour lag, rainbow trails: what moves leaves trails running through the colours, smoke: bright things trail rising smoke), grades (invert, mono, hue, posterize, thermal) and finishers (film light leak, film burn, blackout, scanlines). Sits last on every chain, never swapped, so the pads work on whichever look is live. | sound | performance-pads, feedback, history, sound, beat, pressure | 44 | 5 |

## Adding one

See [CONTRIBUTING.md](../CONTRIBUTING.md): one file per pull request, its header saying what it does and what it reacts to. The row in the table above is added when it is merged. How to write one that works in tussen is the [`tussen-shaders`](../skills/tussen-shaders/SKILL.md) skill.
