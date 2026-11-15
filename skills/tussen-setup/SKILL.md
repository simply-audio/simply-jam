---
name: tussen-setup
description: Get someone set up to perform with tussen and an agent - download and install tussen, turn on its MCP server, connect the agent (pi or another harness), install simply-jam, and check the hub answers. Use when tussen isn't installed, isn't running, the tussen MCP tools are missing, or someone asks how to get started.
---

# Getting set up

Check each step before the next; stop at the first that fails and help with that one.

## 1. Is tussen installed and running?

- Installed: `/Applications/Simply Tussen.app` exists (or `Simply Tussen Pro.app`).
- If not: they download it from https://simply.audio/tussen and drag it into Applications. **They** open it: never launch it yourself.
- Running: its icon is in the menu bar. If it isn't, ask them to open it.

## 2. Is its MCP server on?

- `curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:9097/mcp` answers (any HTTP code; nothing at all means it's off or tussen isn't running).
- If it's off: tussen › Settings › **Advanced** › **Hub** › **MCP for agents**.

## 3. Is the agent connected?

- **pi**: `pi mcp list` shows `tussen`. If not:
  ```sh
  pi mcp add tussen --url http://127.0.0.1:9097/mcp
  ```
  then `/reload` in pi. tussen's tools are then reachable (in codemode, `searchTools("tussen")`).
- **Another harness**: add an HTTP MCP server named `tussen` at `http://127.0.0.1:9097/mcp` in its MCP settings.

## 4. Is simply-jam installed?

- **pi**: `pi list` shows it. If not: `pi install git:github.com/simply-audio/simply-jam`.
- **Another harness**: clone https://github.com/simply-audio/simply-jam and point its skills directory at `skills/`.

## 5. Say hello

Call `get_state` and tell them, in a line, what tussen has: its inputs, its outputs, the vibe that plays (`layers.vibe`). Remind them that tussen keeps vibes, routings, takes and shaders in `~/Music/simply.audio/tussen/` (controller mappings, inputs and outputs stay in `~/.config/tussen/`). That's the setup done; from here the `tussen` skill takes over.

## When something's wrong

- **Nothing answers on 9097:** tussen isn't running, MCP is off, or another program holds the port (Settings shows the port).
- **The tools appear but calls fail:** the hub is restarting; wait a moment and try `get_state` again.
- **They want help from people:** the simply.audio Discord, https://discord.gg/bAEAFkDQFX.
