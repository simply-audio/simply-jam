/*{
  "DESCRIPTION": "audiodev depthscape: depth made visible. Keys (luma, hats, dual), the moving cut (pressure plane, clock tide, bass push, bass sink), parallax (wiggle, 3-plane dolly, paper slices), contours, depth rainbow, Joy Division topo lines, depth gradient map, blocky cut. `other` is what shows on the far side of a cut.",
  "CREDIT": "simply-creative FX 31, 54, 61, 62, 63, 78, 127, 189, 215, 224, 245, 251, 275, 285, 292, 294, ported to tussen ISF",
  "CATEGORIES": ["figure", "depth", "sound", "beat", "modes", "pressure"],
  "INPUTS": [
    { "NAME": "inputImage", "TYPE": "image" },
    { "NAME": "depth", "TYPE": "depth" },
    { "NAME": "wave", "TYPE": "audio" },
    { "NAME": "spectrum", "TYPE": "audioFFT" },
    { "NAME": "amount", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 1.0 },
    { "NAME": "react", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5, "DESCRIPTION": "how much the music moves it: 0 holds it still, 0.5 as tuned, 1 twice as much" },
    { "NAME": "mode", "TYPE": "long", "MIN": 0, "MAX": 14, "DEFAULT": 10,
      "VALUES": [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14],
      "LABELS": ["luma key", "hat key", "dual key", "depth plane", "clock tide", "bass push", "bass sink", "wiggle", "dolly", "paper slices", "contours", "depth rainbow", "topo lines", "gradient map", "blocky cut"] },
    { "NAME": "other", "TYPE": "long", "MIN": 0, "MAX": 3, "DEFAULT": 0,
      "VALUES": [0, 1, 2, 3], "LABELS": ["night", "empty room", "depth heat", "negative"],
      "DESCRIPTION": "the far side of a cut (was the NDI room): 0 night · 1 the room with the figure filled out · 2 depth as heat · 3 negative" },
    { "NAME": "where", "TYPE": "long", "MIN": 0, "MAX": 2, "DEFAULT": 0, "DESCRIPTION": "rainbow / topo: 0 everywhere · 1 the figure (near) · 2 the room (far)" },
    { "NAME": "kickAmt", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.6 },
    { "NAME": "midsAmt", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.3 },
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
    { "TARGET": "state", "WIDTH": "1", "HEIGHT": "1", "FLOAT": true, "PERSISTENT": true },
    {}
  ]
}*/

// mode 0  · luma key      FX 31 Luma key
// mode 1  · hat key       FX 189 Hat key
// mode 2  · dual key      FX 245 Dual key
// mode 3  · depth plane   FX 224 Pressure depth plane, FX 61 Depth sweep (same maths: pressure moves the cut, a laser marks it)
// mode 4  · clock tide    FX 215 Clock tide
// mode 5  · bass push     FX 275 Lows depth push (the depth plane, driven by sustained lows)
// mode 6  · bass sink     FX 294 Lows sink
// mode 7  · wiggle        FX 54 Depth parallax
// mode 8  · dolly         FX 251 Pressure split depth parallax
// mode 9  · paper slices  FX 78 Depth slices
// mode 10 · contours      FX 62 Contours
// mode 11 · depth rainbow FX 63 Depth rainbow (where 1 = the original, the figure only)
// mode 12 · topo lines    FX 127 Topo figure, Joy Division (where 1 = the original; where 0 = the whole frame as ridges)
// mode 13 · gradient map  FX 285 Gradient map by depth
// mode 14 · blocky cut    FX 292 Mids mosaic boundary
//
// Depth here is depthAt(): 1 = near, 0 = far, relative per frame (the ML
// map), or blurred brightness until tussen binds it. Chris's maths ran on
// "far-ness" (0 = near); where a port keeps it, far = 1 - depthAt and the
// old FIGURE_FAR is 1 - figure. The NDI room becomes `other`, a second
// treatment of the same camera; the parallax modes use the camera's own
// room with the figure filled out instead (roomFill).

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

// ── grab these first ────────────────────────────────────────────────
// FX 31 luma key
const float LK_HANDOVER  = 0.08;  // amount range where the depth cut hands over to the luma key
const float LK_TH_HI     = 0.85;
const float LK_TH_LO     = 0.05;
const float LK_SOFT      = 0.06;
const float LK_KICK_DROP = 0.25;
const float LK_MIDS_SOFT = 0.1;
// FX 189 hat key
const float HK_GAIN      = 1.3;   // highs → engagement (the star)
const float HK_THR_MIN   = 0.35;
const float HK_SOFT_MIN  = 0.015;
const float HK_SOFT_MAX  = 0.55;
const float HK_SHIMMER   = 0.9;
const vec3  HK_SHIMMER_COL = vec3(0.7, 0.9, 1.3);
// FX 245 dual key
const float DK_LUMA_MIN  = 0.35;
const float DK_SOFT      = 0.06;
const float DK_MIDS      = 0.6;   // mids alone open the luma key (the star)
const vec3  DK_HALO      = vec3(1.0, 0.75, 0.4);
const float DK_HALO_GAIN = 0.35;
// FX 224 / 61 depth plane (far-ness units)
const float PL_NEAR      = 0.05;  // plane at a light touch
const float PL_FAR       = 1.02;  // plane at full pressure
const float PL_TAKE      = 0.08;  // pressure that takes the plane off its rest (the cut)
const float PL_LASER     = 1.1;
const float PL_LASER_W   = 0.015;
const vec3  PL_LASER_COL = vec3(0.3, 1.0, 0.85);
const float PL_SCAN_FILL = 0.12;
const float PL_KICK_PUSH = 0.25;
const float PL_KICK_GLOW = 2.5;   // FX 61: the kick flashes the slice
const float PL_MIDS_WAVE = 0.04;
// FX 275 bass push
const float PUSH_MAX     = 0.4;   // sustained bass moves the cut this much deeper
const float LOWS_RISE    = 0.5;   // s: how long the bass must hold
const float LOWS_FALL    = 1.0;   // s: back
// FX 294 bass sink
const float SINK_TOP     = 0.95;  // how high the line climbs at full bass (frame heights)
const float SINK_WAVE    = 0.012; // ruffle of the line
const vec3  SINK_LINE    = vec3(0.92, 0.97, 1.0);
// FX 215 clock tide
const float TIDE_BEATS   = 16.0;
const float TIDE_MAX     = 0.38;
const float TIDE_SURGE   = 0.12;
const float RIPPLE       = 0.025;
const float RIPPLE_IDLE  = 0.3;
const float FOAM_W       = 0.012;
const float FOAM_GAIN    = 0.9;
const float REFRACT      = 0.012;
const float UNDER_BAND   = 0.12;
const vec3  WATER        = vec3(0.55, 0.85, 1.0);
// FX 54 wiggle
const float WG_AMOUNT    = 0.045;
const float WG_BG        = 0.7;
const float WG_RATE      = 0.8;
const float WG_ELLIPSE   = 0.35;
const float WG_NEAR_POP  = 0.6;
const float WG_KICK      = 1.0;
const float WG_MIDS_TILT = 0.6;
// FX 251 dolly
const float DL_SLIDE     = 0.16;
const float DL_MID_SHARE = 0.45;
const float DL_FAR_SHARE = -0.2;
const float DL_MID_FAR   = 0.8;   // far-ness where the mid plane ends
const float DL_MID_OPAC  = 0.85;
const float DL_MID_DIM   = 0.75;
const float DL_NEAR_SCALE = 0.06;
const float DL_MIDS_SWAY = 0.25;
// FX 78 slices
const float SL_MAX_OFFSET = 0.06;
const float SL_VERTICAL  = 0.3;
const vec2  SL_SHADOW_OFS = vec2(0.006, -0.008);
const float SL_SHADOW    = 0.55;
const float SL_EDGE      = 0.25;
const float SL_KICK      = 0.6;
const float SL_MIDS_LIGHT = 0.6;
// FX 62 contours
const float CT_MIN       = 6.0;
const float CT_MAX       = 48.0;
const float CT_WIDTH_PX  = 1.4;   // at 1080p; scales with the picture
const float CT_DRIFT     = 0.15;
const vec3  CT_NEAR      = vec3(1.0, 0.35, 0.2);
const vec3  CT_FAR       = vec3(0.2, 0.85, 1.0);
const float CT_DIM       = 0.35;
const float CT_KICK      = 1.0;
const float CT_MIDS      = 0.4;
const float CT_GLOW      = 0.45;  // soft halo round each line
// FX 63 rainbow
const float RB_SPREAD    = 1.5;
const float RB_SCROLL    = 0.08;
const float RB_SHADING   = 0.75;
const float RB_GAIN      = 1.15;
const float RB_KICK_HUE  = 0.33;
const float RB_KICK_BRIGHT = 0.5;
const float RB_MIDS      = 1.0;
// FX 127 topo
const float TP_LINES     = 70.0;
const float TP_LIFT      = 0.10;
const float TP_THICK_PX  = 1.4;   // at 360 p, scales with the picture
const float TP_WIGGLE    = 0.25;
const float TP_SPREAD_PX = 6.0;
const vec3  TP_LINE      = vec3(0.96, 0.95, 0.9);
const vec3  TP_FILL      = vec3(0.0);
const float TP_KICK_LIFT = 0.8;
const float TP_MIDS_WIGGLE = 1.5;
// FX 285 gradient map
const vec3  GM_A         = vec3(0.10, 0.02, 0.30);  // deep end of the gradient
const vec3  GM_B         = vec3(0.95, 0.20, 0.45);
const vec3  GM_C         = vec3(1.00, 0.75, 0.25);  // the figure's edge
const float GM_FROM      = 0.1;   // depth where the gradient starts (below: `other`)
const float GM_SHADE     = 0.6;
const float GM_KICK      = 0.15;  // kick pulls the gradient nearer
// FX 292 blocky cut
const float BC_MIN_PX    = 6.0;   // at 1080p
const float BC_MAX_PX    = 64.0;
const float BC_MIDS      = 1.0;
const float BC_EDGE      = 0.25;  // the stair edge glows a little
// ──────────────────────────────────────────────────────────────────

float modeN() { return floor(mode + 0.5); }
float engage() { return clamp(amount + kickAmt * kickNow() + midsAmt * midsNow(), 0.0, 1.0); }
vec3 camAt(vec2 uv) { return IMG_NORM_PIXEL(inputImage, clamp(uv, 0.0, 1.0)).rgb; }
float farAt(vec2 uv) { return 1.0 - depthAt(uv); }
float figFar() { return 1.0 - figure; }
// depth with smooth slopes (4 bilinear taps half a depth texel apart): iso-lines without stair steps
float depthSmoothAt(vec2 uv) {
  vec2 h = 0.5 / max(IMG_SIZE(depthBuf), vec2(1.0));
  return 0.25 * (depthAt(uv + vec2(h.x, h.y)) + depthAt(uv + vec2(-h.x, h.y)) + depthAt(uv + vec2(h.x, -h.y)) + depthAt(uv - h));
}
float inside(vec2 p) { return step(0.0, p.x) * step(p.x, 1.0) * step(0.0, p.y) * step(p.y, 1.0); }
float pxScale() { return RENDERSIZE.y / 1080.0; }
// a firmer cut for the layers that move apart (no half-figure ghosts)
float figHard(vec2 uv) { return smoothstep(figure - 0.25 * soft, figure + 0.25 * soft, depthAt(uv)); }

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vnoise(vec2 p) {
  vec2 i = floor(p); vec2 f = fract(p); f = f * f * (3.0 - 2.0 * f);
  return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
float h1(float x) { return fract(sin(x * 127.1) * 43758.5453); }
float n1(float x) { float i = floor(x); float t = fract(x); t = t * t * (3.0 - 2.0 * t); return mix(h1(i), h1(i + 1.0), t); }

// state 1×1 (fp16-safe): r sustained lows · a fract(TIME)
vec4 statePass() {
  vec4 s = IMG_NORM_PIXEL(state, vec2(0.5));
  float dt = clamp(fract(TIME - s.a), 1.0 / 240.0, 0.1);
  float l = lowsNow();
  float v = s.r * step(0.5, FRAMEINDEX);
  v += (1.0 - exp(-dt / (l > v ? LOWS_RISE : LOWS_FALL))) * (l - v);
  return vec4(v, 0.0, 0.0, fract(TIME));
}
float lowsHeld() { return IMG_NORM_PIXEL(state, vec2(0.5)).r; }

// the camera's room with the figure filled out: the nearest room on the left
// and on the right, stretched in (a cheap inpaint; better as the ML map firms up)
vec3 roomFill(vec2 uv) {
  vec3 c = camAt(uv);
  float f = figHard(uv);
  if (f < 0.02) return c;
  vec3 L = c; vec3 R = c; float dl = 1.0; float dr = 1.0;
  for (int i = 1; i <= 12; i++) {
    float s = float(i) * 0.03;
    if (dl > 0.99 && figHard(uv - vec2(s, 0.0)) < 0.1 && uv.x - s > 0.0) { L = camAt(uv - vec2(s + 0.01, 0.0)); dl = s; }
    if (dr > 0.99 && figHard(uv + vec2(s, 0.0)) < 0.1 && uv.x + s < 1.0) { R = camAt(uv + vec2(s + 0.01, 0.0)); dr = s; }
  }
  vec3 fill = (L / dl + R / dr) / (1.0 / dl + 1.0 / dr);
  return mix(c, fill, f);
}
vec3 heat(float t) {
  t = clamp(t, 0.0, 1.0);
  vec3 a = mix(vec3(0.02, 0.0, 0.12), vec3(0.55, 0.0, 0.6), smoothstep(0.0, 0.4, t));
  a = mix(a, vec3(1.0, 0.35, 0.05), smoothstep(0.35, 0.75, t));
  return mix(a, vec3(1.0, 0.95, 0.6), smoothstep(0.75, 1.0, t));
}
// the far side of a cut (was the NDI room)
vec3 otherAt(vec2 uv) {
  float o = floor(other + 0.5);
  vec3 c = camAt(uv);
  if (o < 0.5) return vec3(0.03, 0.05, 0.13) + lumaOf(c) * vec3(0.35, 0.55, 0.95);
  if (o < 1.5) return roomFill(uv);
  if (o < 2.5) return heat(depthAt(uv));
  return 1.0 - c;
}

// ── keys ──
vec3 lumaKey(vec2 uv, vec3 cam) {                       // FX 31
  float k = kickNow(); float m = midsNow();
  float amt = engage();
  float l = lumaOf(cam);
  float th = mix(LK_TH_HI, LK_TH_LO, smoothstep(LK_HANDOVER, 1.0, amt)) - LK_KICK_DROP * k;
  float sf = LK_SOFT + LK_MIDS_SOFT * m;
  float lk = smoothstep(th - sf, th + sf, l);
  float key = mix(figureAt(uv), lk, smoothstep(0.0, LK_HANDOVER, amt));
  return mix(otherAt(uv), cam, key);
}
vec3 hatKey(vec2 uv, vec3 cam) {                        // FX 189
  float hi = highsNow();
  float e = clamp(HK_GAIN * hi + kickAmt * kickNow() + midsAmt * midsNow(), 0.0, 1.0);
  float thr = mix(1.03, HK_THR_MIN, e);
  float sf = mix(HK_SOFT_MIN, HK_SOFT_MAX, amount);
  float key = smoothstep(thr - sf, thr, lumaOf(cam)) * step(0.001, e + amount);
  vec3 col = mix(otherAt(uv), cam, key);
  float edge = key * (1.0 - key) * 4.0;
  float spark = step(0.6, hash(floor((isf_FragNormCoord * RENDERSIZE) / 2.0) + floor(TIME * 30.0)));
  col += HK_SHIMMER * hi * edge * (0.4 + spark) * HK_SHIMMER_COL;
  return mix(col, cam, figureAt(uv));
}
vec3 dualKey(vec2 uv, vec3 cam) {                       // FX 245
  float share = clamp(amount + DK_MIDS * midsNow() + kickAmt * kickNow(), 0.0, 1.0);
  float figD = figureAt(uv);
  if (share < 0.002) return mix(otherAt(uv), cam, figD);
  float thr = mix(1.0 + DK_SOFT * 1.5, DK_LUMA_MIN, share);
  float lk = smoothstep(thr - DK_SOFT, thr + DK_SOFT, lumaOf(cam)) * (1.0 - figD);
  float key = max(figD, lk);
  vec3 col = mix(otherAt(uv), cam, key);
  col += DK_HALO * lk * (1.0 - lk) * 4.0 * DK_HALO_GAIN * share;
  return col;
}

// ── the moving cut: a plane in far-ness, cam nearer than it, `other` behind, a laser on it ──
vec3 planeCut(vec2 uv, vec3 cam, float plane, float eng) {
  float d = farAt(uv);
  float k = kickNow();
  plane += PL_MIDS_WAVE * midsNow() * sin(uv.x * 18.0 + uv.y * 7.0 - TIME * 3.0);
  float near = smoothstep(plane + 0.01, plane - 0.01, d);
  vec3 col = mix(otherAt(uv), cam, near);
  float laser = exp(-abs(d - plane) / PL_LASER_W) * eng;
  col = mix(col, col + PL_LASER_COL * PL_SCAN_FILL, near * smoothstep(0.15, 0.0, plane - d) * eng);
  col += PL_LASER_COL * (PL_LASER + PL_KICK_GLOW * k * 0.3) * laser * laser;
  return col;
}
vec3 depthPlane(vec2 uv, vec3 cam) {                    // FX 224 + FX 61
  float p = amount;
  float rest = figFar();
  float w = smoothstep(0.0, 1.0, clamp(p / PL_TAKE, 0.0, 1.0));
  float plane = mix(rest, PL_NEAR + (PL_FAR - PL_NEAR) * p, w) + PL_KICK_PUSH * kickNow();
  float eng = clamp(abs(plane - rest) * 6.0 + p * 2.0 + kickNow() + midsNow() * 0.5, 0.0, 1.0);
  return planeCut(uv, cam, min(plane, 1.02), eng);
}
vec3 bassPush(vec2 uv, vec3 cam) {                      // FX 275
  float l = lowsHeld();
  float plane = figFar() + PUSH_MAX * amount * l + 0.05 * kickAmt * kickNow();
  return planeCut(uv, cam, min(plane, 1.02), clamp(l * 1.5, 0.0, 1.0) * amount);
}
vec3 bassSink(vec2 uv, vec3 cam) {                      // FX 294
  float l = clamp(lowsHeld() * amount, 0.0, 1.0);
  float lvl = SINK_TOP * l * l * (3.0 - 2.0 * l);
  float wave = SINK_WAVE * (sin(uv.x * 40.0 + TIME * 3.0) + 0.5 * sin(uv.x * 97.0 - TIME * 5.0)) * smoothstep(0.0, 0.05, lvl);
  float dy = uv.y - (lvl + wave);                       // < 0 = sunk
  float f = figureAt(uv);
  float sunk = f * smoothstep(0.004, -0.004, dy);
  vec3 col = mix(cam, otherAt(uv), sunk);
  col = mix(col, SINK_LINE, f * exp(-pow(dy / 0.006, 2.0)) * 0.9 * smoothstep(0.0, 0.03, lvl));
  return col;
}
vec3 clockTide(vec2 uv, vec3 cam) {                     // FX 215
  float amt = engage();
  float depth = farAt(uv);
  float t = mod(BEAT, TIDE_BEATS) / TIDE_BEATS;
  float tide = -cos(6.2831853 * t);
  float far = figFar() + TIDE_MAX * amt * tide + TIDE_SURGE * kickNow();
  float ruffle = (vnoise(uv * vec2(18.0, 6.0) + vec2(TIME * 1.3, TIME * 0.4)) - 0.5)
                 * 2.0 * RIPPLE * (RIPPLE_IDLE + (1.0 - RIPPLE_IDLE) * midsNow());
  float dd = depth - (far + ruffle * amt);               // > 0 = under water
  float fig = smoothstep(0.005, -0.005, dd);
  float under = (1.0 - fig) * (1.0 - smoothstep(0.0, UNDER_BAND, dd)) * amt;
  vec2 wob = vec2(sin(uv.y * 60.0 + TIME * 4.0), cos(uv.x * 50.0 - TIME * 3.0)) * REFRACT * under;
  vec3 o = otherAt(uv + wob);
  o = mix(o, o * WATER, under * 0.6);
  vec3 col = mix(o, cam, fig);
  float foam = exp(-pow(dd / FOAM_W, 2.0)) * FOAM_GAIN * amt;
  return mix(col, vec3(0.92, 0.97, 1.0), clamp(foam, 0.0, 1.0));
}

// ── parallax ──
vec3 wiggle(vec2 uv) {                                  // FX 54
  float k = kickNow(); float m = midsNow();
  float fx = engage();
  float ph = TIME * WG_RATE * 6.2831853;
  float a = WG_AMOUNT * fx * (1.0 + WG_KICK * k);
  vec2 o = a * vec2(sin(ph), (WG_ELLIPSE + WG_MIDS_TILT * m) * cos(ph));
  float zoom = 1.0 + 2.0 * WG_AMOUNT * WG_BG * fx * (1.0 + WG_KICK);
  vec3 c = roomFill((uv - 0.5) / zoom + 0.5 + o * WG_BG);
  vec2 fuv = uv - o;
  float pop = 1.0 + WG_NEAR_POP * clamp((depthAt(fuv) - figure) / max(1.0 - figure, 0.05), 0.0, 1.0);
  fuv = uv - o * pop;
  return mix(c, camAt(fuv), figHard(fuv) * inside(fuv));
}
vec3 dolly(vec2 uv) {                                   // FX 251
  float a = clamp(amount + kickAmt * kickNow(), 0.0, 1.0);
  a = a * a * (3.0 - 2.0 * a);
  float x = DL_SLIDE * (a + DL_MIDS_SWAY * midsNow() * sin(TIME * 0.9));
  vec3 col = roomFill(clamp(uv - vec2(x * DL_FAR_SHARE, 0.0), 0.0, 1.0));
  vec2 muv = uv - vec2(x * DL_MID_SHARE, 0.0);
  float md = farAt(muv);
  float mid = (1.0 - figHard(muv)) * (1.0 - smoothstep(DL_MID_FAR - 0.05, DL_MID_FAR, md)) * inside(muv);
  col = mix(col, camAt(muv) * DL_MID_DIM, mid * DL_MID_OPAC * smoothstep(0.0, 0.3, abs(x) / DL_SLIDE));
  vec2 nuv = (uv - vec2(x, 0.0) - 0.5) / (1.0 + DL_NEAR_SCALE * abs(x) / DL_SLIDE) + 0.5;
  return mix(col, camAt(nuv), figHard(nuv) * inside(nuv));
}
// FX 78: is uv in paper band k (0 = nearest) of the figure?
float inBand(vec2 uv, int k) {
  float n = depthAt(uv);
  float b = min(floor((1.0 - n) / max(figFar(), 0.05) * 5.0), 4.0);
  return step(figure, n) * (b == float(k) ? 1.0 : 0.0) * inside(uv);
}
vec2 bandOffset(int k, float a) {
  float dir = (k - (k / 2) * 2 == 0 ? 1.0 : -1.0) * (float(k / 2) + 1.0);
  return a * SL_MAX_OFFSET * vec2(dir, SL_VERTICAL * dir);
}
vec3 slices(vec2 uv) {
  float a = engage() * (1.0 + SL_KICK * kickNow());
  vec3 col = roomFill(uv);
  float hit = 0.0; float shadow = 0.0;
  vec2 tx = 2.0 / RENDERSIZE;
  for (int k = 0; k < 5; k++) {
    if (hit < 0.5) {
      vec2 s = uv - bandOffset(k, a);
      if (inBand(s, k) > 0.5) {
        float rim = 1.0 - inBand(s + vec2(tx.x, 0.0), k) * inBand(s - vec2(tx.x, 0.0), k)
                        * inBand(s + vec2(0.0, tx.y), k) * inBand(s - vec2(0.0, tx.y), k);
        col = camAt(s) * (1.0 + (SL_EDGE + SL_MIDS_LIGHT * midsNow()) * rim * min(a, 1.0));
        hit = 1.0;
        for (int j = 0; j < 5; j++) {
          if (j >= k) break;
          shadow = max(shadow, inBand(uv - bandOffset(j, a) - SL_SHADOW_OFS * a * 4.0, j));
        }
      }
    }
  }
  if (hit < 0.5) {
    for (int j = 0; j < 5; j++) shadow = max(shadow, inBand(uv - bandOffset(j, a) - SL_SHADOW_OFS * a * 4.0, j));
  }
  return col * (1.0 - SL_SHADOW * shadow * smoothstep(0.0, 0.15, a));
}

// ── depth drawn ──
vec3 contours(vec2 uv, vec3 cam) {                      // FX 62
  float k = kickNow(); float m = midsNow();
  float depth = 1.0 - depthSmoothAt(uv);
  float amt = engage();
  float n = mix(CT_MIN, CT_MAX, amt * amt) * (1.0 + CT_MIDS * m);
  float x = depth * n - TIME * CT_DRIFT;
  float fw = max(fwidth(x), 1e-4);
  float dist = abs(fract(x + 0.5) - 0.5) / fw;          // px to the nearest line
  float major = step(abs(mod(floor(x + 0.5), 5.0)), 0.5);
  float wpx = CT_WIDTH_PX * max(pxScale(), 0.5) * (1.0 + 1.5 * CT_KICK * k) * mix(1.0, 1.8, major);
  float line = 1.0 - smoothstep(wpx * 0.5, wpx * 0.5 + 1.0, dist);
  float glow = exp(-dist / (wpx * 2.5)) * CT_GLOW;
  float dense = 1.0 - smoothstep(0.25, 0.6, fw);        // lines closer than ~2 px fade (no moiré)
  line = line * mix(0.7, 1.0, major) * dense;
  glow *= dense;
  float a = clamp(smoothstep(0.0, 0.1, amount) + kickAmt * k + midsAmt * m, 0.0, 1.0);
  vec3 col = cam * (1.0 - CT_DIM * a);
  vec3 ink = mix(mix(CT_NEAR, CT_FAR, depth), vec3(1.0), clamp(CT_KICK * k, 0.0, 1.0));
  col += ink * glow * a;
  return mix(col, ink, clamp(line * a, 0.0, 1.0));
}
vec3 rainbow(vec2 uv, vec3 cam) {                       // FX 63
  float k = kickNow(); float m = midsNow();
  float d = farAt(uv);
  float t = d / max(figFar(), 0.05) * (RB_SPREAD + RB_MIDS * m) - TIME * RB_SCROLL + RB_KICK_HUE * k;
  vec3 rb = (0.5 + 0.5 * cos(6.2831853 * (t + vec3(0.0, 0.33, 0.67)))) * RB_GAIN * (1.0 + RB_KICK_BRIGHT * k);
  vec3 lit = rb * mix(1.0, 0.25 + 1.2 * lumaOf(cam), RB_SHADING);
  return mix(cam, lit, engage() * regionAt(uv));
}
// FX 127: how high the line sitting at row y lifts at column x
float tpLift(float x, float y, float px, float row, float whole) {
  float s = 0.0;
  for (int i = -2; i <= 2; i++) {
    vec2 p = vec2(x + float(i) * px * TP_SPREAD_PX, y);
    float n = depthAt(p);
    s += mix(figureAt(p) * smoothstep(figure, 1.0, n), n * n, whole);
  }
  s /= 5.0;
  float w = TP_WIGGLE * (1.0 + TP_MIDS_WIGGLE * midsNow());
  float nz = n1(x * 90.0 + row * 17.3 + TIME * 0.7) * 0.6 + n1(x * 230.0 - row * 5.1) * 0.4;
  return s * (1.0 - w + w * 2.0 * nz);
}
vec3 topo(vec2 uv, vec3 cam) {
  float wh = floor(where + 0.5);
  float whole = wh < 0.5 ? 1.0 : 0.0;
  float amt = engage();
  float px = 1.0 / RENDERSIZE.x; float py = 1.0 / RENDERSIZE.y;
  float thick = TP_THICK_PX * max(RENDERSIZE.y / 360.0, 1.0);
  float H = TP_LIFT * (1.0 + TP_KICK_LIFT * kickNow());
  float sp = 1.0 / TP_LINES;
  float k0 = floor((uv.y - H) / sp); float k1 = floor(uv.y / sp) + 1.0;
  float cover = 0.0; float line = 0.0;
  for (int i = 0; i < 24; i++) {
    float k = k0 + float(i);
    if (k > k1) break;
    float y0 = k * sp;
    if (y0 >= 0.0) {
      float l = tpLift(uv.x, y0, px, k, whole);
      float top = y0 + H * l;
      if (l > 0.02 && uv.y <= top + thick * py && uv.y >= y0 - thick * py) {
        cover = 1.0;
        line = 1.0 - smoothstep(thick * 0.5, thick, abs(uv.y - top) / py);
        break;
      }
    }
  }
  float fig = max(figureAt(uv), whole);
  float flatLine = 1.0 - smoothstep(thick * 0.5, thick, abs(fract(uv.y / sp + 0.5) - 0.5) * sp / py);
  if (cover < 0.5) line = fig * flatLine * 0.5;
  float m = max(cover, fig);
  vec3 room = wh > 1.5 ? otherAt(uv) : cam;
  vec3 tp = mix(room, mix(TP_FILL, TP_LINE, line), m);
  return mix(cam, tp, amt);
}
vec3 gradientMap(vec2 uv, vec3 cam) {                   // FX 285
  float k = kickNow(); float m = midsNow();
  float n = depthAt(uv) + GM_KICK * k;
  float lo = GM_FROM; float hi = figure;
  float t = clamp((n - lo) / max(hi - lo, 0.05), 0.0, 1.0);
  t = fract(t + 0.15 * m * sin(TIME * 0.5)) * step(0.0, t);
  vec3 g = mix(GM_A, GM_B, smoothstep(0.0, 0.6, t));
  g = mix(g, GM_C, smoothstep(0.55, 1.0, t));
  g *= mix(1.0, 0.35 + 1.3 * lumaOf(cam), GM_SHADE);
  vec3 col = mix(otherAt(uv), g, smoothstep(lo - 0.05, lo + 0.05, n));
  col = mix(col, cam, smoothstep(hi - soft, hi + soft, n));
  return mix(cam, col, engage());
}
vec3 blockyCut(vec2 uv, vec3 cam) {                     // FX 292
  float e = clamp(BC_MIDS * midsNow() + 0.5 * kickAmt * kickNow(), 0.0, 1.0);
  float b = mix(BC_MIN_PX, BC_MAX_PX, e) * pxScale();
  b = max(b, 2.0);
  vec2 pix = uv * RENDERSIZE;                           // y up (gl_FragCoord runs y down in tussen)
  vec2 cell = floor(pix / b);
  float f = step(figure, depthAt((cell + 0.5) * b / RENDERSIZE));
  // a stair edge: the cell differs from a neighbour
  float fx = step(figure, depthAt((cell + vec2(1.5, 0.5)) * b / RENDERSIZE));
  float fy = step(figure, depthAt((cell + vec2(0.5, 1.5)) * b / RENDERSIZE));
  vec2 inCell = fract(pix / b);
  float edge = abs(f - fx) * step(1.0 - 1.5 / b, inCell.x) + abs(f - fy) * step(1.0 - 1.5 / b, inCell.y);
  vec3 col = mix(otherAt(uv), cam, f) + BC_EDGE * clamp(edge, 0.0, 1.0);
  return mix(cam, col, amount);
}

void main() {
  vec2 uv = isf_FragNormCoord;
  if (PASSINDEX == 0.0) { gl_FragColor = sigPass(); return; }
  if (PASSINDEX == 1.0) { gl_FragColor = depthPass(uv); return; }
  if (PASSINDEX == 2.0) { gl_FragColor = statePass(); return; }

  vec3 cam = IMG_NORM_PIXEL(inputImage, uv).rgb;
  float md = modeN();
  vec3 col = cam;
  if (md == 0.0) col = lumaKey(uv, cam);
  else if (md == 1.0) col = hatKey(uv, cam);
  else if (md == 2.0) col = dualKey(uv, cam);
  else if (md == 3.0) col = depthPlane(uv, cam);
  else if (md == 4.0) col = clockTide(uv, cam);
  else if (md == 5.0) col = bassPush(uv, cam);
  else if (md == 6.0) col = bassSink(uv, cam);
  else if (md == 7.0) col = wiggle(uv);
  else if (md == 8.0) col = dolly(uv);
  else if (md == 9.0) col = slices(uv);
  else if (md == 10.0) col = contours(uv, cam);
  else if (md == 11.0) col = rainbow(uv, cam);
  else if (md == 12.0) col = topo(uv, cam);
  else if (md == 13.0) col = gradientMap(uv, cam);
  else col = blockyCut(uv, cam);
  gl_FragColor = vec4(col, 1.0);
}
