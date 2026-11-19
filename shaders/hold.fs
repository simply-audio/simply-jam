/*{
  "DESCRIPTION": "audiodev hold: freezes, stutters and sample-and-hold. A persistent held frame grabbed on a trigger (kick, beat grid, bar, pressure edge) shown in place of the live picture, per region (where), per cell or behind a sweep line.",
  "CREDIT": "simply-creative FX library (FX 10, 64, 65, 128, 161, 171, 186, 188, 199, 207, 216, 220, 244, 252, 268), ported to tussen ISF",
  "CATEGORIES": ["time", "history", "depth", "sound", "beat", "modes", "pressure"],
  "INPUTS": [
    { "NAME": "inputImage", "TYPE": "image" },
    { "NAME": "depth", "TYPE": "depth" },
    { "NAME": "wave", "TYPE": "audio" },
    { "NAME": "spectrum", "TYPE": "audioFFT" },
    { "NAME": "amount", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 1.0, "DESCRIPTION": "pressure / djay dry-wet; for loop/active FX > 0.5 holds" },
    { "NAME": "react", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5, "DESCRIPTION": "how much the music moves it: 0 holds it still, 0.5 as tuned, 1 twice as much" },
    { "NAME": "mode", "TYPE": "long", "MIN": 0, "MAX": 9, "DEFAULT": 0,
      "VALUES": [0, 1, 2, 3, 4, 5, 6, 7, 8, 9],
      "LABELS": ["stutter", "tempo split", "freeze", "time-lapse", "clone", "scan", "radar", "hat cells", "loop tiles", "reveal"] },
    { "NAME": "where", "TYPE": "long", "MIN": 0, "MAX": 2, "DEFAULT": 0, "DESCRIPTION": "0 everywhere · 1 the figure (near) · 2 the room (far)" },
    { "NAME": "grid", "TYPE": "float", "MIN": 0.0625, "MAX": 4.0, "DEFAULT": 0.25, "DESCRIPTION": "beat grid in beats (djay loop/beats): stutter re-grab, tempo split ×2/×4, loop tiles" },
    { "NAME": "latch", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0, "DESCRIPTION": "freeze/clone: 1 = amount > 0.5 holds the frame from its rising edge (an on/off FX), 0 = kicks re-grab, amount sets the hold" },
    { "NAME": "ghost", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.3, "DESCRIPTION": "how much of the live figure stays over the held picture" },
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
    { "TARGET": "state", "WIDTH": "4", "HEIGHT": "1", "FLOAT": true, "PERSISTENT": true },
    { "TARGET": "plate", "FLOAT": true, "PERSISTENT": true },
    { "TARGET": "held", "FLOAT": true, "PERSISTENT": true },
    {}
  ]
}*/

// mode 0 · stutter      FX 10 Beat repeat, FX 171 Beat stutter heads (where=0) ·
//                       FX 252 Loop stutter figure, FX 244 Stop-motion figure (where=1)
// mode 1 · tempo split  FX 220 Fg/bg tempo split (figure every 2·grid, room every 4·grid beats)
// mode 2 · freeze       FX 186 Kick freeze room (where=2) · FX 64 Freeze room (where=2 latch=1)
// mode 3 · time-lapse   FX 188 Time-lapse room (where=2)
// mode 4 · clone        FX 161 Freeze + live · FX 65 Leave-behind (latch=1 ghost=1)
// mode 5 · scan         FX 128 Scan sweep (where=1 ghost=0)
// mode 6 · radar        FX 199 Radar sweep
// mode 7 · hat cells    FX 207 Hat freeze cells
// mode 8 · loop tiles   FX 216 Loop-beats grid
// mode 9 · reveal       FX 268 Clock reveal grid (where=2: the room)
//
// Buffers: `state` (3×1) is the trigger memory (a shader has no other): grab
// flags, age since the grab, previous amount, beat-grid slots, an event count.
// `held` is the grabbed frame (rgb + the figure mask at the grab, in a);
// `plate` is the room plate (FX 214's trick): the camera taken in slowly
// wherever it is not the figure, so a figure that moved leaves room behind it.
// 16-bit float buffers: no absolute TIME is stored, only fract(TIME) and ages.

// ── grab these first ────────────────────────────────────────────────
const float KICK_HOLD    = 0.35;  // stutter: a kick alone holds while kick > this (FX 10; 1 = off)
const float MIDS_STRETCH = 0.25;  // stutter: mids lower that threshold → longer freezes
const float MIDS_HALF    = 0.55;  // stutter: mids above this halve the grid (FX 244 half-beat steps; 1 = off)
const float KICK_FLASH   = 0.35;  // held frame brightens on a kick (FX 10)
const float GRAB_FLASH   = 0.35;  // brightness pop at each grab, decays over GRAB_FLASH_S (FX 171/186/252)
const float GRAB_FLASH_S = 0.08;
const float PAST_WARM    = 0.25;  // held frames lean warm/sepia (FX 171; 0 = off)
const float SCAN         = 0.15;  // VHS scanlines on a stuttered figure (FX 252; 0 = off)
const float BEAT_FLASH   = 0.12;  // tempo split: the room flicks brighter as it steps (FX 220)
const float FREEZE_HOLD  = 0.3;   // freeze latch=1: a kick alone freezes while kick > this (FX 64)
const float FLOOR_MIN    = 0.12;  // freeze: hold s at amount 0 (FX 186)
const float FLOOR_RANGE  = 1.9;   // freeze: extra hold s at full amount
const float MIDS_HOLD    = 0.9;   // freeze: mids add up to this many s
const float MELT         = 0.18;  // freeze: s melting back to live
const float FROST        = 0.25;  // freeze: frozen part desaturates / cools (FX 186)
const vec3  FROST_TINT   = vec3(0.85, 0.95, 1.1);
const float KICK_FROST   = 0.45;  // freeze: icy flash on a kick while frozen (FX 64)
const vec3  ICE          = vec3(0.80, 0.92, 1.10);
const float SHAKE        = 0.006; // freeze: uv jolt of the frozen frame on a kick (FX 64)
const float N_MAX        = 24.0;  // time-lapse: frames held at full choppiness (FX 188)
const float LAPSE_KICK   = 0.6;   // time-lapse: kick snaps it into big steps
const float FLICKER      = 0.12;  // time-lapse: exposure jump per new frame
const float WB_SHIFT     = 0.05;  // time-lapse: white-balance wobble per new frame
const float SHUTTER      = 0.25;  // time-lapse: blink as a new frame lands
const float SHUTTER_S    = 0.06;
const float FRAME_EDGE   = 0.5;   // time-lapse: film-gate vignette while choppy
const float CLONE_HOLD   = 0.2;   // clone latch=1: a kick alone leaves a clone while kick > this (FX 65)
const float MIDS_LINGER  = 0.5;   // clone: mids lower that → kick clones linger
const float CLONE_OPAC   = 0.9;
const float DESAT        = 0.5;   // clone: 0 = full colour · 1 = grey
const vec3  TINT         = vec3(0.8, 0.95, 1.15);  // clone: cool cast
const float OUTLINE      = 0.8;   // clone: bright outline (0 = off)
const float OUTLINE_PX   = 3.0;
const float KICK_BOUNCE  = 0.02;  // clone hops up this far on a kick (uv)
const float CLONE_FLASH  = 1.5;   // clone + outline flash on a kick
const float SWEEP_BEATS  = 4.0;   // scan: one left→right pass every this many beats (FX 128)
const float BAR_WIDTH    = 0.006;
const float BAR_GLOW     = 0.05;
const vec3  BAR_COLOR    = vec3(0.55, 1.0, 0.85);
const float BAR_BRIGHT   = 1.2;
const float BAR_KICK     = 0.6;
const float RADAR_LEAD   = 0.75;  // radar: how far ahead of the arm (share of a turn) the past is taken, at full reach
const float FADE_POW     = 1.6;   // radar: how fast the past fades to live around the circle (FX 199)
const vec3  PHOSPHOR     = vec3(0.35, 1.0, 0.45);
const float ARM_GAIN     = 0.9;
const float WAKE_GAIN    = 0.35;
const float WAKE_IDLE    = 0.35;
const float PAST_TINT    = 0.25;
const vec2  CELLS        = vec2(16.0, 9.0);  // hat cells grid (FX 207)
const float CELL_HOLD    = 0.32;  // s a cell stays frozen after the hat
const float MAX_SHARE    = 0.7;
const float HIGHS_ENGAGE = 0.6;   // hat loudness sets how many cells freeze — the star
const float HAT_ON       = 0.62;  // hat onset: highs through ON after dipping under OFF
const float HAT_OFF      = 0.38;
const float CELL_COOL    = 0.25;
const float CELL_RIM     = 0.6;
const float GAP          = 0.04;  // loop tiles: dark seam (FX 216)
const float LIVE_GLOW    = 0.35;
const vec3  LIVE_TINT    = vec3(1.0, 0.8, 0.4);
const float FROZEN_DIM   = 0.85;
const float TILE_FLASH   = 0.4;
const float GRID_N       = 4.0;   // reveal: 4×4 cells over the bar (FX 268)
const float FLIP_GLOW    = 0.5;
const float LINE         = 0.35;
const float CLOSED_DIM   = 0.35;  // reveal: closed cells (the held frame) dim + desaturate so the reveal reads with one camera
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

// ════ hold: state, plate, held ═════════════════════════════════════════
float modeN() { return floor(mode + 0.5); }
float whereN() { return floor(where + 0.5); }
// state (4×1): p0 r grab A this frame · g s since grab A · b previous amount · a fract(TIME)
//              p1 r grid slot A · g grid slot B (mod 64) · b event count (mod 64) · a beat (mod 64)
//              p2 r grab B this frame · g freeze hold s · b hat armed · a smoothed chop / reach
//              p3 r initialised · g this frame was the first
vec4 st0() { return IMG_NORM_PIXEL(state, vec2(0.125, 0.5)); }
vec4 st1() { return IMG_NORM_PIXEL(state, vec2(0.375, 0.5)); }
vec4 st2() { return IMG_NORM_PIXEL(state, vec2(0.625, 0.5)); }
vec4 st3() { return IMG_NORM_PIXEL(state, vec2(0.875, 0.5)); }
float h11(float x) { return fract(sin(x * 127.1 + 3.7) * 43758.5453); }
float cellHash(vec2 p) { return fract(sin(dot(p, vec2(41.3, 289.1))) * 15731.743); }

// the grid slots: stutter follows `grid` (halved on a mids swell), tempo split
// steps the figure every 2·grid and the room every 4·grid, reveal counts bars
float gridA() {
  float m = modeN();
  if (m < 0.5) return grid * (midsNow() > MIDS_HALF ? 0.5 : 1.0);
  return 2.0 * grid;
}
float stutterThr() { return max(KICK_HOLD - MIDS_STRETCH * midsNow(), 0.05); }

vec4 statePass(float i) {
  vec4 s0 = st0(); vec4 s1 = st1(); vec4 s2 = st2(); vec4 s3 = st3();
  float first = 1.0 - step(0.5, s3.r);
  float dt = mix(clamp(fract(fract(TIME) - s0.a + 1.0), 1.0 / 240.0, 0.1), 1.0 / 60.0, first);
  float m = modeN();
  float k = kickNow();
  float mids = midsNow();
  float beatI = mod(floor(BEAT), 64.0);
  float beatOn = step(0.5, abs(beatI - s1.a)) * (1.0 - first);
  float kOn = max((1.0 - step(0.001, kickAge())), step(0.5, beatSync) * beatOn);
  float pressed = step(0.5, amount);
  float latchRise = pressed * (1.0 - step(0.5, s0.b));
  float pressRise = step(0.1, amount) * (1.0 - step(0.1, s0.b));
  float latched = step(0.5, latch);
  float slotA = mod(floor(BEAT / max(gridA(), 0.01)), 64.0);
  float slotB = m > 8.5 ? mod(floor(BAR), 64.0) : mod(floor(BEAT / max(4.0 * grid, 0.01)), 64.0);
  float chA = step(0.5, abs(slotA - s1.r)) * (1.0 - first);
  float chB = step(0.5, abs(slotB - s1.g)) * (1.0 - first);
  float age = min(s0.g + dt, 30.0);
  float grabA = 0.0; float grabB = 0.0;
  float count = s1.b; float hold = s2.g; float armed = s2.b; float smo = s2.a;

  if (m < 0.5) {                       // stutter
    float held = max(pressed, step(stutterThr(), k));
    grabA = max(1.0 - held, max(chA, kOn));
  } else if (m < 1.5) {                // tempo split
    grabA = chA; grabB = chB;
  } else if (m < 2.5) {                // freeze
    if (latched > 0.5) {
      grabA = 1.0 - max(pressed, step(FREEZE_HOLD, k));
    } else {
      grabA = max(kOn, pressRise);
      if (grabA > 0.5) hold = FLOOR_MIN + FLOOR_RANGE * amount + MIDS_HOLD * mids;
    }
  } else if (m < 3.5) {                // time-lapse
    float chop = clamp(amount + mids + LAPSE_KICK * k, 0.0, 1.0);
    smo += 0.2 * (chop - smo);
    float n = 1.0 + (N_MAX - 1.0) * smo;
    grabA = max(step(smo, 0.01), step((n - 0.5) / 60.0, age));
    count = mod(count + grabA, 64.0);
  } else if (m < 4.5) {                // clone
    if (latched > 0.5) grabA = max(latchRise, (1.0 - pressed) * kOn);
    else grabA = max(kOn, step(clamp(amount + kickAmt * k + midsAmt * mids, 0.0, 1.0), 0.02));
  } else if (m < 6.5) {                // scan, radar: per pixel; radar glides its reach
    float target = clamp(amount + kickAmt * k + midsAmt * mids, 0.0, 1.0);
    smo += (1.0 - exp(-dt / (target > smo ? 0.05 : 0.45))) * (target - smo);
  } else if (m < 7.5) {                // hat cells: an event = a hat onset or a kick
    float hi = highsNow();
    float hit = kOn;
    if (armed > 0.5 && hi > HAT_ON) { hit = 1.0; armed = 0.0; }
    if (hi < HAT_OFF) armed = 1.0;
    grabA = hit * step(0.06, age);
    count = mod(count + grabA, 64.0);
  } else if (m < 8.5) {                // loop tiles
    grabA = max(kOn, max(pressRise, latchRise));
  } else {                             // reveal: on the 1
    grabA = chB;
  }
  grabA = max(grabA, first); grabB = max(grabB, first);
  age = mix(age, 0.0, grabA);

  if (i < 0.5) return vec4(grabA, age, amount, fract(TIME));
  if (i < 1.5) return vec4(slotA, slotB, count, beatI);
  if (i < 2.5) return vec4(grabB, hold, armed, smo);
  return vec4(1.0, first, 0.0, 1.0);
}

vec4 platePass(vec2 uv) {
  vec3 cam = IMG_NORM_PIXEL(inputImage, uv).rgb;
  float f = figureAt(uv);
  vec3 prev = IMG_NORM_PIXEL(plate, uv).rgb;
  if (st3().g > 0.5) return vec4(cam, 1.0);
  if (modeN() > 0.5 && modeN() < 1.5) {            // tempo split: the room steps on its grid
    return vec4(st2().r > 0.5 ? mix(prev, cam, 1.0 - f) : prev, 1.0);
  }
  return vec4(mix(prev, cam, 0.02 * (1.0 - f)), 1.0);
}

float scanSweep() { return fract(BEAT / SWEEP_BEATS); }
// radar: 0 = the arm just passed this pixel, → 1 = about to
float radarBehind(vec2 uv) {
  vec2 p = (uv - 0.5) * vec2(RENDERSIZE.x / max(RENDERSIZE.y, 1.0), 1.0);
  float ang = fract(atan(p.x, p.y) / 6.2831853);   // 0 at 12 o'clock, clockwise
  return fract(BAR_PHASE - ang);
}
float cellFrozen(vec2 uv) {
  float share = MAX_SHARE * clamp(amount + HIGHS_ENGAGE * highsNow() + 0.5 * kickNow() + 0.15 * midsNow(), 0.0, 1.0);
  float n = st1().b;
  float h = cellHash(floor(uv * CELLS) + vec2(n * 7.31, n * 3.17));
  return step(h, share) * step(st0().g, CELL_HOLD) * step(0.001, share);
}

vec4 heldPass(vec2 uv) {
  vec4 live = vec4(IMG_NORM_PIXEL(inputImage, uv).rgb, figureAt(uv));
  vec4 prev = IMG_NORM_PIXEL(held, uv);
  if (st3().g > 0.5) return live;
  float m = modeN();
  if (m > 4.5 && m < 5.5) return uv.x >= scanSweep() ? live : prev;
  if (m > 5.5 && m < 6.5) {
    float lead = max(0.05, RADAR_LEAD * st2().a);
    float b = radarBehind(uv);
    return (b >= 1.0 - lead - 0.04 && b < 1.0 - lead) ? live : prev;
  }
  if (m > 6.5 && m < 7.5) return cellFrozen(uv) > 0.5 ? prev : live;
  return st0().r > 0.5 ? live : prev;
}

// the held look `hc` (its figure mask `ha`) over the live picture at strength
// `lvl`, by `where`: everything · the figure (a held figure over the room plate,
// the live one fading to `ghost`) · the room (held room, live figure in front)
vec3 compose(vec3 cam, vec3 pl, float fig, vec3 hc, float ha, float lvl) {
  float w = whereN();
  if (w < 0.5) {
    vec3 c = mix(cam, hc, lvl);
    return mix(c, cam, fig * ghost * lvl);
  }
  if (w < 1.5) {
    vec3 c = mix(mix(cam, pl, fig), hc, ha * lvl);
    return mix(c, cam, fig * (1.0 - lvl * (1.0 - ghost)));
  }
  vec3 heldRoom = mix(hc, pl, ha);
  return mix(cam, heldRoom, lvl * (1.0 - fig));
}

void main() {
  vec2 uv = isf_FragNormCoord;
  if (PASSINDEX == 0.0) { gl_FragColor = sigPass(); return; }
  if (PASSINDEX == 1.0) { gl_FragColor = depthPass(uv); return; }
  if (PASSINDEX == 2.0) { gl_FragColor = statePass(floor((isf_FragNormCoord.x * RENDERSIZE.x))); return; }
  if (PASSINDEX == 3.0) { gl_FragColor = platePass(uv); return; }
  if (PASSINDEX == 4.0) { gl_FragColor = heldPass(uv); return; }

  vec3 cam = IMG_NORM_PIXEL(inputImage, uv).rgb;
  float fig = figureAt(uv);
  vec3 pl = IMG_NORM_PIXEL(plate, uv).rgb;
  vec4 h = IMG_NORM_PIXEL(held, uv);
  float k = kickNow();
  float mids = midsNow();
  float age = st0().g;
  float pressed = step(0.5, amount);
  float amt = clamp(amount + kickAmt * k + midsAmt * mids, 0.0, 1.0);
  float m = modeN();
  vec3 col = cam;

  if (m < 0.5) {
    // ── stutter: FX 10 / 171 (whole), 252 / 244 (figure) ──
    float hold = max(pressed, step(stutterThr(), k));
    float lvl = max(hold, clamp(amount * 1.2 + midsAmt * mids, 0.0, 1.0));
    vec3 look = mix(h.rgb, lumaOf(h.rgb) * vec3(1.15, 0.95, 0.7), PAST_WARM * hold);
    look *= 1.0 + hold * (KICK_FLASH * k + GRAB_FLASH * exp(-age / GRAB_FLASH_S));
    if (whereN() > 0.5 && whereN() < 1.5) look *= 1.0 - SCAN * hold * step(0.5, fract((isf_FragNormCoord.y * RENDERSIZE.y) / 3.0));
    col = compose(cam, pl, fig, look, h.a, lvl);
  } else if (m < 1.5) {
    // ── tempo split: FX 220 ──
    float stepS = fract(BEAT / (4.0 * grid)) * 4.0 * grid * 60.0 / max(TEMPO, 1.0);
    vec3 room = pl * (1.0 + BEAT_FLASH * exp(-stepS / 0.1));
    vec3 f = mix(h.rgb, cam, ghost * fig);
    col = mix(cam, mix(room, f, h.a), amt);
  } else if (m < 2.5) {
    // ── freeze: FX 186 (kick grabs, amount = hold floor), FX 64 (latch) ──
    float w;
    if (latch > 0.5) w = max(pressed, step(FREEZE_HOLD, k));
    else { float hold = st2().g; w = age < hold ? 1.0 : max(0.0, 1.0 - (age - hold) / MELT); }
    vec2 j = SHAKE * k * w * vec2(sin(TIME * 90.0), cos(TIME * 77.0));
    vec4 hs = IMG_NORM_PIXEL(held, clamp(uv + j, 0.0, 1.0));
    vec3 look = mix(hs.rgb, lumaOf(hs.rgb) * FROST_TINT, FROST * w);
    look = mix(look, ICE * 0.95, KICK_FROST * k * w);
    look += GRAB_FLASH * exp(-age / GRAB_FLASH_S) * w;
    col = compose(cam, pl, fig, look, hs.a, w);
  } else if (m < 3.5) {
    // ── time-lapse: FX 188 ──
    float chop = st2().a;
    float n = st1().b;
    vec3 look = h.rgb * (1.0 + FLICKER * chop * (h11(n) - 0.5) * 2.0);
    look *= 1.0 + WB_SHIFT * chop * (vec3(h11(n + 1.3), h11(n + 2.9), h11(n + 4.1)) - 0.5) * 2.0;
    look *= 1.0 + SHUTTER * chop * (1.0 - smoothstep(0.0, SHUTTER_S, age));
    vec2 c = uv - 0.5;
    look *= 1.0 - FRAME_EDGE * chop * smoothstep(0.25, 0.75, dot(c, c) * 2.2);
    col = compose(cam, pl, fig, look, h.a, step(0.01, chop));
  } else if (m < 4.5) {
    // ── clone: FX 161 (kicks re-freeze, amount crossfades), FX 65 (latch: leave-behind) ──
    float vis = amt;
    if (latch > 0.5) vis = max(pressed, smoothstep(CLONE_HOLD * (1.0 - MIDS_LINGER * mids), 1.0, k));
    vec2 suv = uv - vec2(0.0, KICK_BOUNCE * k);
    vec4 hs = IMG_NORM_PIXEL(held, suv);
    vec3 clone = mix(hs.rgb, vec3(lumaOf(hs.rgb)), DESAT) * TINT;
    vec2 tx = OUTLINE_PX / RENDERSIZE;
    float empty = 0.0;
    for (int i = 0; i < 8; i++) {
      float a = float(i) * 0.7854;
      empty = max(empty, 1.0 - IMG_NORM_PIXEL(held, suv + tx * vec2(cos(a), sin(a))).a);
    }
    float flash = CLONE_FLASH * k;
    clone *= 1.0 + 0.3 * flash;
    clone += OUTLINE * (1.0 + flash) * hs.a * empty * TINT;
    col = mix(mix(cam, pl, fig), clone, hs.a * vis * CLONE_OPAC);
    col = mix(col, cam, fig * (1.0 - vis * (1.0 - ghost)));
  } else if (m < 5.5) {
    // ── scan: FX 128 ──
    float sweep = scanSweep();
    col = compose(cam, pl, fig, h.rgb, h.a, amt * step(uv.x, sweep));
    float d = sweep - uv.x;
    float core = 1.0 - smoothstep(0.0, BAR_WIDTH, abs(d));
    float trail = d > 0.0 ? exp(-d / BAR_GLOW) * 0.35 : 0.0;
    col += BAR_COLOR * BAR_BRIGHT * (core + trail) * amt * (1.0 + BAR_KICK * k);
  } else if (m < 6.5) {
    // ── radar: FX 199 ──
    float reach = st2().a;
    float vis = smoothstep(0.0, 0.05, reach);
    float behind = radarBehind(uv);
    float wp = reach * pow(1.0 - behind, FADE_POW);
    vec3 past = mix(h.rgb, h.rgb * PHOSPHOR * 1.2, PAST_TINT * wp);
    col = compose(cam, pl, fig, past, h.a, wp);
    vec2 p = (uv - 0.5) * vec2(RENDERSIZE.x / max(RENDERSIZE.y, 1.0), 1.0);
    float armDist = min(behind, 1.0 - behind) * 6.2831853 * max(length(p), 0.05);
    float arm = 1.0 - smoothstep(0.0, 0.004, armDist);
    float wake = exp(-behind * 14.0) * (WAKE_IDLE + (1.0 - WAKE_IDLE) * mids);
    col += PHOSPHOR * (arm * ARM_GAIN + wake * WAKE_GAIN) * vis;
  } else if (m < 7.5) {
    // ── hat cells: FX 207 ──
    float fz = cellFrozen(uv);
    float thaw = 1.0 - age / CELL_HOLD;
    vec3 look = mix(h.rgb, (0.2 + lumaOf(h.rgb)) * vec3(0.75, 0.9, 1.15), CELL_COOL);
    vec2 g = abs(fract(uv * CELLS) - 0.5) * 2.0;
    look += vec3(0.7, 0.9, 1.0) * smoothstep(0.88, 0.97, max(g.x, g.y)) * CELL_RIM * thaw;
    col = compose(cam, pl, fig, look, h.a, fz);
  } else if (m < 8.5) {
    // ── loop tiles: FX 216 ──
    float beats = max(grid, 1.0 / 16.0);
    float n = clamp(floor(2.0 / sqrt(beats) + 0.5), 1.0, 8.0);
    vec2 gg = uv * n; vec2 cell = floor(gg); vec2 lp = fract(gg);
    float idx = cell.x + (n - 1.0 - cell.y) * n;
    float liveIdx = mod(floor(BEAT / beats), n * n);
    float live = step(abs(idx - liveIdx), 0.5);
    vec3 c = mix(IMG_NORM_PIXEL(held, lp).rgb * FROZEN_DIM, IMG_NORM_PIXEL(inputImage, lp).rgb, live);
    vec2 e = min(lp, 1.0 - lp);
    float seam = step(min(e.x, e.y), GAP * 0.5);
    float frame = step(min(e.x, e.y), GAP * 1.5) * (1.0 - seam);
    float many = step(1.5, n);
    c = mix(c, vec3(0.02), seam * many);
    c += LIVE_TINT * frame * LIVE_GLOW * live * many;
    c += TILE_FLASH * exp(-age / 0.08);
    col = mix(cam, c, amt);
  } else {
    // ── reveal: FX 268 ──
    vec2 c = floor(uv * GRID_N);
    float i = c.x + GRID_N * (GRID_N - 1.0 - c.y);
    float bar = floor(BAR);
    float mult = 2.0 * floor(h11(bar) * 8.0) + 1.0;          // odd → a permutation of 0..15
    float slot = mod(i * mult + floor(h11(bar + 3.1) * 16.0), 16.0);
    float pos = BAR_PHASE * 16.0;
    float s = floor(pos);
    float open = step(slot, s);
    float fresh = step(abs(slot - s), 0.5) * pow(1.0 - fract(pos), 3.0);
    vec3 closed = mix(h.rgb, vec3(lumaOf(h.rgb)), CLOSED_DIM) * (1.0 - 0.5 * CLOSED_DIM);
    col = compose(cam, pl, fig, closed, h.a, (1.0 - open) * amt);
    float r = regionAt(uv);
    col *= 1.0 + FLIP_GLOW * fresh * open * amt * r;
    vec2 g = abs(fract(uv * GRID_N) - 0.5);
    float ln = 1.0 - smoothstep(0.494, 0.5, max(g.x, g.y));
    col = mix(col, vec3(0.0), (1.0 - ln) * LINE * amt * r);
  }
  gl_FragColor = vec4(col, 1.0);
}
