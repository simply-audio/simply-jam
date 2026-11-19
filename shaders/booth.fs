/*{
  "DESCRIPTION": "audiodev booth: several snapshots at once. A persistent atlas of ½-size snapshots (2×2, or 3×2 for the crowd) written one at a time on kicks or beats, shown as fading layers, a photo-booth sheet, sliding prints, split panes or a crowd of past figures.",
  "CREDIT": "simply-creative FX library (FX 174, 179, 196, 276, 282, 286), ported to tussen ISF",
  "CATEGORIES": ["time", "history", "depth", "sound", "beat", "modes", "pressure"],
  "INPUTS": [
    { "NAME": "inputImage", "TYPE": "image" },
    { "NAME": "depth", "TYPE": "depth" },
    { "NAME": "wave", "TYPE": "audio" },
    { "NAME": "spectrum", "TYPE": "audioFFT" },
    { "NAME": "amount", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 1.0, "DESCRIPTION": "pressure / djay dry-wet" },
    { "NAME": "react", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5, "DESCRIPTION": "how much the music moves it: 0 holds it still, 0.5 as tuned, 1 twice as much" },
    { "NAME": "mode", "TYPE": "long", "MIN": 0, "MAX": 4, "DEFAULT": 0,
      "VALUES": [0, 1, 2, 3, 4], "LABELS": ["kick timeline", "bar sheet", "beat prints", "kick panes", "kick crowd"] },
    { "NAME": "where", "TYPE": "long", "MIN": 0, "MAX": 2, "DEFAULT": 1, "DESCRIPTION": "what a snapshot keeps: 0 the whole frame · 1 the figure · 2 the room" },
    { "NAME": "kickAmt", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.9 },
    { "NAME": "midsAmt", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.25 },
    { "NAME": "beatSync", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0, "DESCRIPTION": "kicks from the Link beat instead of the sound" },
    { "NAME": "gain", "TYPE": "float", "MIN": 0.25, "MAX": 8.0, "DEFAULT": 1.0 },
    { "NAME": "figure", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5, "DESCRIPTION": "the cut: nearer than this is the figure" },
    { "NAME": "soft", "TYPE": "float", "MIN": 0.01, "MAX": 0.3, "DEFAULT": 0.08 },
    { "NAME": "depthFallback", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0, "DESCRIPTION": "1: depth from blurred brightness instead of the ML map" },
    { "NAME": "depthSmooth", "TYPE": "float", "MIN": 0.0, "MAX": 0.95, "DEFAULT": 0.5 }
  ],
  "PASSES": [
    { "TARGET": "sig", "WIDTH": "2", "HEIGHT": "1", "FLOAT": true, "PERSISTENT": true },
    { "TARGET": "depthBuf", "WIDTH": "$WIDTH / 4", "HEIGHT": "$HEIGHT / 4", "FLOAT": true, "PERSISTENT": true },
    { "TARGET": "state", "WIDTH": "8", "HEIGHT": "1", "FLOAT": true, "PERSISTENT": true },
    { "TARGET": "plate", "WIDTH": "$WIDTH / 2", "HEIGHT": "$HEIGHT / 2", "FLOAT": true, "PERSISTENT": true },
    { "TARGET": "atlas", "FLOAT": true, "PERSISTENT": true },
    {}
  ]
}*/

// mode 0 · kick timeline  FX 174 Kick timeline (where=1): the last 4 kick snapshots as fading layers
// mode 1 · bar sheet      FX 179 Bar tiles (where=1: figures) · FX 282 Room photo-booth (where=2: the room);
//                         where=0: the whole frame as a photo-booth sheet
// mode 2 · beat prints    FX 196 Beat snapshots: beat shots slide out as prints behind the live figure
// mode 3 · kick panes     FX 276 Kick split-screen figure: 1→4 panes on kicks, each a later past
// mode 4 · kick crowd     FX 286 Kick crowd (3×2 atlas): each kick drops a past copy into the room
//
// `atlas` holds the snapshots at ½ size (2×2, reading order: 0 top left … 3
// bottom right; 3×2 = 6 for the crowd): rgb = the camera, a = the figure mask
// at the shot. `state` (8×1) is the trigger memory: newest slot, per-slot grab
// flag / age / bar beat / seed, pane count. `plate` is the room plate (FX 214's
// trick) so a snapshot's or a pane's room can lose its figure.
// 16-bit float buffers: no absolute TIME is stored, only fract(TIME) and ages.

// ── grab these first ────────────────────────────────────────────────
const float LIFE_MIN     = 0.35;  // timeline: s a snapshot lasts at amount 0 (a flicker per kick)
const float LIFE_MAX     = 3.0;   // timeline: s at full amount (all four stack up)
const float MIDS_LIFE    = 0.5;   // timeline: mids stretch the life by up to this fraction
const float OPACITY      = 0.85;  // timeline: fresh snapshot opacity
const float FADE_CURVE   = 1.5;
const vec3  NEW_TINT     = vec3(1.15, 1.0, 0.85);
const vec3  OLD_TINT     = vec3(0.6, 0.8, 1.2);
const float TINT_AMT     = 0.5;
const float RIM          = 0.7;   // timeline: outline on each pose
const float KICK_FLASH   = 0.5;   // timeline: newest pose flashes on its kick
const float TILE_OPACITY = 0.92;  // sheet where=1 (FX 179)
const float ROOM_IN_TILE = 0.25;
const float NEW_POP      = 0.6;
const float GRID_LINE    = 0.5;
const float HUE_PER_BEAT = 0.18;
const vec3  BORDER_COL   = vec3(0.96, 0.95, 0.92);  // sheet where=0/2 (FX 282)
const float GUTTER       = 0.018;
const float DESAT        = 0.45;
const float WARM         = 0.08;
const float FLASH        = 0.6;   // a new panel lands with a flash
const float FLASH_BT     = 0.25;  // beats
const float NOW_MARK     = 0.5;
const float PRINT_OPAC   = 0.75;  // prints (FX 196)
const float LIFE_BEATS   = 3.0;
const float SLIDE        = 0.11;
const float SHRINK       = 0.10;
const float TILT         = 0.05;
const float BORDER       = 0.012;
const float BEAT_FLASH   = 0.35;
const float FLASH_BEATS  = 0.12;
const float SPREAD_MIN   = 6.0;   // panes: frames between pane delays at amount 0 (FX 276)
const float SPREAD_MAX   = 28.0;  // … at full amount
const float REST         = 1.5;   // panes: s without a kick → back to one pane
const float ROOM_TINT    = 0.45;
const float DIVIDER      = 0.004;
const float CUT_FLASH    = 0.35;
const float CUT_FLASH_S  = 0.12;
const float CROWD_SHRINK = 0.9;   // crowd: scale loss per unit of dy (FX 286)
const float BASE_SCALE   = 0.85;
const float FAR_DIM      = 0.5;
const float CROWD_OPAC   = 0.92;
const float FADE_IN      = 0.05;
const float MIDS_COOL    = 0.5;
const vec3  COOL         = vec3(0.7, 0.85, 1.15);
const float SHADOW       = 0.35;
// ──────────────────────────────────────────────────────────────────

// ════ shared: signals (copy verbatim) ══════════════════════════════════
// sig is 2×1 and kept from frame to frame:
//   pixel 0: r kick 0..1 (1 on an onset, decays) · g bass envelope · b fract(TIME) · a s since the onset
// (FLOAT buffers are half floats: never store absolute TIME, only fract(TIME) and ages)
//   pixel 1: r lows 0..1 · g mids 0..1 · b highs 0..1 · a mids slow average
// The kick is Chris's waveform-domain detector: 150 Hz low-pass RMS over
// the 512-sample program, an onset when it clears 1.35× its own envelope.
vec4 sigPass() {
  vec4 p0 = IMG_NORM_PIXEL(sig, vec2(0.25, 0.5));
  vec4 p1 = IMG_NORM_PIXEL(sig, vec2(0.75, 0.5));
  float dt = mix(clamp(fract(fract(TIME) - p0.b + 1.0), 1.0 / 240.0, 0.1), 1.0 / 60.0, 1.0 - step(1.0, FRAMEINDEX));
  float kL = 1.0 - exp(-6.2832 * 150.0 / 24000.0);   // every other sample: 24 kHz
  float kH = 1.0 - exp(-6.2832 * 600.0 / 24000.0);
  float l1 = 0.0; float l2 = 0.0; float h1 = 0.0; float h2 = 0.0;
  float sl = 0.0; float sm = 0.0;
  for (int i = 0; i < 256; i++) {
    float v = IMG_NORM_PIXEL(wave, vec2((float(i) + 0.25) / 256.0, 0.25)).r * 2.0 - 1.0;
    l1 += kL * (v - l1); l2 += kL * (l1 - l2);
    h1 += kH * (v - h1); h2 += kH * (h1 - h2);
    if (i >= 32) { sl += l2 * l2; float m = h2 - l2; sm += m * m; }
  }
  float bass = sqrt(sl / 224.0) * gain;
  float mids = sqrt(sm / 224.0) * gain;
  float hi = 0.0;
  for (int i = 0; i < 16; i++) {
    hi += IMG_NORM_PIXEL(spectrum, vec2(mix(27.0, 128.0, (float(i) + 0.5) / 16.0) / 256.0, 0.25)).r;
  }
  hi = clamp(hi / 16.0 * 40.0 * gain, 0.0, 1.0);

  if (isf_FragNormCoord.x < 0.5) {
    float env = p0.g; float kick = p0.r; float age = mix(60.0, min(p0.a + dt, 60.0), step(1.0, FRAMEINDEX));
    if (bass > 1.35 * env + 0.012 && age > 0.09) {
      kick = 1.0; env = bass; age = 0.0;
    } else {
      kick *= exp(-dt / 0.15);
      env += (1.0 - exp(-dt / (bass > env ? 0.25 : 0.06))) * (bass - env);
    }
    return vec4(kick, env, fract(TIME), age);
  }
  float lows = clamp(bass / 0.08, 0.0, 1.0);
  float avg = p1.a + (1.0 - exp(-dt / 3.0)) * (mids - p1.a);
  float swell = clamp((mids - avg) / max(avg, 0.004), 0.0, 1.0);
  float a = 1.0 - exp(-dt / 0.08);
  return vec4(mix(p1.r, lows, a), mix(p1.g, swell, 1.0 - exp(-dt / 0.15)), max(hi, p1.b * exp(-dt / 0.1)), avg);
}
vec4 sigA() { return IMG_NORM_PIXEL(sig, vec2(0.25, 0.5)); }
vec4 sigB() { return IMG_NORM_PIXEL(sig, vec2(0.75, 0.5)); }
float kickAge() { return sigA().a; }   // seconds since the last kick (60 = none for a minute)
float depthReady() { return DEPTH_READY; }   // tussen's own: 1 while the depth model's map is bound
// react 0.5 is the music as the shader was tuned; 0 holds it still, 1 doubles it.
float reactK() { return 2.0 * react; }
float kickNow() { return reactK() * max(sigA().r, beatSync * exp(-6.0 * PHASE)); }
float lowsNow() { return reactK() * sigB().r; }
float midsNow() { return reactK() * sigB().g; }
float highsNow() { return reactK() * sigB().b; }

// ════ shared: depth (copy verbatim; depth shaders only) ════════════════
// depthBuf.r: 1 = nearest, toward 0 = far. The ML map when tussen has it
// (the ML map, DEPTH_READY), else blurred brightness (bright = near)
// with a little centre weighting (the speaker stands in the middle).
float lumaOf(vec3 c) { return dot(c, vec3(0.2126, 0.7152, 0.0722)); }
float fallbackDepth(vec2 uv) {
  float l = 0.0;
  for (int y = -2; y <= 2; y++) {
    for (int x = -2; x <= 2; x++) {
      l += lumaOf(IMG_NORM_PIXEL(inputImage, uv + vec2(float(x), float(y)) * vec2(0.008, 0.014)).rgb);
    }
  }
  l = smoothstep(0.08, 0.85, l / 25.0);
  float aspect = RENDERSIZE.x / max(RENDERSIZE.y, 1.0);
  float centre = 1.0 - smoothstep(0.1, 0.65, length((uv - vec2(0.5, 0.45)) * vec2(aspect * 0.6, 1.0)));
  return mix(l, centre, 0.3);
}
vec4 depthPass(vec2 uv) {
  float d = fallbackDepth(uv);
  d = mix(IMG_NORM_PIXEL(depth, uv).r, d, max(depthFallback, 1.0 - depthReady()));
  float prev = IMG_NORM_PIXEL(depthBuf, uv).r;
  return vec4(mix(d, prev, depthSmooth * step(1.0, FRAMEINDEX)), 0.0, 0.0, 1.0);
}
float depthAt(vec2 uv) { return IMG_NORM_PIXEL(depthBuf, uv).r; }
float figureAt(vec2 uv) { return smoothstep(figure - soft, figure + soft, depthAt(uv)); }
// 1 where the effect applies (`where`: everywhere, the figure, the room)
float regionAt(vec2 uv) {
  float w = floor(where + 0.5);
  float f = figureAt(uv);
  return w < 0.5 ? 1.0 : (w < 1.5 ? f : 1.0 - f);
}
// ═══════════════════════════════════════════════════════════════════════

// ════ booth: state, plate, atlas ═══════════════════════════════════════
float modeN() { return floor(mode + 0.5); }
float whereN() { return floor(where + 0.5); }
float slotsN() { return modeN() > 3.5 ? 6.0 : 4.0; }
vec2 gridN() { return modeN() > 3.5 ? vec2(3.0, 2.0) : vec2(2.0, 2.0); }
// state (8×1): p0 r newest slot · g fract(TIME) · b previous amount · a shot count (mod 64)
//              p1 r beat (mod 64) · g pane count 1..4 (0 = fresh) · b s since a kick · a s since the panes changed
//              p2..p7 slot j: r grabs this frame · g age s · b bar beat at the shot · a seed
vec4 stAt(float i) { return IMG_NORM_PIXEL(state, vec2((i + 0.5) / 8.0, 0.5)); }
vec4 slotSt(float j) { return stAt(2.0 + j); }
float h11(float x) { return fract(sin(x * 127.1 + 3.7) * 43758.5453); }
float barBeat() { return clamp(floor(BAR_PHASE * 4.0), 0.0, 3.0); }

vec4 statePass(float i) {
  vec4 s0 = stAt(0.0); vec4 s1 = stAt(1.0);
  float first = 1.0 - step(0.5, s1.g);
  float dt = mix(clamp(fract(fract(TIME) - s0.g + 1.0), 1.0 / 240.0, 0.1), 1.0 / 60.0, first);
  float m = modeN();
  float n = slotsN();
  float beatI = mod(floor(BEAT), 64.0);
  float beatOn = step(0.5, abs(beatI - s1.r)) * (1.0 - first);
  float kOn = max((1.0 - step(0.001, kickAge())), step(0.5, beatSync) * beatOn);
  float newest = s0.r;
  float count = s0.a;
  float shoot = 0.0;
  float target = newest;
  if (m < 0.5 || m > 3.5) {                       // a kick shoots the next slot
    shoot = kOn;
    target = mod(newest + 1.0, n);
  } else if (m < 1.5) {                           // each beat shoots the slot of its beat in the bar
    shoot = beatOn;
    target = barBeat();
  } else if (m < 2.5) {                           // each beat shoots the next slot
    shoot = beatOn;
    target = mod(newest + 1.0, n);
  } else {                                        // panes: a ring written every `spread` frames
    float spread = SPREAD_MIN + (SPREAD_MAX - SPREAD_MIN) * amount;
    shoot = step((spread - 0.5) / 60.0, slotSt(newest).g + dt);
    target = mod(newest + 1.0, n);
  }
  if (shoot > 0.5) { newest = target; count = mod(count + 1.0, 64.0); }

  // panes (FX 276): each fresh kick adds one, 4 → 1; REST s without a kick → 1,
  // unless the amount holds at least two
  float panes = max(s1.g, 1.0);
  float sinceKick = min(s1.b + dt, 30.0);
  float sinceChange = min(s1.a + dt, 30.0);
  float prevPanes = panes;
  if (kOn > 0.5) { sinceKick = 0.0; }
  float raw = panes;
  if (kOn > 0.5) raw = mod(panes, 4.0) + 1.0;
  if (sinceKick > REST && amount < 0.02) raw = 1.0;
  if (amount > 0.05) raw = max(raw, 2.0);
  panes = raw;
  if (abs(panes - prevPanes) > 0.5) sinceChange = 0.0;

  if (i < 0.5) return vec4(newest, fract(TIME), amount, count);
  if (i < 1.5) return vec4(beatI, panes, sinceKick, sinceChange);
  float j = i - 2.0;
  vec4 sj = slotSt(j);
  float grab = max(first, shoot * step(abs(j - target), 0.5)) * step(j, n - 0.5);
  float age = first > 0.5 ? 30.0 : (grab > 0.5 ? 0.0 : min(sj.g + dt, 30.0));
  float seed = first > 0.5 ? mod(j * 11.0 + 5.0, 64.0) : count;
  return vec4(grab, age, grab > 0.5 ? barBeat() : sj.b, grab > 0.5 ? seed : sj.a);
}

vec4 platePass(vec2 uv) {
  vec3 cam = IMG_NORM_PIXEL(inputImage, uv).rgb;
  vec3 prev = IMG_NORM_PIXEL(plate, uv).rgb;
  if (stAt(1.0).g < 0.5 || FRAMEINDEX < 1.5) return vec4(cam, 1.0);
  return vec4(mix(prev, cam, 0.02 * (1.0 - figureAt(uv))), 1.0);
}

// atlas: slot j's tile, reading order (0 top left)
vec2 tileUV(float j, vec2 q) {
  vec2 g = gridN();
  vec2 c = vec2(mod(j, g.x), g.y - 1.0 - floor(j / g.x));
  return (c + clamp(q, 0.003, 0.997)) / g;
}
vec4 snap(float j, vec2 q) { return IMG_NORM_PIXEL(atlas, tileUV(j, q)); }

vec4 atlasPass(vec2 uv) {
  vec2 g = gridN();
  vec2 c = floor(uv * g);
  float j = c.x + g.x * (g.y - 1.0 - c.y);
  vec2 q = fract(uv * g);
  if (slotSt(j).r > 0.5) return vec4(IMG_NORM_PIXEL(inputImage, q).rgb, figureAt(q));
  return IMG_NORM_PIXEL(atlas, uv);
}

// how much of a snapshot pixel to keep, by `where`: all · its figure · its room
float keep(float a) {
  float w = whereN();
  return w < 0.5 ? 1.0 : (w < 1.5 ? a : 1.0 - a);
}
// a snapshot's colour with its figure swapped for the room plate when where=2
vec3 snapRoom(vec4 s, vec2 q) {
  return whereN() > 1.5 ? mix(s.rgb, IMG_NORM_PIXEL(plate, q).rgb, s.a) : s.rgb;
}
vec3 hueTint(float i) { return 0.75 + 0.35 * cos(6.2832 * (HUE_PER_BEAT * i + vec3(0.0, 0.33, 0.67))); }

void main() {
  vec2 uv = isf_FragNormCoord;
  if (PASSINDEX == 0.0) { gl_FragColor = sigPass(); return; }
  if (PASSINDEX == 1.0) { gl_FragColor = depthPass(uv); return; }
  if (PASSINDEX == 2.0) { gl_FragColor = statePass(floor((isf_FragNormCoord.x * RENDERSIZE.x))); return; }
  if (PASSINDEX == 3.0) { gl_FragColor = platePass(uv); return; }
  if (PASSINDEX == 4.0) { gl_FragColor = atlasPass(uv); return; }

  vec3 cam = IMG_NORM_PIXEL(inputImage, uv).rgb;
  float fig = figureAt(uv);
  float k = kickNow();
  float mids = midsNow();
  float amt = clamp(amount + kickAmt * k + midsAmt * mids, 0.0, 1.0);
  float aspect = RENDERSIZE.x / max(RENDERSIZE.y, 1.0);
  float m = modeN();
  float bps = max(TEMPO, 1.0) / 60.0;
  vec3 col = cam;

  if (m < 0.5) {
    // ── kick timeline: FX 174 ── oldest first, each cut with its own mask
    float life = mix(LIFE_MIN, LIFE_MAX, amount) * (1.0 + MIDS_LIFE * mids);
    vec2 px = 2.0 / RENDERSIZE;
    vec4 ages = vec4(slotSt(0.0).g, slotSt(1.0).g, slotSt(2.0).g, slotSt(3.0).g);
    vec4 ranks = vec4(0.0);                        // 0 = oldest
    for (int i = 0; i < 4; i++) {
      for (int j = 0; j < 4; j++) {
        ranks[i] += (ages[j] > ages[i] || (ages[j] == ages[i] && j < i)) ? 1.0 : 0.0;
      }
    }
    for (int r = 0; r < 4; r++) {
      float idx = 0.0; float age = 30.0;
      for (int i = 0; i < 4; i++) {
        if (abs(ranks[i] - float(r)) < 0.5) { idx = float(i); age = ages[i]; }
      }
      float t = age / life;
      if (t >= 1.0) continue;
      vec4 s = snap(idx, uv);
      float a = keep(s.a);
      if (a < 0.01) continue;
      float inside = keep(snap(idx, uv + vec2(px.x, 0.0)).a) * keep(snap(idx, uv - vec2(px.x, 0.0)).a)
                   * keep(snap(idx, uv + vec2(0.0, px.y)).a) * keep(snap(idx, uv - vec2(0.0, px.y)).a);
      vec3 tint = mix(NEW_TINT, OLD_TINT, t);
      vec3 c = mix(snapRoom(s, uv), vec3(lumaOf(s.rgb)) * tint, TINT_AMT);
      c += RIM * (1.0 - inside) * tint * step(0.5, whereN());
      c *= 1.0 + KICK_FLASH * k * (1.0 - smoothstep(0.0, 0.2, age));
      col = mix(col, c, OPACITY * (1.0 - pow(t, FADE_CURVE)) * a);
    }
    col = mix(col, cam, fig * step(0.5, whereN()));
  } else if (m < 1.5) {
    float w = whereN();
    vec2 cq = floor(uv * 2.0);
    float idx = cq.x + 2.0 * (1.0 - cq.y);
    float bb = barBeat();
    if (w > 0.5 && w < 1.5) {
      // ── bar sheet, figures: FX 179 ── tiles struck this bar, cleared on the 1
      float shown = step(idx, bb + 0.5);
      float pop = step(abs(idx - bb), 0.5) * pow(1.0 - PHASE, 4.0) * NEW_POP;
      vec4 s = IMG_NORM_PIXEL(atlas, uv);
      vec3 tc = s.rgb * hueTint(idx) * (1.0 + pop);
      col = mix(cam, tc, shown * amt * TILE_OPACITY * mix(ROOM_IN_TILE, 1.0, s.a));
      vec2 g = abs(uv - 0.5);
      col = mix(col, vec3(1.0), (1.0 - smoothstep(0.0, 0.003, min(g.x, g.y))) * GRID_LINE * amt);
      col = mix(col, cam, fig);
    } else {
      // ── bar sheet, photo-booth: FX 282 (where=2 the room, where=0 the whole frame) ──
      vec2 gg = vec2(GUTTER / aspect, GUTTER);
      float cx = step(0.5, uv.x); float cy = step(uv.y, 0.5);
      float i = cx + 2.0 * cy;
      vec2 lo = vec2(cx * 0.5, (1.0 - cy) * 0.5) + gg * vec2(1.0 - 0.5 * cx, 1.0 - 0.5 * (1.0 - cy));
      vec2 hi = lo + 0.5 - 1.5 * gg;
      vec3 sheet = BORDER_COL;
      if (all(greaterThan(uv, lo)) && all(lessThan(uv, hi))) {
        vec2 q = (uv - lo) / (hi - lo);
        vec4 s = snap(i, q);
        vec3 c = snapRoom(s, q);
        c = mix(c, vec3(lumaOf(c)), DESAT) * (1.0 + WARM * vec3(1.0, 0.3, -0.6));
        float ageB = slotSt(i).g * bps;
        c += FLASH * (1.0 - smoothstep(0.0, FLASH_BT, ageB));
        c *= 1.0 + NOW_MARK * 0.3 * step(abs(i - bb), 0.1);
        sheet = c;
      }
      col = mix(cam, sheet, smoothstep(0.0, 0.7, amt));
      col = mix(col, cam, fig * step(1.5, w));
    }
  } else if (m < 2.5) {
    // ── beat prints: FX 196 ── oldest first so the newest lies on top
    float newest = stAt(0.0).r;
    for (int kk = 1; kk <= 4; kk++) {
      float i = mod(newest + float(kk), 4.0);
      vec4 si = slotSt(i);
      float ageB = si.g * bps;
      float a = (ageB > LIFE_BEATS ? 0.0 : pow(1.0 - ageB / LIFE_BEATS, 1.2)) * PRINT_OPAC * amt;
      if (a <= 0.001) continue;
      float ang = si.b * 1.5707963 + 0.7853982;     // bar beat 0..3 → NE, NW, SW, SE
      vec2 dir = vec2(cos(ang), sin(ang));
      float sc = 1.0 - SHRINK * ageB;
      float rot = TILT * ageB * (fract(si.a * 7.13) < 0.5 ? -1.0 : 1.0);
      vec2 p = (uv - 0.5 - dir * SLIDE * ageB) * vec2(aspect, 1.0);
      p = mat2(cos(rot), sin(rot), -sin(rot), cos(rot)) * p / sc;
      vec2 q = p / vec2(aspect, 1.0) + 0.5;
      if (q.x < -BORDER || q.x > 1.0 + BORDER || q.y < -BORDER || q.y > 1.0 + BORDER) continue;
      float inner = step(0.0, q.x) * step(q.x, 1.0) * step(0.0, q.y) * step(q.y, 1.0);
      vec4 s = snap(i, q);
      vec3 c = mix(vec3(0.96, 0.95, 0.9), snapRoom(s, q), inner);
      col = mix(col, c, a * mix(1.0, keep(s.a), inner * step(0.5, whereN()) * step(whereN(), 1.5)));
    }
    col = mix(col, cam, fig);
    col += BEAT_FLASH * amt * (1.0 - smoothstep(0.0, FLASH_BEATS, PHASE));
  } else if (m < 3.5) {
    // ── kick panes: FX 276 ── pane 0 live, pane i about i·spread frames back
    vec4 s1 = stAt(1.0);
    float n = floor(s1.g + 0.5);
    if (n > 1.5) {
      float i = min(floor(uv.x * n), n - 1.0);
      float lx = uv.x * n - i;
      vec2 src = vec2(0.5 + (lx - 0.5) / n, uv.y);
      vec3 room = IMG_NORM_PIXEL(plate, src).rgb;
      vec3 tint = 0.6 + 0.4 * cos(6.2831853 * (i / n + 0.1 + vec3(0.0, 0.33, 0.67)));
      room = mix(room, (0.2 + lumaOf(room)) * tint * 1.2, ROOM_TINT);
      vec3 c = IMG_NORM_PIXEL(inputImage, src).rgb;
      float mk = figureAt(src);
      if (i > 0.5) {
        vec4 s = snap(mod(stAt(0.0).r - (i - 1.0) + 8.0, 4.0), src);
        c = s.rgb; mk = s.a;
      }
      col = mix(room, c, mk);
      float edge = min(lx, 1.0 - lx) / n;
      col = mix(col, vec3(1.0), 1.0 - smoothstep(0.0, DIVIDER, edge));
      col += CUT_FLASH * (1.0 - smoothstep(0.0, CUT_FLASH_S, s1.a));
      col = mix(cam, col, clamp(amt + step(0.5, n - 1.0), 0.0, 1.0));
    }
  } else {
    // ── kick crowd: FX 286 ── six past copies, painted back to front
    float barS = 4.0 / bps;
    vec4 sts[6];
    float dys[6];
    float ranks[6];
    for (int i = 0; i < 6; i++) { sts[i] = slotSt(float(i)); dys[i] = 0.05 + h11(sts[i].a * 1.7 + 0.3) * 0.3; }
    for (int i = 0; i < 6; i++) {
      ranks[i] = 0.0;
      for (int j = 0; j < 6; j++) ranks[i] += (dys[j] > dys[i] || (dys[j] == dys[i] && j < i)) ? 1.0 : 0.0;
    }
    for (int pass = 0; pass < 6; pass++) {
      float idx = 0.0; vec4 si = sts[0];
      for (int i = 0; i < 6; i++) {
        if (abs(ranks[i] - float(pass)) < 0.5) { idx = float(i); si = sts[i]; }
      }
      float seed = si.a;
      float side = h11(seed * 3.1 + 0.7) < 0.5 ? -1.0 : 1.0;
      vec2 off = vec2(side * (0.12 + h11(seed * 5.3 + 1.1) * 0.32), 0.05 + h11(seed * 1.7 + 0.3) * 0.3);
      float age = si.g;
      float life = smoothstep(0.0, FADE_IN, age) * clamp(1.0 - age / max(barS, 0.1), 0.0, 1.0);
      float vis = max(life, amount) * CROWD_OPAC;
      if (vis < 0.003) continue;
      float s = BASE_SCALE * (1.0 - CROWD_SHRINK * off.y * 0.5);
      vec2 anchor = vec2(0.5 + off.x, off.y);
      vec2 src = (uv - anchor) / s + vec2(0.5, 0.0);
      vec2 sh = (uv - anchor) / vec2(0.12 * s, 0.025 * s);
      if (src.x < 0.0 || src.x > 1.0 || src.y < 0.0 || src.y > 1.0) continue;
      vec4 sn = snap(idx, src);
      float mk = keep(sn.a);
      col *= 1.0 - SHADOW * vis * exp(-dot(sh, sh));
      vec3 c = snapRoom(sn, src) * (1.0 - FAR_DIM * off.y / 0.4);
      c = mix(c, c * COOL, MIDS_COOL * mids);
      col = mix(col, c, mk * vis);
    }
    col = mix(col, cam, fig);
  }
  gl_FragColor = vec4(col, 1.0);
}
