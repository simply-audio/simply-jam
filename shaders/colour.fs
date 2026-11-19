/*{
  "DESCRIPTION": "audiodev colour: the colour and grade family (levels, posterize, duotone, screen print, hue, tint, complement split, solarize, thermal, colour cycle, bleach, teal-orange, sepia, sunset, infrared, sin city, silhouette, gradient fill, bar grade, palette swap, bands to RGB), each a mode, on the whole picture, the figure or the room",
  "CREDIT": "simply-creative FX library (Chris), ported to tussen ISF",
  "CATEGORIES": ["colour", "depth", "sound", "beat", "modes", "pressure"],
  "INPUTS": [
    { "NAME": "inputImage", "TYPE": "image" },
    { "NAME": "depth", "TYPE": "depth" },
    { "NAME": "wave", "TYPE": "audio" },
    { "NAME": "spectrum", "TYPE": "audioFFT" },
    { "NAME": "amount", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.25, "DESCRIPTION": "pressure: how far the look goes; rests at 0.25 so kicks and mids still push it (1 = a steady look; hue rotate: the angle in turns)" },
    { "NAME": "react", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5, "DESCRIPTION": "how much the music moves it: 0 holds it still, 0.5 as tuned, 1 twice as much" },
    { "NAME": "mode", "TYPE": "long", "MIN": 0, "MAX": 20, "DEFAULT": 0,
      "VALUES": [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20],
      "LABELS": ["cel", "posterize", "duotone", "screen print", "hue rotate", "tint", "complement split", "solarize", "thermal", "colour cycle", "bleach bypass", "teal-orange", "sepia film", "sunset", "infrared", "sin city", "silhouette", "gradient fill", "bar grade", "palette swap", "bands to RGB"] },
    { "NAME": "where", "TYPE": "long", "MIN": 0, "MAX": 2, "DEFAULT": 0, "DESCRIPTION": "0 everywhere · 1 the figure (near) · 2 the room (far)" },
    { "NAME": "cycle", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0, "DESCRIPTION": "the clocked variant: posterize steps down per beat (FX 211), silhouette takes the next colour per kick (FX 274)" },
    { "NAME": "hue", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0, "DESCRIPTION": "tint: the hue on the colour wheel (a key's position); 0 = drift slowly round" },
    { "NAME": "kickAmt", "TYPE": "float", "MIN": 0.0, "MAX": 2.0, "DEFAULT": 1.0, "DESCRIPTION": "× each FX's KICK_ENGAGE" },
    { "NAME": "midsAmt", "TYPE": "float", "MIN": 0.0, "MAX": 2.0, "DEFAULT": 1.0, "DESCRIPTION": "× each FX's MIDS_ENGAGE" },
    { "NAME": "highsAmt", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0, "DESCRIPTION": "posterize: × HIGHS_ENGAGE, hats engage it and flicker the level count (FX 234: 1)" },
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
    { "TARGET": "stateBuf", "WIDTH": "4", "HEIGHT": "1", "FLOAT": true, "PERSISTENT": true },
    {}
  ]
}*/

// audiodev colour — Chris's colour and grade FX as one shader.
// `where` (0 whole · 1 figure · 2 room) folds the figure-only / room-only
// versions in; modes marked [both] treat figure and room each their own way
// and ignore `where`.
//
// mode 0  · cel            FX 6 Cel shading (where=1)
// mode 1  · posterize      FX 8 Quantize · FX 211 Beat posterize steps (cycle=1)
//                          · FX 234 Hat posterize figure (where=1 highsAmt=1)
// mode 2  · duotone        FX 40 Duotone
// mode 3  · screen print   FX 229 Two-tone layers [both] (kick swaps the inks)
// mode 4  · hue rotate     FX 41 Hue rotate · FX 283 Mids hue figure only (where=1)
// mode 5  · tint           FX 102 Key hue (the key's position → `hue`, pressure → amount)
// mode 6  · complement     FX 213 Complement split [both]
// mode 7  · solarize       FX 42 Solarize · FX 258 Kick negative figure (where=1 amount=0)
// mode 8  · thermal        FX 19 Thermal (where=1) · FX 134 Thermal room (where=2)
// mode 9  · colour cycle   FX 146 Colour cycle
// mode 10 · bleach bypass  FX 136 Bleach bypass
// mode 11 · teal-orange    FX 137 Teal-orange grade
// mode 12 · sepia film     FX 135 Sepia flashback (where=2)
// mode 13 · sunset         FX 270 Mids sunset grade (long figure shadow)
// mode 14 · infrared       FX 88 Infrared figure [both]
// mode 15 · sin city       FX 293 Kick colour pop [both]
// mode 16 · silhouette     FX 15 Silhouette fill (where=1) · FX 274 Kick strobe colours (where=1 cycle=1)
// mode 17 · gradient fill  FX 263 Figure fill gradient (where=1)
// mode 18 · bar grade      FX 185 Bar grade (four grades round the bar)
// mode 19 · palette swap   FX 248 Clock colour swap [both]
// mode 20 · bands to RGB   FX 259 Bands to channels

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


// ════ colour: helpers ══════════════════════════════════════════════════
const float TAU = 6.2831853;

float h21(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float h11(float x) { return fract(sin(x * 91.17 + 1.3) * 43758.5453); }
// 4×4 ordered dither, 0..1 (the Bayer matrix by maths)
float bayer2(vec2 a) { a = floor(a); return fract(dot(a, vec2(0.5, a.y * 0.75))); }
float bayer4(vec2 a) { return bayer2(0.5 * a) * 0.25 + bayer2(a); }
// hue turn around the grey axis (Rodrigues), radians
vec3 roll(vec3 c, float a) {
  vec3 k = vec3(0.57735);
  return c * cos(a) + cross(k, c) * sin(a) + k * dot(k, c) * (1.0 - cos(a));
}
// tussenHueRotate: YIQ, luma kept, turns
vec3 hueRotate(vec3 c, float turns) {
  float y = dot(c, vec3(0.299, 0.587, 0.114));
  float i = dot(c, vec3(0.595716, -0.274453, -0.321263));
  float q = dot(c, vec3(0.211456, -0.522591, 0.311135));
  float s = sin(turns * TAU); float co = cos(turns * TAU);
  vec2 iq = vec2(co * i - s * q, s * i + co * q);
  return max(vec3(y + 0.9563 * iq.x + 0.6210 * iq.y,
                  y - 0.2721 * iq.x - 0.6474 * iq.y,
                  y - 1.1070 * iq.x + 1.7046 * iq.y), vec3(0.0));
}
vec3 hueCol(float h) { return 0.5 + 0.5 * cos(TAU * (h + vec3(0.0, -0.3333, 0.3333))); }
vec3 cam3(vec2 uv) { return IMG_NORM_PIXEL(inputImage, uv).rgb; }
// pixels from the figure's outline (its 0.5 contour). The figure is soft
// (¼-size depth), so the old engine's neighbour-sum rims turn into wide
// bands here; the contour distance keeps an ink line a few pixels wide.
float outlinePx(float f) { return abs(f - 0.5) / max(fwidth(f), 1e-4); }

// ════ colour: state pass ═══════════════════════════════════════════════
// stateBuf is 4×1 and kept from frame to frame:
//   pixel 0: r kicks counted (mod 4) · g ink swap glide 0↔1 (FX 229) · b previous kick · a last TIME
//   pixel 1: r gradient slide phase (FX 263) · g slow mids "sun" (FX 270)
//   pixel 2: the room's average colour · pixel 3: the figure's (FX 248)
vec4 stA() { return IMG_NORM_PIXEL(stateBuf, vec2(0.125, 0.5)); }
vec4 stB() { return IMG_NORM_PIXEL(stateBuf, vec2(0.375, 0.5)); }
vec3 roomAvg() { return IMG_NORM_PIXEL(stateBuf, vec2(0.625, 0.5)).rgb; }
vec3 figAvg() { return IMG_NORM_PIXEL(stateBuf, vec2(0.875, 0.5)).rgb; }

vec4 statePass() {
  float px = floor(isf_FragNormCoord.x * 4.0);
  vec4 a = stA(); vec4 b = stB();
  float dt = clamp(fract(fract(TIME) - a.a + 1.0), 1.0 / 240.0, 0.1);
  float k = kickNow();
  if (px < 0.5) {
    float hit = step(0.95, k) * step(a.b, 0.95);             // a fresh kick
    float count = mod(a.r + hit, 4.0);
    float target = mod(count, 2.0);                          // each kick flips the inks
    float sw = a.g + (1.0 - exp(-dt / 0.06)) * (target - a.g);
    return vec4(count, sw, k, fract(TIME));
  }
  if (px < 1.5) {
    float m = midsNow();
    float slide = b.r + dt * (0.05 + 0.9 * m + 0.4 * k);
    float sun = b.g + (1.0 - exp(-dt / (m > b.g ? 1.2 : 2.5))) * (m - b.g);
    return vec4(slide, sun, 0.0, 1.0);
  }
  // averages over a sparse 8×6 grid: the room (far) and the figure (near)
  vec3 r = vec3(0.0); vec3 g = vec3(0.0); float nr = 0.0; float nf = 0.0;
  for (int j = 0; j < 6; j++) {
    for (int i = 0; i < 8; i++) {
      vec2 p = (vec2(float(i), float(j)) + 0.5) / vec2(8.0, 6.0);
      vec3 c = cam3(p);
      float f = figureAt(p);
      r += c * (1.0 - f); nr += 1.0 - f;
      g += c * f; nf += f;
    }
  }
  if (px < 2.5) return vec4(nr > 0.5 ? r / nr : vec3(0.5), 1.0);
  return vec4(nf > 0.5 ? g / nf : vec3(0.5), 1.0);
}

// ════ colour: the looks ════════════════════════════════════════════════

// ironbow, FX 19: black → indigo → magenta → orange → yellow → white
vec3 ironbow5(float t) {
  t = clamp(t, 0.0, 1.0);
  float s = t * 5.0;
  if (s < 1.0) return mix(vec3(0.00, 0.00, 0.05), vec3(0.20, 0.02, 0.45), s);
  if (s < 2.0) return mix(vec3(0.20, 0.02, 0.45), vec3(0.75, 0.05, 0.50), s - 1.0);
  if (s < 3.0) return mix(vec3(0.75, 0.05, 0.50), vec3(1.00, 0.45, 0.05), s - 2.0);
  if (s < 4.0) return mix(vec3(1.00, 0.45, 0.05), vec3(1.00, 0.85, 0.20), s - 3.0);
  return mix(vec3(1.00, 0.85, 0.20), vec3(1.00, 1.00, 0.92), s - 4.0);
}
// ironbow, FX 134: black → deep blue → purple → red → orange → yellow → white
vec3 ironbow6(float t) {
  t = clamp(t, 0.0, 1.0);
  float s = t * 6.0;
  if (s < 1.0) return mix(vec3(0.0), vec3(0.1, 0.0, 0.45), s);
  if (s < 2.0) return mix(vec3(0.1, 0.0, 0.45), vec3(0.55, 0.0, 0.6), s - 1.0);
  if (s < 3.0) return mix(vec3(0.55, 0.0, 0.6), vec3(0.9, 0.1, 0.15), s - 2.0);
  if (s < 4.0) return mix(vec3(0.9, 0.1, 0.15), vec3(1.0, 0.55, 0.0), s - 3.0);
  if (s < 5.0) return mix(vec3(1.0, 0.55, 0.0), vec3(1.0, 0.95, 0.3), s - 4.0);
  return mix(vec3(1.0, 0.95, 0.3), vec3(1.0), s - 5.0);
}

// mode 0 · cel (FX 6) ───────────────────────────────────────────────────
const float CEL_LEVELS_LO = 10.0;  // luma bands at a touch of signal
const float CEL_LEVELS_HI = 3.0;   // luma bands at full signal
const float CEL_SAT       = 0.8;   // extra saturation at full signal
const float CEL_INK       = 1.0;   // ink line darkness at full signal
const float CEL_INK_EDGE  = 0.18;  // luma-gradient threshold for an ink line
const float CEL_INK_PX    = 1.5;   // ink sample radius, px
const float CEL_KICK_INK  = 1.5;   // kick: ink lines thicken by this ×
const float CEL_KICK_FLASH = 0.35; // kick: flat colour brightens on the hit
const float CEL_KICK_BAND = 1.0;   // kick: bands dropped on the hit
const float CEL_MIDS_SAT  = 0.8;   // mids: extra saturation
vec3 celLook(vec2 uv, vec3 cam, float fx, float k, float m) {
  float levels = max(mix(CEL_LEVELS_LO, CEL_LEVELS_HI, fx) - CEL_KICK_BAND * k, 2.0);
  float l = lumaOf(cam);
  float q = (floor(l * levels) + 0.5) / levels;
  vec3 ch = mix(vec3(1.0), cam / max(l, 1e-3), 1.0 + (CEL_SAT + CEL_MIDS_SAT * m) * fx);
  vec3 c = clamp(ch * q, 0.0, 1.0) * (1.0 + CEL_KICK_FLASH * k);
  vec2 t = CEL_INK_PX * (1.0 + CEL_KICK_INK * k) / RENDERSIZE;
  float lx = lumaOf(cam3(uv + vec2(t.x, 0.0))) - lumaOf(cam3(uv - vec2(t.x, 0.0)));
  float ly = lumaOf(cam3(uv + vec2(0.0, t.y))) - lumaOf(cam3(uv - vec2(0.0, t.y)));
  float ink = smoothstep(CEL_INK_EDGE, CEL_INK_EDGE * 2.0, length(vec2(lx, ly)));
  c *= 1.0 - CEL_INK * fx * ink;
  return mix(cam, c, smoothstep(0.0, 0.15, fx));
}
// the figure's own silhouette as an ink line (used with where = 1)
float celRim(vec2 uv, float k) {
  float r = 2.0 * CEL_INK_PX * (1.0 + CEL_KICK_INK * k);
  return 1.0 - smoothstep(r - 1.0, r, outlinePx(figureAt(uv)));
}

// mode 1 · posterize (FX 8, 211, 234) ───────────────────────────────────
const float PO_LEVELS_MAX = 16.0;  // levels per channel at a light touch (FX 8)
const float PO_LEVELS_MIN = 2.0;   // … at full signal
const float PO_FADE_IN    = 0.08;
const float PO_DITHER     = 0.35;  // ordered dither (0 = clean bands)
const float PO_SATURATE   = 1.25;
const float PO_KICK_CRUSH = 0.7;   // kick cuts the levels by this fraction
const float PO_KICK_POP   = 0.25;  // kick contrast pop
const float PO_MIDS_SAT   = 0.6;
const float PO_TOP_BITS   = 4.0;   // cycle (FX 211): bits on the 1 at full amount (16 levels)
const float PO_STEP_BITS  = 1.0;   // … bits lost per beat (16, 8, 4, 2)
const float PO_CONTOUR    = 0.7;   // … ink lines where the level changes
const float PO_MIDS_HUE   = 0.6;   // … mids roll the posterized palette (rad)
const float PO_FLICKER_HZ = 24.0;  // hats (FX 234): re-roll rate of the level count
const float PO_FLICKER    = 0.8;   // … how wide the level count jumps
const float PO_SKEW       = 0.5;   // … R/G/B get different level counts
const float PO_HIGHS_ENGAGE = 1.0; // … hats alone posterize (× highsAmt)
vec3 poSteps(vec3 kv, float k, float cyc) {
  if (cyc > 0.5) {
    float beat = mod(floor(BEAT), 4.0);
    return max(exp2(mix(vec3(8.0), vec3(PO_TOP_BITS - PO_STEP_BITS * beat), kv)), vec3(2.0)) - 1.0;
  }
  vec3 s = mix(vec3(PO_LEVELS_MAX), vec3(PO_LEVELS_MIN), sqrt(kv)) - 1.0;
  return max(vec3(1.0), floor(s * (1.0 - PO_KICK_CRUSH * k) + 0.5));
}
vec3 posterLook(vec2 uv, vec3 cam, float amt, float k, float m) {
  float hi = PO_HIGHS_ENGAGE * highsAmt * highsNow();
  float a = clamp(amt + hi, 0.0, 1.0);
  float cyc = step(0.5, cycle);
  float t = floor(TIME * PO_FLICKER_HZ);
  float hop = PO_FLICKER * hi;
  vec3 r = vec3(h11(t), h11(t + 3.1), h11(t + 7.7));
  vec3 kv = clamp(vec3(a) + mix(vec3(r.x), r, PO_SKEW) * hop - hop * 0.3, 0.0, 1.0);
  vec3 steps = poSteps(kv, k, cyc);
  vec2 dOff = floor(vec2(h11(t + 11.0), h11(t + 13.0)) * 4.0) * step(0.05, hi);
  float d = (bayer4((isf_FragNormCoord * RENDERSIZE) + dOff) - 0.5) * (mix(PO_DITHER, 0.6, cyc) + 0.55 * hop);
  vec3 q = floor(clamp(cam, 0.0, 1.0) * steps + 0.5 + d) / steps;
  q = mix(vec3(lumaOf(q)), q, PO_SATURATE + PO_MIDS_SAT * m);
  q = (q - 0.5) * (1.0 + PO_KICK_POP * k) + 0.5;
  if (cyc > 0.5) {
    // contours: the quantised luma one pixel right / up (no dither)
    vec2 tx = 1.5 / RENDERSIZE;
    float l0 = lumaOf(floor(cam * steps + 0.5) / steps);
    float lx = lumaOf(floor(cam3(uv + vec2(tx.x, 0.0)) * steps + 0.5) / steps);
    float ly = lumaOf(floor(cam3(uv + vec2(0.0, tx.y)) * steps + 0.5) / steps);
    float L = (steps.r + steps.g + steps.b) / 3.0 + 1.0;
    float edge = step(0.001, abs(l0 - lx) + abs(l0 - ly)) * smoothstep(64.0, 4.0, L);
    q = roll(q, PO_MIDS_HUE * m * a);
    q *= 1.0 - PO_CONTOUR * edge;
  }
  return mix(cam, clamp(q, 0.0, 1.0), smoothstep(0.0, PO_FADE_IN, a));
}

// mode 2 · duotone (FX 40) ──────────────────────────────────────────────
const vec3  DUO_DARK  = vec3(0.10, 0.04, 0.32);  // shadows — deep indigo
const vec3  DUO_LIGHT = vec3(1.00, 0.45, 0.38);  // highlights — hot coral
const vec3  DUO_PAPER = vec3(1.00, 0.93, 0.80);  // the very brightest tones
const float DUO_CONTRAST = 1.35;
const float DUO_PIVOT = 0.45;
const float DUO_KICK_BLOW = 0.25;     // kick: pivot drops → highlights flood to paper
const float DUO_KICK_CONTRAST = 0.6;
const vec3  DUO_MIDS_INK = vec3(1.00, 0.72, 0.20);  // mids warm the highlight ink to gold
const float DUO_MIDS_WARM = 0.7;
vec3 duoLook(vec3 cam, float amt, float k, float m) {
  float l = clamp((lumaOf(cam) - (DUO_PIVOT - DUO_KICK_BLOW * k)) * DUO_CONTRAST * (1.0 + DUO_KICK_CONTRAST * k) + 0.5, 0.0, 1.0);
  vec3 light = mix(DUO_LIGHT, DUO_MIDS_INK, DUO_MIDS_WARM * m);
  vec3 duo = mix(DUO_DARK, light, smoothstep(0.0, 0.8, l));
  duo = mix(duo, DUO_PAPER, smoothstep(0.8, 1.0, l));
  return mix(cam, duo, amt);
}

// mode 3 · screen print (FX 229) ────────────────────────────────────────
const vec3  PR_INK_A = vec3(0.95, 0.25, 0.35);   // figure ink (warm)…
const vec3  PR_INK_B = vec3(0.05, 0.55, 0.62);   // …room ink
const vec3  PR_PAPER = vec3(0.97, 0.94, 0.86);
const float PR_CONTRAST = 1.4;
const vec2  PR_MISREG = vec2(0.004, -0.003);     // figure screen misregistration, uv
const float PR_GRAIN = 0.06;
const float PR_MIDS_HUE = 0.12;                  // mids drift both inks (turns)
vec3 screenLayer(vec3 src, vec3 ink, float grain) {
  float d = clamp((1.0 - lumaOf(src) - 0.5) * PR_CONTRAST + 0.5 + grain, 0.0, 1.0);
  return mix(vec3(1.0), ink / max(max(PR_PAPER.r, PR_PAPER.g), PR_PAPER.b), d);
}
vec3 printLook(vec2 uv, vec3 cam, float amt, float m) {
  float sw = clamp(stA().g, 0.0, 1.0);
  float a = TAU * PR_MIDS_HUE * m;
  vec3 inkFig = roll(mix(PR_INK_A, PR_INK_B, sw), a);
  vec3 inkRoom = roll(mix(PR_INK_B, PR_INK_A, sw), a);
  float g = (h21(floor((isf_FragNormCoord * RENDERSIZE))) - 0.5) * PR_GRAIN;
  vec2 fuv = clamp(uv - PR_MISREG, 0.0, 1.0);
  float figM = figureAt(fuv);
  float roomM = 1.0 - figureAt(uv);
  vec3 p = PR_PAPER * (1.0 + g * 0.5);
  p *= mix(vec3(1.0), screenLayer(cam, inkRoom, g), roomM);
  p *= mix(vec3(1.0), screenLayer(cam3(fuv), inkFig, -g), figM);
  return mix(cam, p, amt);
}

// mode 4 · hue rotate (FX 41, 283) ──────────────────────────────────────
const float HUE_TURNS  = 1.0;      // turns at full amount (1 = all the way round)
const float HUE_KICK_ENGAGE = 0.35; // kick alone swings the hue this many turns
const float HUE_MIDS_ENGAGE = 0.15; // mids: standing hue offset, turns
const float HUE_KICK_JUMP = 0.1;   // with pressure up: extra knock per kick, × amount
const float HUE_MIDS_DRIFT = 0.08; // with pressure up: mids push further, × amount
const float HUE_SAT   = 0.35;      // extra saturation as it turns
vec3 hueLook(vec3 cam, float k, float m) {
  float a = clamp(amount + HUE_KICK_ENGAGE * kickAmt * k + HUE_MIDS_ENGAGE * midsAmt * m, 0.0, 1.0);
  float turns = HUE_TURNS * a + amount * (HUE_KICK_JUMP * k + HUE_MIDS_DRIFT * m);
  vec3 c = hueRotate(cam, turns);
  float l = lumaOf(c);
  return max(mix(vec3(l), c, 1.0 + HUE_SAT * sin(3.14159 * a)), 0.0);
}

// mode 5 · tint (FX 102) ────────────────────────────────────────────────
const float TINT_DRIFT = 0.04;     // wheel turns/s while `hue` is 0
const float TINT_KEEP_LUMA = 0.85;
const float TINT_SAT = 1.0;
const float TINT_KICK_FLASH = 0.25;
const float TINT_MIDS_DRIFT = 0.08;
vec3 tintLook(vec3 cam, float amt, float k, float m) {
  float h = hue > 0.001 ? hue : TIME * TINT_DRIFT;
  h += TINT_MIDS_DRIFT * m * sin(TIME * 1.3);
  vec3 hc = hueCol(h);
  vec3 tint = mix(vec3(1.0), hc / max(lumaOf(hc), 0.15), TINT_SAT);
  vec3 c = mix(vec3(0.55), vec3(lumaOf(cam)), TINT_KEEP_LUMA) * tint * (1.0 + TINT_KICK_FLASH * k);
  return clamp(mix(cam, c, amt), 0.0, 1.0);
}

// mode 6 · complement split (FX 213) ────────────────────────────────────
const float CS_MIDS_GAIN = 1.1;    // mids → push (the star)
const float CS_WARM_HUE  = 0.45;   // rad — figure hue (amber); room gets +π
const float CS_HUE_SWING = 0.35;   // rad the pair rotates as mids climb
const float CS_SAT_BASE  = 0.18;
const float CS_SAT_PRESS = 0.32;
const float CS_PRESS_PUSH = 0.5;
const float CS_KICK_SAT  = 0.25;
const float CS_KICK_ENGAGE = 0.7;  // kick alone slams the push in
const float CS_SEAM      = 0.5;
vec3 yiqTint(float luma, float h, float sat) {
  float i = sat * cos(h); float q = sat * sin(h);
  return vec3(luma + 0.956 * i + 0.621 * q, luma - 0.272 * i - 0.647 * q, luma - 1.106 * i + 1.703 * q);
}
vec3 splitLook(vec2 uv, vec3 cam, float k, float m) {
  float push = clamp(CS_MIDS_GAIN * midsAmt * m + CS_PRESS_PUSH * amount + CS_KICK_ENGAGE * kickAmt * k, 0.0, 1.0);
  float sat = CS_SAT_BASE + CS_SAT_PRESS * amount + CS_KICK_SAT * k;
  float h = CS_WARM_HUE + CS_HUE_SWING * m;
  float l = lumaOf(cam);
  float s = sat * (0.3 + 0.7 * sin(3.14159 * clamp(l, 0.0, 1.0)));
  float f = figureAt(uv);
  vec3 roomC = mix(cam, clamp(yiqTint(l, h + 3.14159265, s), 0.0, 1.0), push);
  vec3 figC = mix(cam, clamp(yiqTint(l, h, s), 0.0, 1.0), push);
  vec3 col = mix(roomC, figC, f);
  vec2 p = 2.0 / RENDERSIZE;
  float e = abs(f - figureAt(uv + vec2(p.x, 0.0))) + abs(f - figureAt(uv + vec2(0.0, p.y)));
  return mix(col, vec3(lumaOf(col) * 0.5 + 0.5), clamp(e, 0.0, 1.0) * CS_SEAM * push);
}

// mode 7 · solarize (FX 42, 258) ────────────────────────────────────────
const float SO_SOFT = 0.04;
const float SO_CONTRAST = 1.15;
const float SO_KICK_DROP = 0.2;
const float SO_MIDS_CONTRAST = 0.5;
const float SO_MIDS_ROLL = 1.2;    // FX 258 (where = 1): mids roll the negative's hue
vec3 solarLook(vec3 cam, float fx, float k, float m, float onFig) {
  float thr = 1.0 - fx - SO_KICK_DROP * k * smoothstep(0.0, 0.1, fx);
  vec3 kk = smoothstep(vec3(thr - SO_SOFT), vec3(thr + SO_SOFT), cam) * smoothstep(0.0, 0.05, fx);
  vec3 neg = roll(1.0 - cam, SO_MIDS_ROLL * m * onFig);
  vec3 s = mix(cam, neg, kk);
  return mix(cam, clamp((s - 0.5) * (SO_CONTRAST + SO_MIDS_CONTRAST * m) + 0.5, 0.0, 1.0), smoothstep(0.0, 0.2, fx));
}

// mode 8 · thermal (FX 19 on the figure / whole, FX 134 on the room) ────
const float TH_W_LUMA = 0.65;      // FX 19: heat from brightness…
const float TH_W_NEAR = 0.35;      // …and from nearness
const float TH_CONTRAST = 1.4;
const float TH_SHIMMER = 0.03;
const float TH_KICK_HEAT = 0.35;
const float TH_MIDS_SHIMMER = 0.08;
const float THR_SOFT = 0.004;      // FX 134: thermal blur (uv)
const float THR_CONTRAST = 1.3;
const float THR_SHIMMER = 0.03;
const float THR_SCANLINES = 0.06;
const float THR_KICK_HEAT = 0.25;
vec3 thermalLook(vec2 uv, vec3 cam, float amt, float k, float m, float w) {
  if (w > 1.5) {
    vec2 e = vec2(THR_SOFT, 0.0);
    float l = (lumaOf(cam) * 2.0 + lumaOf(cam3(uv + e)) + lumaOf(cam3(uv - e))
             + lumaOf(cam3(uv + e.yx)) + lumaOf(cam3(uv - e.yx))) / 6.0;
    l = (l - 0.5) * THR_CONTRAST + 0.5;
    l += THR_SHIMMER * (h21(floor((isf_FragNormCoord * RENDERSIZE) / 2.0) + floor(TIME * 30.0)) - 0.5);
    l += THR_KICK_HEAT * k;
    vec3 th = ironbow6(l) * (1.0 - THR_SCANLINES * step(0.5, fract((isf_FragNormCoord.y * RENDERSIZE.y) / 3.0)));
    return mix(cam, th, amt);
  }
  float heat = TH_W_LUMA * lumaOf(cam) + TH_W_NEAR * depthAt(uv);
  heat = (heat - 0.5) * TH_CONTRAST + 0.5 + TH_KICK_HEAT * k;
  heat += (TH_SHIMMER + TH_MIDS_SHIMMER * m) * (h21(floor(uv * 160.0) + floor(TIME * 12.0)) - 0.5);
  return mix(cam, ironbow5(heat), amt);
}

// mode 9 · colour cycle (FX 146) ────────────────────────────────────────
const float CC_MIN_BANDS = 1.0;
const float CC_MAX_BANDS = 4.0;
const float CC_SPEED = 0.35;
const float CC_KEEP_SHADE = 0.55;
const float CC_SAT = 1.0;
const float CC_KICK_JOLT = 0.25;
const float CC_MIDS_SPEED = 0.6;
vec3 cycleLook(vec3 cam, float amt, float k, float m) {
  float l = lumaOf(cam);
  float ph = l * mix(CC_MIN_BANDS, CC_MAX_BANDS, amt) - TIME * (CC_SPEED + CC_MIDS_SPEED * m) - CC_KICK_JOLT * k;
  vec3 hc = hueCol(ph);
  vec3 rb = mix(vec3(lumaOf(hc)), hc, CC_SAT);
  return mix(cam, rb * mix(1.0, 0.25 + 1.1 * l, CC_KEEP_SHADE), amt);
}

// mode 10 · bleach bypass (FX 136) ──────────────────────────────────────
const float BB_DESAT = 0.7;
const float BB_CONTRAST = 1.6;
const float BB_CRUSH = 0.08;
const vec3  BB_SILVER = vec3(0.92, 0.97, 1.03);
const float BB_GRAIN = 0.06;
const float BB_KICK_FLASH = 0.15;
const float BB_MIDS_GRAIN = 1.5;
vec3 bleachLook(vec3 cam, float amt, float k, float m) {
  float l = lumaOf(cam);
  vec3 b = vec3(l);
  vec3 c = mix(2.0 * cam * b, 1.0 - 2.0 * (1.0 - cam) * (1.0 - b), step(0.5, cam));  // silver overlay
  c = clamp(mix(c, vec3(lumaOf(c)), BB_DESAT), 0.0, 1.0);
  c = mix(c, c * c * (3.0 - 2.0 * c), clamp(BB_CONTRAST - 1.0, 0.0, 1.0));
  c = (c - 0.5) * max(BB_CONTRAST - 0.6, 1.0) + 0.5;
  c = (c - BB_CRUSH) / (1.0 - BB_CRUSH);
  c *= mix(vec3(1.0), BB_SILVER, smoothstep(0.4, 1.0, lumaOf(c)));
  c *= 1.0 + BB_KICK_FLASH * k * smoothstep(0.5, 1.0, l);
  c += (h21(floor((isf_FragNormCoord * RENDERSIZE)) + fract(TIME * 24.0) * 97.0) - 0.5) * BB_GRAIN * (1.0 + BB_MIDS_GRAIN * m);
  return mix(cam, clamp(c, 0.0, 1.0), amt);
}

// mode 11 · teal-orange (FX 137) ────────────────────────────────────────
const vec3  TO_TEAL = vec3(0.0, 0.42, 0.48);
const vec3  TO_ORANGE = vec3(1.0, 0.58, 0.25);
const float TO_SPLIT = 0.45;
const float TO_SOFT = 0.35;
const float TO_TINT = 0.55;
const float TO_CONTRAST = 0.35;
const float TO_SAT = 1.25;
const float TO_VIGNETTE = 0.35;
const float TO_KICK_PUNCH = 0.5;
const float TO_MIDS_SPLIT = 0.15;
vec3 tealLook(vec2 uv, vec3 cam, float amt, float k, float m) {
  float con = TO_CONTRAST * (1.0 + TO_KICK_PUNCH * k);
  vec3 s = clamp(cam + con * (cam - 0.5) * (1.0 - abs(2.0 * cam - 1.0)), 0.0, 1.0);
  float ls = lumaOf(s);
  float sp = TO_SPLIT - TO_MIDS_SPLIT * m;
  vec3 tint = mix(TO_TEAL, TO_ORANGE, smoothstep(sp - TO_SOFT, sp + TO_SOFT, ls));
  vec3 g = mix(s, tint * (ls / max(lumaOf(tint), 1e-3)), TO_TINT);
  g = mix(vec3(lumaOf(g)), g, TO_SAT);
  g *= 1.0 - TO_VIGNETTE * smoothstep(0.35, 0.85, length(uv - 0.5));
  return mix(cam, clamp(g, 0.0, 1.0), amt);
}

// mode 12 · sepia film (FX 135) ─────────────────────────────────────────
const vec3  SE_DARK = vec3(0.16, 0.10, 0.05);
const vec3  SE_LIGHT = vec3(1.00, 0.88, 0.66);
const float SE_FADE = 0.25;
const float SE_GRAIN = 0.10;
const float SE_FLICKER = 0.08;
const float SE_SCRATCHES = 0.5;
const float SE_VIGNETTE = 0.9;
const float SE_FPS = 18.0;
const float SE_MIDS_FLICKER = 1.5;
vec3 sepiaLook(vec2 uv, vec3 cam, float amt, float m) {
  float fr = floor(TIME * SE_FPS);
  float noisy = 1.0 + SE_MIDS_FLICKER * m;
  float l = lumaOf(cam);
  l = mix(l, SE_FADE + (1.0 - SE_FADE) * l, amt);
  l *= 1.0 + SE_FLICKER * noisy * (h21(vec2(fr, 3.1)) - 0.5) * 2.0;
  l += SE_GRAIN * noisy * (h21(floor(uv * RENDERSIZE) + fr * 13.7) - 0.5);
  for (int i = 0; i < 3; i++) {
    float on = step(1.0 - SE_SCRATCHES * 0.6, h21(vec2(fr, float(i) * 5.3)));
    float x = h21(vec2(fr * 0.37, float(i) + 11.0));
    l = mix(l, 0.95, on * (1.0 - smoothstep(0.0, 1.5 / RENDERSIZE.x, abs(uv.x - x))));
  }
  vec3 sep = mix(SE_DARK, SE_LIGHT, clamp(l, 0.0, 1.0));
  sep *= 1.0 - SE_VIGNETTE * smoothstep(0.25, 0.75, length((uv - 0.5) * vec2(1.1, 1.0)));
  return mix(cam, sep, amt);
}

// mode 13 · sunset (FX 270) ─────────────────────────────────────────────
const vec2  SU_POS = vec2(-0.15, 0.62);   // just off the left edge, low
const vec3  SU_GOLD = vec3(1.25, 0.95, 0.55);
const vec3  SU_DUSK = vec3(0.55, 0.32, 0.6);
const float SU_SAT = 1.25;
const float SU_GLOW = 0.55;
const float SU_SHADOW_LEN = 0.45;
const float SU_SHADOW_DARK = 0.55;
const float SU_MIDS_ENGAGE = 0.85;  // slow mids alone bring the sunset in — the star
const float SU_KICK_ENGAGE = 0.2;
const float SU_KICK_FLARE = 0.6;
vec3 sunsetLook(vec2 uv, vec3 cam, float k) {
  float g = clamp(amount + SU_MIDS_ENGAGE * midsAmt * stB().g + SU_KICK_ENGAGE * kickAmt * k, 0.0, 1.0);
  float aspect = RENDERSIZE.x / max(RENDERSIZE.y, 1.0);
  vec2 toSun = normalize((SU_POS - uv) * vec2(aspect, 1.0)) / vec2(aspect, 1.0);
  float sh = 0.0;
  for (int i = 1; i <= 14; i++) {
    float t = float(i) / 14.0;
    sh = max(sh, figureAt(uv + toSun * t * SU_SHADOW_LEN * g) * (1.0 - t * 0.8));
  }
  sh *= 1.0 - figureAt(uv);
  float l = lumaOf(cam);
  vec3 c = mix(vec3(l), cam, SU_SAT) * mix(SU_DUSK, SU_GOLD, smoothstep(0.1, 0.7, l));
  c *= 1.0 - SU_SHADOW_DARK * sh;
  float sun = exp(-length((uv - SU_POS) * vec2(aspect, 1.0)) * 1.6);
  c += SU_GOLD * sun * SU_GLOW * (1.0 + SU_KICK_FLARE * k);
  return mix(cam, c, g);
}

// mode 14 · infrared (FX 88) ────────────────────────────────────────────
const vec3  IR_WEIGHTS = vec3(0.75, 0.35, -0.10);
const vec3  IR_TINT = vec3(1.0, 0.92, 0.95);
const float IR_GAMMA = 0.8;
const float IR_GLOW = 0.35;
const float IR_GRAIN = 0.06;
const float IR_SWAP_SOFT = 0.1;
const float IR_KICK_GLOW = 0.6;
const float IR_MIDS_GRAIN = 0.1;
vec3 irOf(vec3 c, float k, float m) {
  float l = pow(clamp(dot(c, IR_WEIGHTS), 0.0, 1.0), IR_GAMMA);
  l += (IR_GLOW + IR_KICK_GLOW * k) * smoothstep(0.4, 1.0, l);
  l += (IR_GRAIN + IR_MIDS_GRAIN * m) * (h21((isf_FragNormCoord * RENDERSIZE) + fract(TIME * 7.3) * 91.0) - 0.5);
  return IR_TINT * l;
}
vec3 irLook(vec2 uv, vec3 cam, float amt, float k, float m) {
  vec3 ir = irOf(cam, k, m);
  float a = smoothstep(0.0, 0.5 - IR_SWAP_SOFT, amt);              // room → IR
  float s = smoothstep(0.5 - IR_SWAP_SOFT, 0.5 + IR_SWAP_SOFT, amt); // the swap
  return mix(mix(cam, ir, a * (1.0 - s)), mix(cam, ir, s), figureAt(uv));
}

// mode 15 · sin city (FX 293) ───────────────────────────────────────────
const float SC_POP_SAT = 1.5;      // the figure's colour on the kick, saturated
const float SC_CRUSH = 0.08;       // hard black / white for the mono
vec3 sinCityLook(vec2 uv, vec3 cam, float amt, float k) {
  float l = lumaOf(cam);
  vec3 mono = vec3(smoothstep(SC_CRUSH, 1.0 - SC_CRUSH, l));
  float pop = smoothstep(0.0, 0.5, k) * figureAt(uv);
  vec3 popC = clamp(mix(vec3(l), cam, SC_POP_SAT), 0.0, 1.0);
  return mix(mix(cam, mono, amt), popC, pop * amt);
}

// mode 16 · silhouette (FX 15, 274) ─────────────────────────────────────
const vec3  SI_POSTER = vec3(1.0, 0.24, 0.43);  // #FF3D6E
const vec3  SI_SHADOW = vec3(0.35, 0.05, 0.25);
const float SI_SPLIT = 0.42;
const vec3  SI_RIM = vec3(1.0, 0.95, 0.85);
const float SI_RIM_PX = 3.0;
const float SI_KICK_RIM = 3.0;
const float SI_KICK_FLASH = 0.5;
const float SI_MIDS_HUE = 0.25;               // turns
vec3 strobeColour(float i) {                   // FX 274's four flat colours
  if (i < 0.5) return vec3(1.0, 0.24, 0.43);   // poster pink
  if (i < 1.5) return vec3(0.1, 0.85, 0.95);   // cyan
  if (i < 2.5) return vec3(1.0, 0.86, 0.1);    // yellow
  return vec3(0.45, 0.2, 1.0);                 // violet
}
vec3 silhouetteLook(vec2 uv, vec3 cam, float amt, float k, float m) {
  vec2 tx = 1.0 / RENDERSIZE;
  float l = 0.0;
  for (int j = -1; j <= 1; j++) {
    for (int i = -1; i <= 1; i++) { l += lumaOf(cam3(uv + vec2(float(i), float(j)) * tx * 3.0)); }
  }
  l /= 9.0;
  vec3 top = SI_POSTER; vec3 low = SI_SHADOW;
  if (cycle > 0.5) { top = strobeColour(stA().r); low = top * 0.35; }
  vec3 fill = mix(low, top, step(SI_SPLIT, l));
  fill = roll(fill, TAU * SI_MIDS_HUE * m);
  fill = mix(fill, vec3(1.0), SI_KICK_FLASH * k);
  float rimPx = SI_RIM_PX + SI_KICK_RIM * k;
  float f = figureAt(uv);
  float edge = (1.0 - smoothstep(rimPx - 1.0, rimPx, outlinePx(f))) * step(0.5, f);
  fill = mix(fill, SI_RIM, edge);
  return mix(cam, fill, amt);
}

// mode 17 · gradient fill (FX 263) ──────────────────────────────────────
const float GF_STEPS = 3.0;
const float GF_TONE = 0.45;
const float GF_SPAN = 1.2;
const float GF_INK = 0.85;
const float GF_INK_PX = 2.0;
const float GF_MIDS_STRETCH = 0.8;
vec3 pal(float t) { return 0.5 + 0.5 * cos(TAU * (t + vec3(0.0, 0.33, 0.67))); }
vec3 gradientLook(vec2 uv, vec3 cam, float amt, float m) {
  float slide = stB().r;
  float ang = 1.2 + 0.4 * sin(slide * 0.7);
  float t = dot(uv - 0.5, vec2(cos(ang), sin(ang))) / (GF_SPAN / (1.0 + GF_MIDS_STRETCH * m)) + slide * 0.25;
  vec3 fill = mix(pal(t), pal(t + 0.35), 0.5 + 0.5 * sin(t * 3.14159));
  float l = floor(clamp(lumaOf(cam), 0.0, 0.999) * GF_STEPS) / (GF_STEPS - 1.0);
  fill *= 1.0 - GF_TONE + GF_TONE * l * 1.3;
  float f = figureAt(uv);
  fill *= 1.0 - GF_INK * (1.0 - smoothstep(GF_INK_PX - 1.0, GF_INK_PX, outlinePx(f))) * step(0.5, f);
  return mix(cam, fill, amt);
}

// mode 18 · bar grade (FX 185) ──────────────────────────────────────────
const float BG_XFADE = 0.25;       // beat fraction for the crossfade
const float BG_DOWNBEAT = 0.18;    // exposure bump on beat 1
const float BG_CONTRAST = 1.25;
vec3 scurve(vec3 c) { return clamp(0.5 + (c - 0.5) * BG_CONTRAST, 0.0, 1.0); }
vec3 barGrade(float i, vec3 c) {
  float l = lumaOf(c);
  if (i < 0.5) {                   // teal / orange film
    vec3 g = mix(vec3(0.05, 0.32, 0.38), vec3(1.05, 0.68, 0.38), smoothstep(0.1, 0.9, l)) * (0.6 + 0.8 * l);
    return scurve(mix(c, g, 0.6));
  }
  if (i < 1.5) return scurve(mix(vec3(0.02, 0.08, 0.28), vec3(0.85, 0.95, 1.0), pow(max(l, 0.0), 0.85)));  // cyanotype
  if (i < 2.5) return scurve(mix(vec3(0.35, 0.02, 0.32), vec3(0.8, 1.0, 0.35), smoothstep(0.08, 0.85, l))); // magenta / lime
  return scurve(mix(c, vec3(l), 0.7) * vec3(0.95, 1.0, 1.05) * 1.1);                                       // bleach silver
}
vec3 barLook(vec3 cam, float amt) {
  float ph = fract(BEAT);
  float cur = mod(floor(BEAT), 4.0);
  vec3 g = mix(barGrade(mod(cur + 3.0, 4.0), cam), barGrade(cur, cam), smoothstep(0.0, BG_XFADE, ph));
  g *= 1.0 + BG_DOWNBEAT * (1.0 - ph) * step(cur, 0.5);
  return mix(cam, g, amt);
}

// mode 19 · palette swap (FX 248) ───────────────────────────────────────
const float PS_SNAP = 0.08;        // beat fraction for the swap to cross over
const float PS_CAST_GAIN = 1.6;
const float PS_KICK_ENGAGE = 0.8;  // on plain beats a kick still flashes the trade across
vec3 trade(vec3 c, vec3 own, vec3 other) {
  vec3 o = max(own, vec3(0.03)); vec3 t = max(other, vec3(0.03));
  vec3 ratio = pow((t / lumaOf(t)) / (o / lumaOf(o)), vec3(PS_CAST_GAIN));
  return clamp(c * ratio, 0.0, 1.2);
}
vec3 swapLook(vec2 uv, vec3 cam, float amt, float k) {
  vec3 ra = roomAvg(); vec3 fa = figAvg();
  float odd = mod(floor(BEAT), 2.0);
  float stt = mix(1.0 - odd, odd, smoothstep(0.0, PS_SNAP, fract(BEAT)));
  float sw = clamp(stt * amt + (1.0 - stt) * PS_KICK_ENGAGE * kickAmt * k, 0.0, 1.0);
  vec3 room = mix(cam, trade(cam, ra, fa), sw);
  vec3 fig = mix(cam, trade(cam, fa, ra), sw);
  return mix(room, fig, figureAt(uv));
}

// mode 20 · bands to RGB (FX 259) ───────────────────────────────────────
const float BR_DEPTH = 0.9;
const float BR_AUDIO_ENGAGE = 1.0;
const float BR_KICK_PUNCH = 0.35;
const float BR_METER = 0.8;
vec3 bandsLook(vec3 cam, float k) {
  vec3 band = vec3(lowsNow(), midsNow(), highsNow());
  float w = clamp(amount + BR_AUDIO_ENGAGE * midsAmt * max(band.r, max(band.g, band.b)), 0.0, 1.0);
  vec3 g = 1.0 + BR_DEPTH * w * (band - 0.5) * 2.0;
  g.r += BR_KICK_PUNCH * kickAmt * k * w;
  vec3 col = cam * max(g, vec3(0.0));
  vec2 p = (isf_FragNormCoord * RENDERSIZE) - vec2(12.0, 12.0);   // three meters, bottom left
  if (p.x >= 0.0 && p.x < 30.0 && p.y >= 0.0 && p.y < 60.0) {
    float i = floor(p.x / 10.0);
    float b = i < 0.5 ? band.r : (i < 1.5 ? band.g : band.b);
    vec3 mc = i < 0.5 ? vec3(1.0, 0.2, 0.2) : (i < 1.5 ? vec3(0.2, 1.0, 0.3) : vec3(0.3, 0.5, 1.0));
    float on = step(p.y / 60.0, b) * step(mod(p.x, 10.0), 6.0);
    col = mix(col, mc, on * BR_METER * w);
  }
  return col;
}

// ════ engage: each FX's KICK_ENGAGE / MIDS_ENGAGE (× kickAmt / midsAmt) ══
// modes 4, 6, 13, 20 carry their own inside the look.
vec2 engageOf(float md, float w, float cyc, float hats) {
  if (md < 0.5) return vec2(0.8, 0.3);                       // FX 6
  if (md < 1.5) {
    if (hats > 0.0) return vec2(0.55, 0.15);                 // FX 234
    return cyc > 0.5 ? vec2(0.9, 0.2) : vec2(0.8, 0.25);     // FX 211 · FX 8
  }
  if (md < 2.5) return vec2(0.8, 0.25);                      // FX 40
  if (md < 3.5) return vec2(0.7, 0.2);                       // FX 229
  if (md < 5.5) return vec2(0.75, 0.25);                     // FX 102
  if (md < 7.5) return vec2(0.7, 0.2);                       // FX 42 / 258
  if (md < 8.5) return w > 1.5 ? vec2(0.85, 0.25) : vec2(0.9, 0.25); // FX 134 · FX 19
  if (md < 9.5) return vec2(0.8, 0.25);                      // FX 146
  if (md < 10.5) return vec2(0.85, 0.25);                    // FX 136
  if (md < 11.5) return vec2(0.8, 0.3);                      // FX 137
  if (md < 12.5) return vec2(0.85, 0.25);                    // FX 135
  if (md < 14.5) return vec2(0.45, 0.15);                    // FX 88
  if (md < 15.5) return vec2(0.85, 0.2);                     // FX 293 (never built: chosen)
  if (md < 16.5) return vec2(0.85, 0.25);                    // FX 15 / 274
  if (md < 17.5) return vec2(0.8, 0.5);                      // FX 263
  if (md < 18.5) return vec2(0.8, 0.25);                     // FX 185
  return vec2(0.8, 0.2);                                     // FX 248
}

// ════ main ═════════════════════════════════════════════════════════════
void main() {
  vec2 uv = isf_FragNormCoord;
  if (PASSINDEX == 0.0) { gl_FragColor = sigPass(); return; }
  if (PASSINDEX == 1.0) { gl_FragColor = depthPass(uv); return; }
  if (PASSINDEX == 2.0) { gl_FragColor = statePass(); return; }

  vec3 cam = cam3(uv);
  float k = kickNow();
  float m = midsNow();
  float md = floor(mode + 0.5);
  float w = floor(where + 0.5);
  vec2 eng = engageOf(md, w, cycle, highsAmt);
  float amt = clamp(amount + eng.x * kickAmt * k + eng.y * midsAmt * m, 0.0, 1.0);
  float onFig = step(0.5, w) * step(w, 1.5);
  float region = regionAt(uv);

  vec3 look = cam;
  if (md < 0.5) look = celLook(uv, cam, amt, k, m);
  else if (md < 1.5) look = posterLook(uv, cam, amt, k, m);
  else if (md < 2.5) look = duoLook(cam, amt, k, m);
  else if (md < 3.5) { look = printLook(uv, cam, amt, m); region = 1.0; }
  else if (md < 4.5) look = hueLook(cam, k, m);
  else if (md < 5.5) look = tintLook(cam, amt, k, m);
  else if (md < 6.5) { look = splitLook(uv, cam, k, m); region = 1.0; }
  else if (md < 7.5) look = solarLook(cam, amt, k, m, onFig);
  else if (md < 8.5) look = thermalLook(uv, cam, amt, k, m, w);
  else if (md < 9.5) look = cycleLook(cam, amt, k, m);
  else if (md < 10.5) look = bleachLook(cam, amt, k, m);
  else if (md < 11.5) look = tealLook(uv, cam, amt, k, m);
  else if (md < 12.5) look = sepiaLook(uv, cam, amt, m);
  else if (md < 13.5) look = sunsetLook(uv, cam, k);
  else if (md < 14.5) { look = irLook(uv, cam, amt, k, m); region = 1.0; }
  else if (md < 15.5) { look = sinCityLook(uv, cam, amt, k); region = 1.0; }
  else if (md < 16.5) look = silhouetteLook(uv, cam, amt, k, m);
  else if (md < 17.5) look = gradientLook(uv, cam, amt, m);
  else if (md < 18.5) look = barLook(cam, amt);
  else if (md < 19.5) { look = swapLook(uv, cam, amt, k); region = 1.0; }
  else look = bandsLook(cam, k);

  vec3 col = mix(cam, look, region);
  // cel on the figure: the silhouette ink sits on both sides of the cut
  if (md < 0.5 && onFig > 0.5) col *= 1.0 - CEL_INK * amt * celRim(uv, k);
  gl_FragColor = vec4(col, 1.0);
}
