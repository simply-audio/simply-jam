# Shaders

Shaders I believe in: ISF GLSL files (`.fs`) that tussen runs on a video output's chain. These are the ones I perform with, gathered from my scenes.

**To use one:** copy it into your scene's `shaders/` folder (`~/Music/tussen/scenes/<scene>/shaders/`), then ask your agent to add it to an output, or call `add_shader {output, file: "shaders/<name>.fs"}`. Most read the music on their own; `padfx` sits last on a chain and turns pad pressure into effects.

**Needs:** *depth* reads tussen's ML depth map (and falls back to brightness without it); *sound* reacts to the music; *two pictures* blends a second input (name it in the output's inputs).

## Figure and depth (needs the ML depth map, falls back to brightness)

| Shader | What it does | Needs | Parameters | Passes |
|---|---|---|---|---|
| [`djaybg`](djaybg.fs) | djaybg: you in front, djay Pro behind. The ML depth map cuts the figure (near) out of the camera and lays it over djay's picture; brightness depth stands in until the model is ready. | depth, sound, two pictures (cam, plate) | 5 | 1 |
| [`figure`](figure.fs) | audiodev figure: the figure as a graphic, cut by depth. Outline, neon tubes, aura, key glow, lightning, hat strobe rim, beat halo, shadow puppet, paper cutout, stencil, x-ray, double exposure, ghost, ink on paper, shadow ink, light composite, ambient wrap. | depth, sound | 12 | 5 |
| [`depthscape`](depthscape.fs) | audiodev depthscape: depth made visible. Keys (luma, hats, dual), the moving cut (pressure plane, clock tide, bass push, bass sink), parallax (wiggle, 3-plane dolly, paper slices), contours, depth rainbow, Joy Division topo lines, depth gradient map, blocky cut. `other` is what shows on the far side of a cut. | depth, sound | 12 | 4 |
| [`hearts3d`](hearts3d.fs) | hearts3d: three passes around a depth buffer. 1: the picture's depth (the ML depth map when tussen has one, else blurred brightness; depthFallback blends to brightness). 2: ray-marched 3D hearts, depth-tested against it, colour + z into a float buffer. 3: depth of field from that z. | depth, sound | 11 | 3 |
| [`lens`](lens.fs) | audiodev lens: blur and focus, depth-aware. Gaussian blur / rack focus / dreamy figure, pressure breath, mids focus pull, tilt-shift, zoom blur, motion blur, hex bokeh, depth fog, smoke room. `where` picks the layer that goes soft (0 all, 1 figure, 2 room). | depth, sound | 11 | 6 |

## Time and memory

| Shader | What it does | Needs | Parameters | Passes |
|---|---|---|---|---|
| [`timeline`](timeline.fs) | audiodev timeline: the camera's own past from an atlas ring (16 quarter-size slots over `reach` seconds), as delays, scrubs, slit-scans, per-pixel time maps, ghosts and echoes of the figure, and the figure and the room on different clocks. | depth, sound | 14 | 4 |
| [`hold`](hold.fs) | audiodev hold: freezes, stutters and sample-and-hold. A persistent held frame grabbed on a trigger (kick, beat grid, bar, pressure edge) shown in place of the live picture, per region (where), per cell or behind a sweep line. | depth, sound | 14 | 6 |
| [`booth`](booth.fs) | audiodev booth: several snapshots at once. A persistent atlas of ½-size snapshots (2×2, or 3×2 for the crowd) written one at a time on kicks or beats, shown as fading layers, a photo-booth sheet, sliding prints, split panes or a crowd of past figures. | depth, sound | 11 | 6 |
| [`tunnel`](tunnel.fs) | Feedback tunnels: the last frame folded back into this one. mode 0 tunnel (FX 50: the room recedes into rings), 1 zoom-rotate spiral (FX 131: classic video feedback, colour rolling per echo), 2 kaleido tunnel (FX 139: an 8-wedge kaleidoscope diving inward). figureOnTop keeps the figure live and sharp over its own tunnel (modes 0, 1; needs depth). | depth, sound | 11 | 4 |
| [`melt`](melt.fs) | Things that flow: one feedback buffer advected. 0 the figure dissolves upward into smoke (FX 80), 1 pixel sort, bright streaks falling (81), 2 the room melts and drips (112), 3 the room flows like liquid on the mids (183), 4 pixel rain on the hats (228), 5 the figure's colours bleed into the room like ink (237). | depth, sound | 13 | 4 |

## Colour, texture and screens

| Shader | What it does | Needs | Parameters | Passes |
|---|---|---|---|---|
| [`colour`](colour.fs) | audiodev colour: the colour and grade family (levels, posterize, duotone, screen print, hue, tint, complement split, solarize, thermal, colour cycle, bleach, teal-orange, sepia, sunset, infrared, sin city, silhouette, gradient fill, bar grade, palette swap, bands to RGB), each a mode, on the whole picture, the figure or the room | depth, sound | 14 | 4 |
| [`print`](print.fs) | audiodev print: the picture as cells and dots (mosaic, halftone, dot swap, glyphs, matrix rain, dither, LED wall, stained glass, pointillism). | depth, sound | 12 | 3 |
| [`screen`](screen.fs) | audiodev screen: screens and film stock — CRT, film print, VHS, night vision, hologram figure, hat static, hat scanline shove, hat grain. One mode per look, `where` puts it on the whole picture, the figure or the room. | depth, sound | 12 | 5 |
| [`glitch`](glitch.fs) | audiodev glitch: RGB split, block datamosh, slice glitch, prism, pixel stretch, kick RGB burst, hat vibrate, edge crackle, kick katana slice, hat micro-glitch. Built to hit on the kick; `where` puts it on the whole picture, the figure (it moves over the room plate) or the room. | depth, sound | 12 | 6 |

## Whole looks

| Shader | What it does | Needs | Parameters | Passes |
|---|---|---|---|---|
| [`hackathon`](hackathon.fs) | The hackathon look (tussen-composite v01–v21): the gel bench (now in magenta, the past in teal, summed as light), cel bands on the figure riding the hats, and on every kick the figure's outline frozen and dissolving into embers that radiate outward. Highs fringe the frame. Also FX 70 (dual gel), 47 (edge dust), 176/289 (hat glitter/shards), 284 (falling dust). | depth, sound | 20 | 4 |
| [`hearts`](hearts.fs) | hearts: floating hearts over the picture that pump with the bass, glow with the highs and flash on the beat; fx_* are performance effects (0 = off, held pad = on) | sound | 20 | 1 |

## Performance pads

| Shader | What it does | Needs | Parameters | Passes |
|---|---|---|---|---|
| [`padfx`](padfx.fs) | Pad FX: 24 performance effects, each an fx_* float 0..1 (pad pressure: harder = stronger), all 0 = the picture untouched. Sits last on every chain, never swapped, so the pads work on whichever look is live. | sound | 25 | 3 |

## Adding one

One file per shader, with a `DESCRIPTION` in its header saying what it does and what it reacts to, and a row in the table above. Every parameter needs a sensible default, and its sound-reactive parts need an amount, never just on or off. How shaders work in tussen is in the [`tussen` skill's performing reference](../skills/tussen/references/performing.md).
