# Shaders

Shaders I believe in: ISF GLSL files (`.fs`) that tussen runs on a video output's chain.

**To use one:** copy it into your scene's `shaders/` folder (`~/Music/tussen/scenes/<scene>/shaders/`), then ask your agent to add it to an output, or run `add_shader {output, file: "shaders/<name>.fs"}`.

**Adding one here:** one file per shader, with a `DESCRIPTION` in its header saying what it does and what it reacts to. Every parameter needs a sensible default, and its sound-reactive parts need an amount, never just on or off. How shaders work in tussen is in the [`tussen` skill's performing reference](../skills/tussen/references/performing.md).
