# Contributing a shader

simply-jam's shaders are the looks people play in [tussen](https://simply.audio/tussen). A new one is welcome. Write it with your own coding agent (Claude Code, Codex, pi, …) pointed at this clone: [`AGENTS.md`](AGENTS.md) and the [`tussen-shaders`](skills/tussen-shaders/SKILL.md) skill tell it everything, and [`template.fs`](skills/tussen-shaders/template.fs) is where it starts.

## What makes a good one

- **It moves with the music.** The kick, the bass, the mids and the highs drive what it does, and in silence it sits still.
- **It has `amount` and `react`**, its first two parameters. `amount` is how strong the look is (0: the picture untouched); `react` is how much the music moves it (0: still). A performer puts them on pads.
- **It works for everyone**: without a camera (calm on black) and without tussen's depth model (it falls back to brightness), so it looks right on a laptop on day one.
- **It is cheap**: near tussen's own shaders, under about 1 ms a frame at 1080p, because several looks stack in one frame.
- **Its defaults look good.** Someone adds it and it already plays.

The skill has the full rules and the traps that make a shader fail to compile.

## Test it in your own tussen

With tussen open on your Mac and your agent connected to it (the skill says how):

1. `check_shader` compiles your file exactly as tussen would, changing nothing. Fix until it says `ok`.
2. Let your agent put it on a separate test mix fed by your camera, and watch it on tussen's **Video** tab.
3. Play music, then stop it. Turn `react` to 0, then `amount` to 0. Try it without the depth model.
4. Note its `time.ms` from `list_shaders`.

## The pull request

- **One shader per pull request**: it adds or changes one file in `shaders/`, and nothing else. The catalogue row in `shaders/README.md` is written from your header when it is merged.
- **MIT**, like the rest of the repo. Your own work, or work whose licence allows MIT (Shadertoy's default licence doesn't).
- **A credit line** in the header: `"CREDIT": "Your Name"`, and anyone whose work it builds on.
- **In the description**: what it looks like, what it reacts to, how you tested it, and its `time.ms`. A screenshot or a short clip helps.
- **A check runs on it**: the `shaders` workflow compiles every shader exactly as tussen does (no tussen or GPU needed) and holds yours to the house rules: `DESCRIPTION`, `CREDIT` and `CATEGORIES` in the header, `amount` and `react` first, nothing in `ignored`, `DEPTH_READY` read if it has a `depth` input, no `IMPORTED` images. Each problem shows on your diff at its line. A shader that doesn't compile, a broken rule in yours, or more than one shader changed fails it; the older shaders' broken rules only warn.

## How it reaches players

Once merged, it ships in the next `simply-jam` shader pack on simply.audio. Players install or update the pack from tussen's **Settings › Advanced › Folders › Shader packs**, or their agent does it for them. Players never need GitHub.
