# Contributing shaders to simply-jam

You are a coding agent in a clone of simply-jam, helping someone write or improve a shader for [tussen](https://simply.audio/tussen), the hub that runs their jam, so they can try it in their own tussen and contribute it here.

## Where things are

- [`shaders/`](shaders/): the shaders, one ISF GLSL file each (`<name>.fs`). They reach players as the `simply-jam` shader pack.
- [`skills/tussen-shaders/`](skills/tussen-shaders/SKILL.md): **read this before writing a line**. The rules a shader here follows, the traps, how to test it, and [`template.fs`](skills/tussen-shaders/template.fs) to start from.
- [`CONTRIBUTING.md`](CONTRIBUTING.md): what makes a good shader here, and the pull request.
- [`music/`](music/): how a set feels and moves. The pictures follow the music.
- [`skills/tussen/`](skills/tussen/SKILL.md): the skill players' agents use to set up and perform with tussen; simply.audio publishes it from here. Its "Talking to the hub" section is how you connect to their tussen.

## Trying it in their tussen

Their tussen is their instrument, and may be in the middle of a show.

1. **Never start, install or download tussen.** They open it. If it isn't running, say what you'd test and ask.
2. **Compile with `check_shader` first**: it changes nothing.
3. **Ask before changing anything of theirs** (a mix, a look, a file in their vibe, anything on a screen). To see a shader run, propose a separate `shader-test` mix, as the skill says, and remove it afterwards.
4. **Check every change** with `list_shaders`: a shader that fails to compile keeps the last good one running, so nothing breaks, but your change didn't happen.

## The pull request

One pull request adds or changes **one** shader in `shaders/`, and touches nothing else. MIT, with a credit line in its header. Details in [CONTRIBUTING.md](CONTRIBUTING.md).

## How it ships

Once merged, the shader is in the next `simply-jam` shader pack on simply.audio. Players install or update it from tussen's Settings › Advanced › Folders, or their agent does (`install_shader_pack {pack: "simply-jam"}`). They never need GitHub.

## Keep it growing

When tussen surprises you while writing a shader (a misleading error, a trick that worked), add it to the `tussen-shaders` skill as one line with its reason, in its own pull request.
