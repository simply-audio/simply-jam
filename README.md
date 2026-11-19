# simply-jam

Performing with [tussen](https://simply.audio/tussen), together with an agent.

tussen is the hub that runs a jam: your music, cameras and controllers come in and are lined up on the beat, and sound and pictures go out. simply-jam is everything an agent needs to play it with you. It holds the skills for driving a running tussen, the shaders I perform with, and how I think about the music.

## What's in it

| | |
|---|---|
| [`AGENTS.md`](AGENTS.md) | what an agent should know before it touches a live show |
| [`skills/`](skills/) | skills for driving tussen: setting it up, shaders, controllers, outputs, vibes |
| [`shaders/`](shaders/) | shaders I believe in, ready to drop into a vibe |
| [`music/`](music/) | musical guidance: how a set moves, and what the pictures should do with it |

## Install

**1. tussen.** Click **Download** on [simply.audio/tussen](https://simply.audio/tussen), drag Simply Tussen into Applications and open it. Press **Start jamming** and play some music: the pictures move with it.

**2. Your agent** (Claude Code, Codex, pi or another). Paste this into it:

> Set me up to jam with tussen: follow https://simply.audio/tussen/agent

It saves the `tussen` skill from simply.audio into its own skills folder, connects to tussen, and keeps the skill up to date when tussen updates. You don't type any commands.

**3. Play.** Ask, for example:

- *"Put a shader on my camera that pulses with the kick."*
- *"Map the Launchpad's bottom row to switch between these three looks on the bar."*
- *"Send the stage output to NDI."*

The shaders in [`shaders/`](shaders/) reach tussen as the `simply-jam` shader pack: your agent installs it, or tussen's Settings › Folders does.

## Put a shader on a pad, without an agent

`bin/jam` puts any shader from [`shaders/`](shaders/) on a Launchpad look pad of the running tussen:

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
