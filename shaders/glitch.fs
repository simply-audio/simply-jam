/*{
  "DESCRIPTION": "audiodev glitch: RGB split, block datamosh, slice glitch, prism, pixel stretch, kick RGB burst, hat vibrate, edge crackle, kick katana slice, hat micro-glitch. Built to hit on the kick; `where` puts it on the whole picture, the figure (it moves over the room plate) or the room.",
  "CREDIT": "simply-creative FX library (FX 12 23 113 138 143 241 264 269 296 299), ported to tussen ISF",
  "CATEGORIES": ["glitch", "feedback", "depth", "sound", "beat", "modes", "pressure"],
  "INPUTS": [
    { "NAME": "inputImage", "TYPE": "image" },
    { "NAME": "depth", "TYPE": "depth" },
    { "NAME": "wave", "TYPE": "audio" },
    { "NAME": "spectrum", "TYPE": "audioFFT" },
    { "NAME": "amount", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.25, "DESCRIPTION": "pressure: the resting base; kicks, mids and hats push it up" },
    { "NAME": "react", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5, "DESCRIPTION": "how much the music moves it: 0 holds it still, 0.5 as tuned, 1 twice as much" },
    { "NAME": "mode", "TYPE": "long", "MIN": 0, "MAX": 9, "DEFAULT": 0,
      "VALUES": [0, 1, 2, 3, 4, 5, 6, 7, 8, 9],
      "LABELS": ["RGB split", "block mosh", "slice glitch", "prism", "pixel stretch", "RGB burst", "vibrate", "edge crackle", "katana", "micro glitch"] },
    { "NAME": "where", "TYPE": "long", "MIN": 0, "MAX": 2, "DEFAULT": 0, "DESCRIPTION": "0 everywhere · 1 the figure (near) · 2 the room (far)" },
    { "NAME": "kickAmt", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 1.0, "DESCRIPTION": "× each look's own kick engagement" },
    { "NAME": "midsAmt", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 1.0, "DESCRIPTION": "× each look's own mids engagement" },
    { "NAME": "highsAmt", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 1.0, "DESCRIPTION": "× each look's own hats engagement" },
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
    { "TARGET": "hat", "WIDTH": "17", "HEIGHT": "1", "FLOAT": true, "PERSISTENT": true },
    { "TARGET": "plate", "WIDTH": "$WIDTH / 2", "HEIGHT": "$HEIGHT / 2", "PERSISTENT": true },
    { "TARGET": "mosh", "PERSISTENT": true },
    {}
  ]
}*/

// mode 0 · RGB split     FX 12 RGB split (where=1: each channel carries its own silhouette)
// mode 1 · block mosh    FX 23 Block glitch / datamosh (its own persistent pass = u_self)
// mode 2 · slice glitch  FX 113 Figure glitch (where=1); where=0 slices the whole picture
// mode 3 · prism         FX 138 Prism
// mode 4 · pixel stretch FX 143 Pixel stretch
// mode 5 · RGB burst     FX 241 Kick rgb-shift figure (where=1): channels blown out from the figure's centre
// mode 6 · vibrate       FX 264 Highs vibrate figure (where=1)
// mode 7 · edge crackle  FX 269 Highs crackle edges (always the cut's edge)
// mode 8 · katana        FX 296 Kick slice figure (where=1), built from the gallery line
// mode 9 · micro glitch  FX 299 Highs glitch room (where=2), built from the gallery line
// Everything that moves the figure (0 2 5 6 8 with where=1) cuts each colour
// channel with its own moved silhouette and fills behind from the room plate.

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

// ════ hats + room plate (port-glitch extras) ═══════════════════════════
// hat is kept; its last pixel holds the hat state (glitch.fs uses the
// pixels before it for the figure centroid): r slow highs average · g s since the hat onset ·
// b hat count (the old u_hats[0] seed) · a fract(TIME). Chris's FX 264 rule:
// an onset when the highs jump 0.12 above their own 120 ms follower.
vec4 hatS() { return IMG_NORM_PIXEL(hat, vec2(1.0 - 0.5 / IMG_SIZE(hat).x, 0.5)); }
vec4 hatPass() {
  vec4 p = hatS();
  float h = highsNow();
  float dt = mix(clamp(fract(fract(TIME) - p.a + 1.0), 1.0 / 240.0, 0.1), 1.0 / 60.0, 1.0 - step(1.0, FRAMEINDEX));
  float age = mix(60.0, min(p.g + dt, 60.0), step(1.0, FRAMEINDEX)); float count = p.b;
  if (h > 0.35 && h - p.r > 0.12 && age > 0.07) { age = 0.0; count = mod(count + 1.0, 2048.0); }
  return vec4(p.r + (1.0 - exp(-dt / 0.12)) * (h - p.r), age, count, fract(TIME));
}
float hatAge() { return hatS().g; }
float hatSpike() { return exp(-hatAge() / 0.1); }
float hatCount() { return hatS().b; }
// kick counter stand-in (old u_kseed): the last onset's time, new on every kick
float kickSeed() { return fract(floor((TIME - kickAge()) * 20.0) * 0.6180339) * 977.0 + beatSync * floor(BEAT); }

// the room plate: takes in the camera slowly wherever it is not the figure
// (FX 214's trick), so what stands behind a moving figure is there to show
vec4 platePass(vec2 uv) {
  vec3 c = IMG_NORM_PIXEL(inputImage, uv).rgb;
  vec3 prev = IMG_NORM_PIXEL(plate, uv).rgb;
  float k = mix(1.0, 0.02 * (1.0 - figureAt(uv)), step(1.0, FRAMEINDEX));
  return vec4(mix(prev, c, k), 1.0);
}
vec3 camAt(vec2 uv) { return IMG_NORM_PIXEL(inputImage, uv).rgb; }
vec3 roomAt(vec2 uv) { return IMG_NORM_PIXEL(plate, uv).rgb; }
// a room-only treatment reading displaced pixels: where=2 fills figure pixels from the plate
vec3 srcAt(vec2 uv) {
  float r = step(1.5, floor(where + 0.5));
  return mix(camAt(uv), roomAt(uv), r * figureAt(uv));
}
// engagement: pressure + each FX's own kick/mids/highs consts scaled by the knobs
float engage(float ke, float me, float he) {
  return clamp(amount + kickAmt * ke * kickNow() + midsAmt * me * midsNow() + highsAmt * he * highsNow(), 0.0, 1.0);
}
float h11(float n) { return fract(sin(n * 91.345) * 47453.5453); }
float h21(vec2 p) { return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453); }
float hash12(vec2 p) {
  vec3 p3 = fract(vec3(p.xyx) * 0.1031);
  p3 += dot(p3, p3.yzx + 33.33);
  return fract((p3.x + p3.y) * p3.z);
}
float h31(vec3 p) { p = fract(p * 0.1031); p += dot(p, p.zyx + 31.32); return fract((p.x + p.y) * p.z); }
float vnoise1(float x) {
  float i = floor(x); float t = fract(x);
  return mix(hash12(vec2(i, 7.0)), hash12(vec2(i + 1.0, 7.0)), t * t * (3.0 - 2.0 * t));
}
// ═══════════════════════════════════════════════════════════════════════

// Moved-picture compose: `moved` was read at qR/qG/qB per channel.
// where=0 the whole picture moves · where=1 each channel is cut with its own
// moved silhouette over the room (the plate fills where the figure was) ·
// where=2 the room moves (srcAt fills from the plate) and the figure stays on top.
vec3 composeMoved(vec2 uv, vec3 moved, vec2 qR, vec2 qG, vec2 qB) {
  float w = floor(where + 0.5);
  if (w < 0.5) return moved;
  float f = figureAt(uv);
  if (w > 1.5) return mix(moved, camAt(uv), f);
  vec3 mask = vec3(figureAt(qR), figureAt(qG), figureAt(qB));
  vec3 bg = mix(camAt(uv), roomAt(uv), f);
  return mix(bg, moved, mask);
}
vec3 readMoved(vec2 qR, vec2 qG, vec2 qB) {
  return vec3(srcAt(clamp(qR, 0.0, 1.0)).r, srcAt(clamp(qG, 0.0, 1.0)).g, srcAt(clamp(qB, 0.0, 1.0)).b);
}
float hashP(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }

// ── mode 0 · RGB split (FX 12) — grab these first ───────────────────
const float RGB_SHIFT    = 0.035;  // sideways channel offset at full signal, uv
const float RGB_RADIAL   = 0.04;   // extra radial (lens) offset at the corners, uv
const float RGB_SPIN     = 0.35;   // rad/s the shove direction turns
const float RGB_CURVE    = 1.5;    // >1 = gentle at low signal, wild at the top
const float RGB_KICK_PUNCH = 1.5;
const float RGB_MIDS_LENS  = 1.0;
const float RGB_KICK_ENGAGE = 0.8;
const float RGB_MIDS_ENGAGE = 0.3;

vec3 rgbSplit(vec2 uv) {
  float kick = kickNow(); float mids = midsNow();
  float a = pow(engage(RGB_KICK_ENGAGE, RGB_MIDS_ENGAGE, 0.0), RGB_CURVE);
  float ang = TIME * RGB_SPIN;
  vec2 dir = vec2(cos(ang), sin(ang) * 0.35);
  vec2 rc = uv - 0.5;
  a *= 1.0 + RGB_KICK_PUNCH * 0.5 * kick;
  vec2 off = a * (RGB_SHIFT * dir + RGB_RADIAL * (1.0 + RGB_MIDS_LENS * mids) * rc * length(rc) * 2.0);
  return composeMoved(uv, readMoved(uv + off, uv, uv - off), uv + off, uv, uv - off);
}

// ── mode 1 · block mosh (FX 23) — grab these first ──────────────────
const float BG_RATE      = 12.0;
const vec2  BG_BLOCKS    = vec2(16.0, 9.0);
const float BG_MOSH      = 0.30;
const float BG_DISPLACE  = 0.18;
const float BG_JUMP      = 0.12;
const float BG_SMEAR     = 0.006;
const float BG_TEAR_BANDS = 0.25;
const float BG_TEAR      = 0.03;
const float BG_SLICE     = 0.08;
const float BG_KICK_BURST = 1.0;
const float BG_MIDS_TEAR = 1.0;
const float BG_KICK_ENGAGE = 1.0;
const float BG_MIDS_ENGAGE = 0.25;

// runs in its own persistent pass (`mosh` is the old u_self)
vec4 moshPass(vec2 uv) {
  float kick = kickNow(); float mids = midsNow();
  float amt = max(step(0.5, amount), clamp(kickAmt * BG_KICK_ENGAGE * kick + midsAmt * BG_MIDS_ENGAGE * mids, 0.0, 1.0));
  if (amt < 0.02) return vec4(srcAt(uv), 1.0);
  float kb = BG_KICK_BURST * kick;
  float st = floor(TIME * BG_RATE) + 17.0 * floor(kb * 4.0);
  vec2 cc = floor(uv * BG_BLOCKS);
  vec2 fine = floor(uv * BG_BLOCKS * 3.0) + 100.0;
  vec2 cell = mix(fine, cc, step(hashP(cc + st * 0.13), 0.5));
  float h  = hashP(cell + st * 1.37);
  float h2 = hashP(cell + st * 2.71 + 9.1);
  float band = floor(uv.y * 40.0);
  float hb = hashP(vec2(band, st * 0.71));
  vec2 suv = uv;
  suv.x += step(hb, 0.15 * amt) * (hashP(vec2(band, st + 3.0)) - 0.5) * 2.0 * BG_SLICE;
  float moshP = amt * BG_MOSH * (1.0 + kb);
  float disp = amt * BG_DISPLACE * (1.0 + 1.5 * kb);
  vec3 c;
  if (h < moshP) {
    vec2 mv = (vec2(h2, hashP(cell + 5.5)) - 0.5) * 2.0 * BG_SMEAR;
    c = IMG_NORM_PIXEL(mosh, clamp(uv - mv, 0.0, 1.0)).rgb;
  } else if (h < moshP + disp) {
    vec2 off = (vec2(h2, hashP(cell + 7.7)) - 0.5) * 2.0 * BG_JUMP * (1.0 + kb);
    c = srcAt(fract(suv + off));
  } else {
    c = srcAt(fract(suv));
  }
  float tb = floor(uv.y * 24.0);
  if (hashP(vec2(tb, st * 0.37 + 1.0)) < amt * BG_TEAR_BANDS * (1.0 + BG_MIDS_TEAR * mids)) {
    float o = (hashP(vec2(tb, st + 11.0)) - 0.5) * 2.0 * BG_TEAR;
    c.r = srcAt(fract(suv + vec2(o, 0.0))).r;
    c.b = srcAt(fract(suv - vec2(o, 0.0))).b;
  }
  return vec4(c, 1.0);
}

// ── mode 2 · slice glitch (FX 113) — grab these first ───────────────
const float SG_SLICES    = 28.0;
const float SG_MAX_SHIFT = 0.12;
const float SG_TEAR      = 0.018;
const float SG_RATE      = 9.0;
const float SG_BLOCKS    = 0.12;
const float SG_KICK_ENGAGE = 0.9;
const float SG_MIDS_ENGAGE = 0.2;

vec3 sliceGlitch(vec2 uv) {
  float amt = engage(SG_KICK_ENGAGE, SG_MIDS_ENGAGE, 0.0);
  float seed = floor(TIME * SG_RATE) + kickSeed() * 17.0;
  float band = floor(uv.y * SG_SLICES * (0.5 + h11(seed + 3.0)));
  float r = h21(vec2(band, seed));
  float broken = step(1.0 - amt * 0.85, r);
  float shift = (h21(vec2(band + 7.0, seed)) - 0.5) * 2.0 * SG_MAX_SHIFT * amt * broken;
  vec2 g = vec2(uv.x + shift, uv.y);
  vec2 tear = vec2(SG_TEAR * amt * (0.4 + broken), 0.0);
  vec3 c = readMoved(g + tear, g, g - tear);
  vec2 blk = floor(uv * vec2(14.0, 22.0));
  float dead = step(1.0 - SG_BLOCKS * amt, h21(blk + seed * 1.3));
  c = mix(c, vec3(h21(blk + 1.0), h21(blk + 2.0), h21(blk + 3.0)) * vec3(0.3, 1.0, 0.8), dead);
  return composeMoved(uv, c, g + tear, g, g - tear);
}

// ── mode 3 · prism (FX 138) — grab these first ──────────────────────
const float PR_FAN_ANGLE = 0.12;
const float PR_FAN_SHIFT = 0.06;
const vec2  PR_PIVOT     = vec2(0.5, -0.4);
const float PR_SAT       = 0.9;
const float PR_CURVE     = 1.3;
const float PR_KICK_ENGAGE = 0.85;
const float PR_MIDS_ENGAGE = 0.2;
const float PR_KICK_PUNCH  = 0.5;
const float PR_MIDS_TURN   = 0.6;

vec3 hueOf(float h) { return 0.5 + 0.5 * cos(6.2831853 * (h + vec3(0.0, -0.3333, 0.3333))); }
vec2 fanUv(vec2 uv, float a, vec2 shift, float aspect) {
  vec2 p = (uv - PR_PIVOT) * vec2(aspect, 1.0);
  p = mat2(cos(a), -sin(a), sin(a), cos(a)) * p;
  return PR_PIVOT + p * vec2(1.0 / aspect, 1.0) + shift;
}
vec3 prism(vec2 uv) {
  float kick = kickNow(); float mids = midsNow();
  float amt = pow(engage(PR_KICK_ENGAGE, PR_MIDS_ENGAGE, 0.0), PR_CURVE);
  amt *= 1.0 + PR_KICK_PUNCH * kick;
  float aspect = RENDERSIZE.x / RENDERSIZE.y;
  float axis = PR_MIDS_TURN * mids * sin(TIME * 0.8);
  vec2 sdir = vec2(cos(axis), sin(axis));
  vec3 acc = vec3(0.0); vec3 wsum = vec3(0.0);
  for (int i = 0; i < 3; i++) {
    float k = float(i) - 1.0;
    vec3 tint = mix(vec3(1.0), hueOf(float(i) / 3.0 + 0.08) * 1.5, PR_SAT * smoothstep(0.0, 0.15, amt));
    vec2 suv = fanUv(uv, k * PR_FAN_ANGLE * amt + axis * 0.2 * amt, k * PR_FAN_SHIFT * amt * sdir, aspect);
    acc += tint * srcAt(clamp(suv, 0.0, 1.0));
    wsum += tint;
  }
  return acc / wsum;
}

// ── mode 4 · pixel stretch (FX 143) — grab these first ──────────────
const float PS_GLIDE    = 0.4;
const float PS_RANGE    = 0.3;
const float PS_REROLL_S = 6.0;
const float PS_BANDING  = 0.25;
const float PS_KICK_ENGAGE = 0.9;
const float PS_MIDS_ENGAGE = 0.2;
const float PS_MIDS_SHIMMER = 0.004;

vec3 pixelStretch(vec2 uv) {
  float mids = midsNow();
  float amt = engage(PS_KICK_ENGAGE, PS_MIDS_ENGAGE, 0.0);
  float roll = kickSeed() + floor(TIME / PS_REROLL_S) * 13.0;
  float dir = floor(h11(roll) * 4.0);                       // 0 → · 1 ← · 2 ↑ · 3 ↓
  float home = 0.3 + 0.4 * h11(roll + 5.0);
  float pos = clamp(home + PS_RANGE * 0.5 * sin(TIME * PS_GLIDE + roll), 0.05, 0.95);
  float horiz = 1.0 - step(1.5, dir);
  float flip = step(0.5, mod(dir, 2.0));
  float a = mix(uv.y, uv.x, horiz);
  float b = mix(uv.x, uv.y, horiz);
  a = mix(a, 1.0 - a, flip);
  float src = mix(pos, 1.0 - pos, flip);
  float reach = (1.0 - src) * amt;
  float inSmear = step(src, a) * step(a, src + reach);
  float sa = mix(src, 1.0 - src, flip);
  b += PS_MIDS_SHIMMER * mids * sin(a * 90.0 + TIME * 30.0) * inSmear;
  vec2 s = mix(vec2(b, sa), vec2(sa, b), horiz);
  vec3 base = srcAt(uv);
  vec3 smear = srcAt(clamp(s, 0.0, 1.0));
  smear *= 1.0 - PS_BANDING * 0.5 * (0.5 + 0.5 * sin(b * 700.0));
  float tail = inSmear * smoothstep(src + reach, src + reach - 0.02, a);
  return mix(base, smear, tail);
}

// the figure's centroid: the hat pass's first 16 pixels each sum one column
// of a 16×12 grid of the cut (xy weighted, z weight); the reader adds them up
vec4 centPass(float col) {
  vec3 s = vec3(0.0);
  for (int y = 0; y < 12; y++) {
    vec2 q = vec2((col + 0.5) / 16.0, (float(y) + 0.5) / 12.0);
    float m = figureAt(q);
    s += vec3(q * m, m);
  }
  return vec4(s, 1.0);
}
vec2 figCentroid() {
  vec3 s = vec3(0.0);
  for (int x = 0; x < 16; x++) { s += IMG_NORM_PIXEL(hat, vec2((float(x) + 0.5) / 17.0, 0.5)).xyz; }
  return s.z > 0.5 ? s.xy / s.z : vec2(0.5);
}

// ── mode 5 · RGB burst (FX 241) — grab these first ──────────────────
const float RB_SPLIT    = 0.22;   // radial scale-out of the outermost channel at full burst
const vec3  RB_CH_SCALE = vec3(1.0, 0.35, -0.55); // R out, G a little, B in
const float RB_FX_SPLIT = 0.4;    // standing split at full pressure (share of SPLIT)
const float RB_KICK_ENGAGE = 1.0;
const float RB_MIDS_ENGAGE = 0.12;
const float RB_MIDS_WOBBLE = 0.04;
const float RB_GLOW     = 0.25;

vec3 rgbBurst(vec2 uv) {
  float kick = kickNow(); float mids = midsNow();
  float burst = clamp(RB_FX_SPLIT * amount + kickAmt * RB_KICK_ENGAGE * kick * kick + midsAmt * RB_MIDS_ENGAGE * mids, 0.0, 1.0);
  vec2 c = figCentroid();
  vec3 k = 1.0 + RB_SPLIT * burst * RB_CH_SCALE;
  vec3 a = RB_MIDS_WOBBLE * mids * sin(TIME * 2.0 + vec3(0.0, 2.1, 4.2));
  vec2 qR = c + (uv - c) / k.x + vec2(1.0, 0.0) * a.x;
  vec2 qG = c + (uv - c) / k.y + vec2(cos(2.094), sin(2.094)) * a.y;
  vec2 qB = c + (uv - c) / k.z + vec2(cos(4.189), sin(4.189)) * a.z;
  vec3 col = readMoved(qR, qG, qB) * (1.0 + RB_GLOW * burst * abs(RB_CH_SCALE));
  return composeMoved(uv, col, qR, qG, qB);
}

// ── mode 6 · vibrate (FX 264) — grab these first ────────────────────
const float VB_HIGHS_PX = 3.0;   // shake (px) at full highs
const float VB_HAT_PX   = 6.0;   // extra shake (px) on a hat onset
const float VB_PRESS_PX = 2.5;   // pressure: standing shake (px)
const float VB_KICK_PX  = 2.0;
const float VB_MIDS_PX  = 0.6;
const float VB_FRINGE   = 0.45;
const float VB_VERTICAL = 0.6;

vec2 shakeOf(float seed) {
  float fr = FRAMEINDEX;
  return vec2(h11(fr + seed) - 0.5, (h11(fr * 1.37 + seed + 11.0) - 0.5) * VB_VERTICAL) * 2.0;
}
vec3 vibrate(vec2 uv) {
  float pxs = highsAmt * (VB_HIGHS_PX * highsNow() + VB_HAT_PX * hatSpike()) + VB_PRESS_PX * amount
            + kickAmt * VB_KICK_PX * kickNow() + midsAmt * VB_MIDS_PX * midsNow();
  vec2 o = shakeOf(0.0) * pxs / RENDERSIZE;
  vec2 fr = shakeOf(5.0) * pxs * VB_FRINGE / RENDERSIZE;
  vec2 qR = uv - o - fr; vec2 qG = uv - o; vec2 qB = uv - o + fr;
  return composeMoved(uv, readMoved(qR, qG, qB), qR, qG, qB);
}

// ── mode 7 · edge crackle (FX 269) — grab these first ───────────────
const float CR_AMP       = 0.022;
const float CR_GRAIN_PX  = 5.0;
const float CR_HZ        = 30.0;
const vec3  CR_SPARK     = vec3(0.7, 0.85, 1.0);
const float CR_SPARK_GAIN = 0.9;
const float CR_HIGHS_ENGAGE = 0.8;
const float CR_KICK_ENGAGE = 0.6;
const float CR_MIDS_ENGAGE = 0.15;

vec2 hash22(vec2 p) {
  return fract(sin(vec2(dot(p, vec2(127.1, 311.7)), dot(p, vec2(269.5, 183.3)))) * 43758.5453) - 0.5;
}
vec3 crackle(vec2 uv, vec2 px) {
  float amt = engage(CR_KICK_ENGAGE, CR_MIDS_ENGAGE, CR_HIGHS_ENGAGE);
  vec3 cam = camAt(uv);
  if (amt < 0.002) return cam;
  float fig = step(figure, depthAt(uv));
  float seed = floor(TIME * CR_HZ * (0.5 + 0.5 * highsNow()));
  vec2 cell = floor(px / CR_GRAIN_PX);
  vec2 n = hash22(cell + seed * 17.0) + 0.7 * hash22(floor(cell / 4.0) + seed * 3.0);
  vec2 q = uv + n * CR_AMP * amt * vec2(RENDERSIZE.y / RENDERSIZE.x, 1.0);
  float fig2 = step(figure, depthAt(q));
  vec3 bg = mix(cam, roomAt(uv), fig);
  vec3 col = mix(bg, camAt(q), fig2);
  float jumped = abs(fig2 - fig);
  return mix(col, CR_SPARK * (1.2 + lumaOf(col)), jumped * CR_SPARK_GAIN * smoothstep(0.0, 0.3, amt));
}

// ── mode 8 · katana (FX 296, ⏳ in the gallery: built from its line) ──
// The figure is cut into a few tilted slices that slide apart sideways on
// the kick and snap back with it; a white edge flashes along each cut.
const float KT_SLICES   = 6.0;   // slices over the frame height
const float KT_SLIDE    = 0.16;  // uv a slice slides at a full kick
const float KT_TILT     = 0.25;  // slope of the cuts (re-rolled per kick, ±)
const float KT_PRESS    = 0.3;   // pressure: standing slide (share of a kick)
const float KT_TEAR     = 0.12;  // RGB tear in a sliding slice (× its slide)
const vec3  KT_GLEAM    = vec3(0.9, 0.95, 1.0);
const float KT_GLEAM_W  = 0.007; // the cut's bright edge, share of the height (reads from the back)
const float KT_GLEAM_GAIN = 1.4;
const float KT_KICK_ENGAGE = 1.0;
const float KT_MIDS_ENGAGE = 0.1;

vec3 katana(vec2 uv) {
  float kick = kickNow();
  float seed = kickSeed();
  float hit = kickAmt * KT_KICK_ENGAGE * kick;
  float s = clamp(KT_PRESS * amount + hit + midsAmt * KT_MIDS_ENGAGE * midsNow(), 0.0, 1.0);
  float tilt = KT_TILT * (h11(seed) * 2.0 - 1.0);
  float y = (uv.y + tilt * (uv.x - 0.5)) * KT_SLICES + h11(seed + 1.0);
  float band = floor(y);
  float dir = mod(band, 2.0) * 2.0 - 1.0;
  float dx = dir * (0.55 + 0.45 * h21(vec2(band, seed))) * KT_SLIDE * s;
  vec2 q = uv - vec2(dx, 0.0);
  vec2 t = vec2(abs(dx) * KT_TEAR, 0.0);
  vec3 col = composeMoved(uv, readMoved(q + t, q, q - t), q + t, q, q - t);
  float fy = fract(y);
  float d = min(fy, 1.0 - fy) / KT_SLICES;
  float onCut = floor(where + 0.5) < 0.5 ? 1.0 : regionAt(q);
  col += KT_GLEAM * KT_GLEAM_GAIN * exp(-d / KT_GLEAM_W) * clamp(hit, 0.0, 1.0) * onCut;
  return col;
}

// ── mode 9 · micro glitch (FX 299, ⏳ in the gallery: built from its line) ──
// Tiny blocks flicker in the room on the hats: jump, swap channels, invert.
const float MG_BLOCK    = 0.018; // block height, share of the frame height (some run 4× wide)
const float MG_DENS     = 0.12;  // share of blocks glitching at full
const float MG_JUMP     = 0.08;  // how far a block's content jumps, share of the height
const float MG_HZ       = 30.0;  // re-rolls per second (and on every hat)
const float MG_HIGHS_ENGAGE = 1.0;
const float MG_KICK_ENGAGE  = 0.3;
const float MG_MIDS_ENGAGE  = 0.1;

vec3 microGlitch(vec2 uv, vec2 px) {
  float amt = engage(MG_KICK_ENGAGE, MG_MIDS_ENGAGE, MG_HIGHS_ENGAGE);
  float seed = floor(TIME * MG_HZ) + hatCount() * 31.0;
  float bpx = MG_BLOCK * RENDERSIZE.y;
  vec2 wide = floor(px / vec2(bpx * 4.0, bpx));
  vec2 cell = mix(floor(px / bpx), wide + 500.0, step(0.6, h21(wide + seed * 0.11)));
  float on = step(h21(cell + seed * 0.37), MG_DENS * amt);
  vec2 jump = (vec2(h21(cell + seed + 1.0), h21(cell + seed + 2.0)) - 0.5) * 2.0 * MG_JUMP * vec2(RENDERSIZE.y / RENDERSIZE.x, 1.0);
  vec3 c = srcAt(clamp(uv + jump * on, 0.0, 1.0));
  float kind = h21(cell + seed + 3.0);
  c = mix(c, c.brg, step(0.5, kind) * (1.0 - step(0.75, kind)));
  c = mix(c, 1.0 - c, step(0.75, kind) * (1.0 - step(0.9, kind)));
  c = mix(c, vec3(lumaOf(c) * 1.8), step(0.9, kind));
  return mix(srcAt(uv), c, on);
}

void main() {
  vec2 uv = isf_FragNormCoord;
  if (PASSINDEX == 0.0) { gl_FragColor = sigPass(); return; }
  if (PASSINDEX == 1.0) { gl_FragColor = depthPass(uv); return; }
  float m = floor(mode + 0.5);
  if (PASSINDEX == 2.0) {
    float col = floor((isf_FragNormCoord.x * RENDERSIZE.x));
    if (col > 15.5) { gl_FragColor = hatPass(); return; }
    gl_FragColor = abs(m - 5.0) < 0.5 ? centPass(col) : vec4(0.0); return;
  }
  if (PASSINDEX == 3.0) { gl_FragColor = platePass(uv); return; }
  if (PASSINDEX == 4.0) {
    if (abs(m - 1.0) < 0.5) { gl_FragColor = moshPass(uv); return; }
    gl_FragColor = vec4(camAt(uv), 1.0); return;
  }

  vec2 px = (isf_FragNormCoord * RENDERSIZE);
  vec3 cam = camAt(uv);
  vec3 col = cam;
  // modes that move the picture compose themselves (each channel its own silhouette)
  if (m < 0.5) { gl_FragColor = vec4(rgbSplit(uv), 1.0); return; }
  if (m < 1.5) col = IMG_NORM_PIXEL(mosh, uv).rgb;
  else if (m < 2.5) { gl_FragColor = vec4(sliceGlitch(uv), 1.0); return; }
  else if (m < 3.5) col = prism(uv);
  else if (m < 4.5) col = pixelStretch(uv);
  else if (m < 5.5) { gl_FragColor = vec4(rgbBurst(uv), 1.0); return; }
  else if (m < 6.5) { gl_FragColor = vec4(vibrate(uv), 1.0); return; }
  else if (m < 7.5) { gl_FragColor = vec4(crackle(uv, px), 1.0); return; }
  else if (m < 8.5) { gl_FragColor = vec4(katana(uv), 1.0); return; }
  else col = microGlitch(uv, px);
  gl_FragColor = vec4(mix(cam, col, regionAt(uv)), 1.0);
}
