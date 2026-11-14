# simply-jam

Performing with [tussen](https://simply.audio/tussen), together with an agent.

tussen is the hub that runs a jam: your music, cameras and controllers come in and are lined up on the beat, and sound and pictures go out. simply-jam is everything an agent needs to play it with you. It holds the skills for driving a running tussen, the shaders I perform with, and how I think about the music.

## What's in it

| | |
|---|---|
| [`AGENTS.md`](AGENTS.md) | what an agent should know before it touches a live show |
| [`skills/`](skills/) | skills for driving tussen: setting it up, shaders, controllers, outputs, scenes |
| [`shaders/`](shaders/) | shaders I believe in, ready to drop into a scene |
| [`music/`](music/) | musical guidance: how a set moves, and what the pictures should do with it |

## Install

**1. tussen.** Download it from [simply.audio/tussen](https://simply.audio/tussen), open it, and in **Settings › Advanced › Hub** make sure **MCP for agents** is on.

**2. pi**, the agent, from [pi.dev](https://pi.dev):

```sh
curl -fsSL https://pi.dev/install.sh | sh
```

**3. simply-jam, and the connection to tussen:**

```sh
pi install git:github.com/simply-audio/simply-jam
pi mcp add tussen --url http://127.0.0.1:9097/mcp
```

**4. Play.** Run `pi` and ask, for example:

- *"Put a shader on my camera that pulses with the kick."*
- *"Map the Launchpad's bottom row to switch between these three looks on the bar."*
- *"Send the stage output to NDI."*

If anything isn't installed or connected yet, the `tussen-setup` skill walks you and the agent through it.

Other agents (Claude Code, Codex, …) can use the skills too: point them at `skills/` and connect them to `http://127.0.0.1:9097/mcp`.

## Community

Come and jam with us on the [simply.audio Discord](https://discord.gg/bAEAFkDQFX): share shaders and scenes, ask questions, find people to play with. Everything else is at [simply.audio](https://simply.audio).

## Licence

[MIT](LICENSE).
