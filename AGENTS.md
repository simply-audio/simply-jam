# Contributing shaders to simply-jam

You are a coding agent in a clone of simply-jam, helping someone write or improve a shader for [tussen](https://simply.audio/tussen), the hub that runs their jam, so they can try it in their own tussen and contribute it here. (Performing with tussen is a different job: its agent instructions are at https://simply.audio/tussen/agent.)

## Where things are

- [`shaders/`](shaders/): the shaders, one ISF GLSL file each (`<name>.fs`). They reach players as the `simply-jam` shader pack.
- [`skills/tussen-shaders/`](skills/tussen-shaders/SKILL.md): **read this before writing a line**. The rules a shader here follows, the traps, how to test it, and [`template.fs`](skills/tussen-shaders/template.fs) to start from.
- [`skills/tussen/`](skills/tussen/SKILL.md): how to talk to a running tussen (MCP, `ctl`, names).
- [`CONTRIBUTING.md`](CONTRIBUTING.md): what makes a good shader here, and the pull request.
- [`music/`](music/): how a set feels and moves. The pictures follow the music.

## Trying it in their tussen

Their tussen is their instrument, and may be in the middle of a show.

1. **Never start, install or download tussen.** They open it. If it isn't running, say what you'd test and ask.
2. **Compile with `check_shader` first**: it changes nothing.
3. **Ask before changing anything of theirs** (a mix, a look, a file in their vibe, anything on a screen). To see a shader run, propose a separate `shader-test` mix, as the skill says, and remove it afterwards.
4. **Check every change** with `list_shaders`: a shader that fails to compile keeps the last good one running, so nothing breaks, but your change didn't happen.

## The pull request

One pull request adds or changes **one** shader in `shaders/`, and touches nothing else. MIT, with a credit line in its header. Details in [CONTRIBUTING.md](CONTRIBUTING.md).

## Keep it growing

When tussen surprises you while writing a shader (a misleading error, a trick that worked), add it to the `tussen-shaders` skill as one line with its reason, in its own pull request.
