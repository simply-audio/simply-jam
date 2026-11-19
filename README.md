# simply-jam

Shaders for [tussen](https://simply.audio/tussen), and the place to make new ones.

tussen is the hub that runs a jam: your music, cameras and controllers come in and are lined up on the beat, and sound and pictures go out. The pictures are shaders. simply-jam holds the ones I perform with, and everything you and your coding agent need to write your own and contribute it.

## Make a shader

1. **Have tussen running** on your Mac ([download it](https://simply.audio/tussen)), so you can try what you write.
2. **Clone this repo and point your coding agent at it** (Claude Code, Codex, pi or another). Ask it for the look you have in mind: *"a shader that shatters the picture on the kick and lets the figure stay clear"*. [`AGENTS.md`](AGENTS.md) and the [`tussen-shaders`](skills/tussen-shaders/SKILL.md) skill tell it how to write one that works in tussen, starting from [`template.fs`](skills/tussen-shaders/template.fs), and how to try it in your tussen without disturbing what's playing.
3. **Open a pull request** with that one shader. [CONTRIBUTING.md](CONTRIBUTING.md) says what makes a good one and what the pull request needs.

Once merged, it ships in the next `simply-jam` shader pack on simply.audio, and every tussen player can install it from Settings › Advanced › Folders, or have their agent do it. They never need GitHub.

## What's in it

| | |
|---|---|
| [`shaders/`](shaders/) | the shaders, one ISF GLSL file each: the `simply-jam` shader pack |
| [`skills/tussen-shaders/`](skills/tussen-shaders/SKILL.md) | writing a shader that works in tussen, and testing it |
| [`AGENTS.md`](AGENTS.md) · [`CONTRIBUTING.md`](CONTRIBUTING.md) | for your agent, and for you |
| [`music/`](music/) | how a set moves, and what the pictures should do with it |
| [`skills/tussen/`](skills/tussen/SKILL.md) | the skill an agent uses to set someone up and perform with tussen; simply.audio publishes it from here |
| [`bin/jam`](bin/jam) | puts a shader from `shaders/` on a Launchpad pad of the running tussen (below) |

## Playing with tussen?

Start at [simply.audio/tussen](https://simply.audio/tussen): download tussen, then tell your agent *"Set me up to jam with tussen: follow https://simply.audio/tussen/agent"*. It gets the `tussen` skill and the shaders from simply.audio; you don't need this repo.

## A shader on a pad, without an agent

```sh
bin/jam shaders              # what you can load
bin/jam pads                 # what each pad does now
bin/jam pad 2/5 paint        # pad row 2 (from the bottom), column 5 now plays paint
bin/jam pad 1/3 print --mode 4 --amount 0.5
```

It copies the shader into the playing vibe and swaps it into that pad's look. It needs curl and jq; `JAM_CONTROLLER` picks another controller (default `launchpad-x`).

## Community

Come and jam with us on the [simply.audio Discord](https://discord.gg/bAEAFkDQFX): share shaders and vibes, ask questions, find people to play with. Everything else is at [simply.audio](https://simply.audio).

## Licence

[MIT](LICENSE).
