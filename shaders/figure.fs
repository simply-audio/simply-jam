/*{
  "DESCRIPTION": "audiodev figure: the figure as a graphic, cut by depth. Outline, neon tubes, aura, key glow, lightning, hat strobe rim, beat halo, shadow puppet, paper cutout, stencil, x-ray, double exposure, ghost, ink on paper, shadow ink, light composite, ambient wrap.",
  "CREDIT": "simply-creative FX library (FX 16 53 72 79 91 107 108 109 119 120 149 191 192 198 208 261 271), ported to tussen ISF",
  "CATEGORIES": ["figure", "depth", "sound", "beat", "modes", "pressure"],
  "INPUTS": [
    { "NAME": "inputImage", "TYPE": "image" },
    { "NAME": "depth", "TYPE": "depth" },
    { "NAME": "wave", "TYPE": "audio" },
    { "NAME": "spectrum", "TYPE": "audioFFT" },
    { "NAME": "amount", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 1.0, "DESCRIPTION": "pressure: how far the look is engaged (rim width for the hat rim, halo size for the beat halo)" },
    { "NAME": "react", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5, "DESCRIPTION": "how much the music moves it: 0 holds it still, 0.5 as tuned, 1 twice as much" },
    { "NAME": "mode", "TYPE": "long", "MIN": 0, "MAX": 16, "DEFAULT": 0,
      "VALUES": [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16],
      "LABELS": ["outline", "neon tubes", "glow aura", "key glow", "lightning", "hat strobe rim", "beat halo", "shadow puppet", "paper cutout", "stencil print", "x-ray", "double exposure", "ghost", "ink on paper", "shadow ink", "light composite", "ambient wrap"] },
    { "NAME": "where", "TYPE": "long", "MIN": 0, "MAX": 2, "DEFAULT": 0, "DESCRIPTION": "unused here: every mode has its own figure/room split" },
    { "NAME": "room", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0, "DESCRIPTION": "darken + desaturate the room (the far part), standing in for the old NDI plate" },
    { "NAME": "kickAmt", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.8 },
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
    { "TARGET": "maskBuf", "WIDTH": "$WIDTH / 4", "HEIGHT": "$HEIGHT / 4", "FLOAT": true },
    { "TARGET": "plate", "WIDTH": "$WIDTH / 2", "HEIGHT": "$HEIGHT / 2", "FLOAT": true, "PERSISTENT": true },
    {}
  ]
}*/

// mode 0  · outline         FX 72 Outline figure
// mode 1  · neon tubes      FX 120 Neon tube outline
// mode 2  · glow aura       FX 16 Glow aura
// mode 3  · key glow        FX 108 Chroma key glow
// mode 4  · lightning       FX 79 Lightning edge
// mode 5  · hat strobe rim  FX 198 Hat strobe outline (amount = rim width)
// mode 6  · beat halo       FX 261 Clock cut-out pulse (amount = halo size)
// mode 7  · shadow puppet   FX 119 Shadow puppet
// mode 8  · paper cutout    FX 149 Paper cutout
// mode 9  · stencil print   FX 91 Stencil print
// mode 10 · x-ray           FX 107 Figure x-ray
// mode 11 · double exposure FX 109 Double exposure
// mode 12 · ghost           FX 53 Ghost
// mode 13 · ink on paper    FX 271 Ink figure on paper
// mode 14 · shadow ink      FX 192 Shadow ink
// mode 15 · light composite FX 191 Light composite
// mode 16 · ambient wrap    FX 208 Ambient wrap
//
// The old FX put the webcam figure over a second picture (the NDI room). Here
// the room is the camera's own far part; where the room has to show THROUGH
// the figure (ghost, x-ray, double exposure, the composites) it comes from a
// room plate that learns the background wherever the figure is not.
//
// passes: sig · depthBuf · maskBuf (¼: the figure cut + three disc blurs of
// it, read back as a signed distance to the edge, so every line is a clean
// analytic contour) · plate (½: the room behind the figure) · the look.

// ════ shared: signals (copy verbatim) ══════════════════════════════════
// sig is 2×1 and kept from frame to frame:
//   pixel 0: r kick 0..1 (1 on an onset, decays) · g bass envelope · b fract(TIME) · a s since the last onset
// (16-bit float buffers: only fract(TIME) and ages are stored, never absolute TIME)
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
float kickAge() { return sigA().a; }
vec4 sigB() { return IMG_NORM_PIXEL(sig, vec2(0.75, 0.5)); }
// 1 while music plays, 0 in silence (the lows or the highs heard): a pulse
// on the beat clock alone is gated on it, so a look sits still in silence.
float musicOn() { return smoothstep(0.05, 0.2, max(sigB().r, sigB().b)); }
float depthReady() { return DEPTH_READY; }   // tussen's own: 1 while the depth model's map is bound
// react 0.5 is the music as the shader was tuned; 0 holds it still, 1 doubles it.
float reactK() { return 2.0 * react; }
// amount 0 is the picture as it came in (tussen skips the shader there); from
// 0.1 up the look is all there, its own amount meaning unchanged.
float wet() { return clamp(amount * 10.0, 0.0, 1.0); }
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
// distances are in frame heights (Chris's px were at 720 lines: 3 px = 3/720)
const float PLATE_RATE  = 0.02;   // how fast the room plate learns the background
const float MB_R1 = 0.015;        // mask blurs behind the edge distance (frame heights)
const float MB_R2 = 0.045;
const float MB_R3 = 0.12;
// outline (FX 72)
const float OL_LINE     = 3.0 / 720.0;    // outline half-width
const float OL_GLOW_R   = 14.0 / 720.0;   // glow reach
const float OL_GLOW     = 0.6;
const vec3  OL_LINE_C   = vec3(0.55, 1.0, 0.95);  // neon cyan-white
const float OL_KEEP     = 0.0;            // figure fill left at full (0.15 = ghost)
const float OL_KICK_THICK = 1.0;
const float OL_KICK_FLASH = 1.2;
const float OL_MIDS_HUE = 0.6;
const vec3  OL_MIDS_C   = vec3(1.0, 0.3, 0.85);   // hot magenta
// neon tubes (FX 120): Chris's iso-levels 0.22 / 0.78 of a 0.03 blur ≈ ±0.011 off the edge
const vec3  NT_TUBE_A   = vec3(1.0, 0.15, 0.6);   // outer tube (hot pink)
const vec3  NT_TUBE_B   = vec3(0.1, 0.9, 1.0);    // inner tube (cyan)
const float NT_OFFSET   = 0.011;  // each tube's distance from the edge
const float NT_TUBE_W   = 0.0037; // tube thickness
const float NT_GAIN     = 1.6;
const float NT_HALO     = 0.5;
const float NT_DARK     = 0.85;
const float NT_KICK_FLICKER = 0.5;
const float NT_MIDS_BREATH  = 0.35;
// glow aura (FX 16)
const float AU_MAX_R    = 0.07;
const float AU_GAIN     = 2.2;
const vec3  AU_IN       = vec3(1.0, 0.85, 0.5);
const vec3  AU_OUT      = vec3(1.0, 0.25, 0.6);
const float AU_RIM_IN   = 0.25;
const float AU_SHIMMER  = 0.15;
const float AU_KICK_R   = 0.8;
const float AU_KICK_GAIN = 1.2;
const vec3  AU_MIDS_C   = vec3(0.45, 0.3, 1.0);
const float AU_MIDS_SHIFT = 0.8;
// key glow (FX 108)
const vec3  KG_COL      = vec3(0.2, 1.0, 0.55);   // key green
const float KG_RADIUS   = 0.035;
const float KG_RIM      = 2.2;
const float KG_INNER    = 1.2;
const float KG_ROOM_DIM = 0.75;
const float KG_KICK_REACH = 0.6;
const float KG_MIDS_HUE = 0.25;
// lightning (FX 79)
const float LI_JAG      = 9.0 / 720.0;
const float LI_JAG_FREQ = 14.0;
const float LI_FLICKER_HZ = 24.0;
const float LI_COVERAGE = 0.55;
const vec3  LI_CORE     = vec3(0.95, 0.97, 1.0);
const vec3  LI_GLOW     = vec3(0.35, 0.55, 1.0);
const float LI_GLOW_W   = 8.0 / 720.0;
const float LI_GAIN     = 1.6;
const float LI_KICK_STRIKE = 1.0;
const float LI_MIDS_JAG = 0.8;
// hat strobe rim (FX 198)
const float HR_W_MIN    = 0.004;
const float HR_W_MAX    = 0.035;
const float HR_STROBE   = 1.6;
const float HR_SHIMMER  = 0.45;
const float HR_HOLD     = 0.25;
const float HR_WHITE_CORE = 0.5;
// beat halo (FX 261)
const float BH_R_MIN    = 0.004;
const float BH_R_MAX_LO = 0.018;
const float BH_R_MAX_HI = 0.055;
const float BH_DOWNBEAT = 1.5;
const float BH_CURVE    = 2.0;
const vec3  BH_BAND     = vec3(1.0, 0.97, 0.9);
const vec3  BH_HALO_C   = vec3(1.0, 0.45, 0.7);
const float BH_HALO     = 0.6;
// shadow puppet (FX 119)
const vec3  SP_SHADOW   = vec3(0.015, 0.01, 0.01);
const vec3  SP_WARM     = vec3(1.0, 0.62, 0.28);
const float SP_SCREEN_WARM = 0.75;
const float SP_HALO_R   = 0.05;
const float SP_HALO     = 0.9;
const float SP_VIGNETTE = 0.6;
const float SP_FLICKER  = 0.08;
const float SP_KICK_FLARE = 0.8;
const float SP_MIDS_FLICK = 0.15;
// paper cutout (FX 149)
const float PC_BORDER   = 0.012;
const vec3  PC_PAPER    = vec3(0.97, 0.96, 0.92);
const vec2  PC_SHADOW_OFF = vec2(0.012, -0.018);
const float PC_SHADOW   = 0.55;
const float PC_SHADOW_SOFT = 0.012;
const float PC_WOBBLE   = 0.01;
const float PC_WOBBLE_FPS = 8.0;
const float PC_POSTER   = 6.0;
const float PC_GRAIN    = 0.05;
const float PC_KICK_LIFT = 1.5;
const float PC_MIDS_WOBBLE = 1.0;
// stencil print (FX 91)
const vec3  ST_PAPER    = vec3(0.96, 0.92, 0.82);
const vec3  ST_INK_A    = vec3(0.10, 0.14, 0.38);
const vec3  ST_INK_B    = vec3(0.92, 0.20, 0.22);
const float ST_CUT_A    = 0.30;
const float ST_CUT_B    = 0.55;
const vec2  ST_MISREG_A = vec2(-0.012, 0.006);
const vec2  ST_MISREG_B = vec2(0.014, -0.008);
const float ST_GRAIN    = 0.08;
const float ST_KICK_JOLT = 1.5;
const float ST_MIDS_INK = 0.15;
// x-ray (FX 107)
const vec3  XR_TINT     = vec3(0.55, 0.85, 1.0);
const float XR_GAMMA    = 1.4;
const float XR_CAM_GHOST = 0.18;
const float XR_RIM_W    = 6.0 / 720.0;
const vec3  XR_RIM_C    = vec3(0.8, 0.95, 1.0);
const float XR_RIM_GAIN = 1.6;
const float XR_SCAN     = 0.06;
const float XR_MIDS_RIM = 1.0;
const float XR_KICK_PUNCH = 0.8;
// double exposure (FX 109)
const vec3  DE_PAPER    = vec3(0.96, 0.94, 0.90);
const float DE_CAM_SCREEN = 0.75;
const float DE_CAM_GAMMA = 1.6;
const float DE_ROOM_GHOST = 0.12;
const float DE_EDGE_SOFT = 4.0 / 720.0;
const float DE_ZOOM     = 0.10;
const float DE_KICK_BURN = 0.35;
const float DE_MIDS_DRIFT = 0.04;
// ghost (FX 53)
const float GH_MIN_OPACITY = 0.15;
const float GH_SCREEN   = 0.6;
const vec3  GH_TINT     = vec3(0.75, 0.9, 1.1);
const float GH_RIM      = 0.6;
const float GH_RIM_W    = 6.0 / 720.0;
const float GH_WOBBLE   = 0.004;
const float GH_KICK_FLASH = 1.2;
const float GH_MIDS_WOBBLE = 3.0;
// ink on paper (FX 271)
const vec3  IP_PAPER    = vec3(0.95, 0.92, 0.85);
const vec3  IP_INK      = vec3(0.05, 0.06, 0.1);
const float IP_WATERMARK = 0.12;
const float IP_INK_GAMMA = 1.6;
const float IP_INK_FLOOR = 0.18;
const float IP_EDGE_W   = 0.004;
const float IP_DROPS    = 9.0;
const float IP_DROP_SIZE = 0.028;
const float IP_SPLASH_R = 0.38;
const float IP_DRY      = 2.5;
// shadow ink (FX 192)
const vec3  SI_INK      = vec3(0.10, 0.08, 0.20);
const float SI_INK_BASE = 0.65;
const float SI_KICK_DENS = 0.6;
const float SI_TONE     = 0.55;
const float SI_BLEED    = 5.0 / 720.0;
const float SI_KICK_BLEED = 10.0 / 720.0;
const float SI_RAG      = 0.35;
const vec2  SI_SHADOW_OFF = vec2(0.018, -0.022);
const float SI_SHADOW   = 0.35;
// light composite (FX 191)
const float LC_GAIN     = 1.25;
const float LC_SCREEN   = 0.5;
const float LC_GLOW_R   = 0.09;
const float LC_GLOW     = 0.9;
const float LC_GLOW_IDLE = 0.3;
const float LC_KICK_FLASH = 0.5;
// ambient wrap (FX 208)
const float AW_RELIGHT  = 1.0;
const float AW_LOCAL    = 0.5;
const float AW_LOCAL_R  = 0.18;
const float AW_EXPOSURE = 0.6;
const float AW_WRAP     = 26.0 / 720.0;
const float AW_WRAP_GAIN = 1.1;
const float AW_MIDS_WRAP = 1.0;
const float AW_PRESS_WRAP = 0.35;
// ──────────────────────────────────────────────────────────────────

const float PI = 3.14159265;
float h11(float n) { return fract(sin(n * 91.345) * 47453.5453); }
float h21(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vnoise(vec2 p) {
  vec2 i = floor(p); vec2 t = fract(p); t = t * t * (3.0 - 2.0 * t);
  return mix(mix(h21(i), h21(i + vec2(1.0, 0.0)), t.x), mix(h21(i + vec2(0.0, 1.0)), h21(i + vec2(1.0, 1.0)), t.x), t.y);
}
vec3 hue(float h) { return clamp(abs(fract(h + vec3(0.0, 2.0, 1.0) / 3.0) * 6.0 - 3.0) - 1.0, 0.0, 1.0); }
float aspectOf() { return RENDERSIZE.x / max(RENDERSIZE.y, 1.0); }
float aaW() { return 1.0 / max(RENDERSIZE.y, 1.0); }      // one output pixel, frame heights
vec3 camAt(vec2 uv) { return IMG_NORM_PIXEL(inputImage, clamp(uv, 0.0, 1.0)).rgb; }
float engage() { return clamp(amount + kickAmt * kickNow() + midsAmt * midsNow(), 0.0, 1.0); }
float hatNow() { return smoothstep(0.55, 0.75, highsNow()); }   // no hat onset counter: the highs, hard-kneed
float hatSeed() { return floor(BEAT * 2.0); }                   // a new colour every 8th

// the room (Chris's NDI): the camera's far part, the plate where the figure stands
vec3 roomTone(vec3 c) { return mix(c, vec3(lumaOf(c)), 0.8 * room) * (1.0 - 0.75 * room); }
vec3 plateAt(vec2 uv) { return IMG_NORM_PIXEL(plate, clamp(uv, 0.0, 1.0)).rgb; }
vec3 ndiAt(vec2 uv) { return roomTone(mix(camAt(uv), plateAt(uv), figureAt(clamp(uv, 0.0, 1.0)))); }

// ── pass 2: the cut + three disc blurs (¼ size) ──
float discBlur(vec2 uv, float R, float n) {
  vec2 asp = vec2(1.0 / aspectOf(), 1.0);
  float s = 0.0;
  for (int i = 0; i < 40; i++) {
    float fi = float(i);
    if (fi >= n) break;
    float r = R * sqrt((fi + 0.5) / n);
    float a = fi * 2.39996;
    s += figureAt(uv + vec2(cos(a), sin(a)) * asp * r);
  }
  return s / n;
}
vec4 maskPass(vec2 uv) {
  return vec4(figureAt(uv), discBlur(uv, MB_R1, 12.0), discBlur(uv, MB_R2, 24.0), discBlur(uv, MB_R3, 40.0));
}
// ── pass 3: room plate (½ size) ──
vec4 platePass(vec2 uv) {
  vec4 prev = IMG_NORM_PIXEL(plate, uv);
  vec3 cam = camAt(uv);
  float fresh = 1.0 - step(1.0, FRAMEINDEX) * step(0.5, prev.a);
  vec3 p = mix(prev.rgb, cam, PLATE_RATE * (1.0 - figureAt(uv)));
  // alpha: 1 + kicks so far (mod 64), the splash seed for ink on paper (FX 271's u_splash[0])
  float count = mix(prev.a, 1.0, fresh);
  count = 1.0 + mod(count - 1.0 + (1.0 - step(0.001, kickAge())), 64.0);
  // first frame: the room where it shows, and where the figure stands the
  // room's colour from around it (so the plate never starts with the figure in it)
  float f0 = figureAt(uv);
  vec3 around = vec3(0.0); float aw = 0.0;
  for (int i = 0; i < 8; i++) {
    float a = float(i) * 0.785398;
    vec2 q = clamp(uv + vec2(cos(a) / aspectOf(), sin(a)) * 0.2, 0.0, 1.0);
    float w = 1.0 - figureAt(q) + 0.001;
    around += camAt(q) * w; aw += w;
  }
  vec3 start = mix(cam, around / aw, f0);
  return vec4(mix(p, start, fresh), count);
}

// ── the edge as a signed distance ──
// x: distance to the figure's edge in frame heights (+ outside, − inside), yz: outward normal (aspect space)
float inBand(float v) { return smoothstep(0.02, 0.12, v) * smoothstep(0.98, 0.88, v); }
vec4 maskAt(vec2 uv) { return IMG_NORM_PIXEL(maskBuf, clamp(uv, 0.0, 1.0)); }
vec3 edgeInfo(vec2 uv) {
  vec2 e = 1.0 / IMG_SIZE(maskBuf);
  vec4 c = maskAt(uv);
  vec4 gx = (maskAt(uv + vec2(e.x, 0.0)) - maskAt(uv - vec2(e.x, 0.0))) / (2.0 * e.x * aspectOf());
  vec4 gy = (maskAt(uv + vec2(0.0, e.y)) - maskAt(uv - vec2(0.0, e.y))) / (2.0 * e.y);
  vec4 gl = sqrt(gx * gx + gy * gy);
  vec4 d = clamp((0.5 - c) / max(gl, vec4(0.05)), -0.3, 0.3);
  float dist = mix(0.25, -0.25, step(0.5, c.a));
  dist = mix(dist, d.a, inBand(c.a));
  dist = mix(dist, d.b, inBand(c.b));
  dist = mix(dist, d.g, inBand(c.g));
  dist = mix(dist, d.r, inBand(c.r));
  vec2 n = -vec2(gx.b + gx.g, gy.b + gy.g);
  return vec3(dist, n / max(length(n), 1e-5));
}
float edgeDist(vec2 uv) { return edgeInfo(uv).x; }
float crisp(float dist) { return 1.0 - smoothstep(-aaW(), aaW(), dist); }       // the cut, anti-aliased
float lineAt(float d, float w) { return 1.0 - smoothstep(w - aaW(), w + aaW(), abs(d)); }
// share of a ring of radius R that crosses the edge, from a point d off it (Chris's ring tests)
float ringFrac(float d, float R) { return acos(clamp(d / max(R, 1e-5), -1.0, 1.0)) / PI; }
// share of a disc of radius R that is the figure, from a point d off the edge (+ outside)
float discCov(float d, float R) {
  float t = clamp(d / max(R, 1e-5), -1.0, 1.0);
  return (acos(t) - t * sqrt(1.0 - t * t)) / PI;
}

// ── the looks ──
vec3 lookOutline(vec2 uv, vec3 cam, float dist) {
  float k = kickNow(); float mi = midsNow(); float fx = engage();
  float ad = abs(dist);
  float line = lineAt(ad, OL_LINE * (1.0 + OL_KICK_THICK * k));
  float glow = ringFrac(ad, OL_GLOW_R) + 0.5 * ringFrac(ad, OL_GLOW_R * 0.5);
  vec3 lc = mix(OL_LINE_C, OL_MIDS_C, OL_MIDS_HUE * mi);
  vec3 c = mix(ndiAt(uv), cam, crisp(dist) * mix(1.0, OL_KEEP, fx));
  c += lc * OL_GLOW * (1.0 + OL_KICK_FLASH * k) * glow * fx;
  return mix(c, lc * 1.2, line * smoothstep(0.0, 0.35, fx));
}

vec3 lookNeon(vec2 uv, vec3 cam, float dist) {
  float amt = engage();
  float breath = 1.0 + NT_MIDS_BREATH * midsNow() * (0.5 + 0.5 * sin(TIME * 2.1));
  float ca = (dist - NT_OFFSET * breath) / NT_TUBE_W;
  float cb = (dist + NT_OFFSET * breath) / NT_TUBE_W;
  float ta = exp(-ca * ca * 4.0) + NT_HALO * exp(-ca * ca * 0.6);
  float tb = exp(-cb * cb * 4.0) + NT_HALO * exp(-cb * cb * 0.6);
  vec3 neon = (ta * NT_TUBE_A + tb * NT_TUBE_B) * NT_GAIN * (1.0 + NT_KICK_FLICKER * kickNow());
  vec3 base = mix(ndiAt(uv), cam, crisp(dist));
  return base * (1.0 - NT_DARK * amt) + amt * neon;
}

vec3 lookAura(vec2 uv, vec3 cam, float dist) {
  float k = kickNow(); float mi = midsNow(); float amt = engage();
  float R = AU_MAX_R * (0.3 + 0.7 * amt) * (1.0 + AU_KICK_R * k);
  float outer = discCov(dist + 0.15 * R, R);    // ring weights lean inward
  float inner = discCov(dist, R * 0.35);
  float breathe = 1.0 + AU_SHIMMER * sin(TIME * 2.3 + uv.y * 6.0);
  vec3 outC = mix(AU_OUT, AU_MIDS_C, AU_MIDS_SHIFT * mi);
  vec3 halo = AU_IN * inner * inner * 1.4 + outC * outer;
  float outside = mix(AU_RIM_IN, 1.0, 1.0 - crisp(dist));
  vec3 light = halo * outside * AU_GAIN * amt * breathe * (1.0 + AU_KICK_GAIN * k);
  return cam + (1.0 - cam) * (1.0 - exp(-light));
}

vec3 lookKeyGlow(vec2 uv, vec3 cam, float dist) {
  float amt = engage();
  float fig = crisp(dist);
  float r = KG_RADIUS * (0.35 + 0.65 * amt) * (1.0 + KG_KICK_REACH * kickNow());
  float cov = discCov(dist + 0.2 * r, r);       // 1/ring weights lean inward
  float outer = cov * (1.0 - fig);
  float inner = (1.0 - cov) * fig;
  vec3 col = mix(KG_COL, KG_COL.bgr, KG_MIDS_HUE * midsNow());
  vec3 c = mix(ndiAt(uv) * (1.0 - KG_ROOM_DIM * amt), cam, fig);
  return c + amt * col * (KG_RIM * outer * outer * 2.0 + KG_INNER * inner);
}

vec2 jag(vec2 uv, float seed) {
  vec2 p = uv * LI_JAG_FREQ + seed * 13.1;
  return vec2(vnoise(p) + 0.5 * vnoise(p * 2.7 + 5.0), vnoise(p + 31.7) + 0.5 * vnoise(p * 2.7 + 17.0)) / 1.5 - 0.5;
}
vec3 lookLightning(vec2 uv, vec3 cam) {
  float k = kickNow(); float mi = midsNow();
  float lvl = amount >= 0.5 ? 1.0 : clamp(kickAmt * 1.25 * k + midsAmt * 1.2 * mi, 0.0, 1.0);
  float tick = floor(TIME * LI_FLICKER_HZ);
  float kk = LI_KICK_STRIKE * k;
  float cover = mix(LI_COVERAGE, 1.0, clamp(kk * 1.5, 0.0, 1.0));
  float core = 0.0; float glow = 0.0;
  for (int i = 0; i < 2; i++) {
    float seed = tick + float(i) * 7.3;
    float on = step(1.0 - cover, vnoise(uv * 5.0 + seed * 3.7));
    vec2 q = uv + jag(uv, seed) * 2.0 * LI_JAG * (1.0 + LI_MIDS_JAG * mi) * vec2(1.0 / aspectOf(), 1.0);
    float ad = abs(edgeDist(q));
    core = max(core, (1.0 - smoothstep(0.0, 1.5 * aaW(), ad)) * on);
    glow = max(glow, (1.0 - smoothstep(0.0, LI_GLOW_W, ad)) * on);
  }
  float strobe = 0.75 + 0.25 * h21(vec2(tick, 1.0));
  vec3 light = (LI_CORE * core + LI_GLOW * glow * 0.6) * LI_GAIN * strobe * (1.0 + 1.5 * kk) * lvl;
  return cam + (1.0 - cam) * (1.0 - exp(-light));
}

vec3 lookHatRim(vec2 uv, vec3 cam, float dist) {
  float k = kickNow(); float hat = hatNow();
  float s = -dist;                               // depth inside the figure
  float w = mix(HR_W_MIN, HR_W_MAX, amount) * (1.0 + 0.8 * k);
  float out_ = 0.5 * (ringFrac(s, w) + ringFrac(s, 0.5 * w));
  float rim = smoothstep(0.0, 0.3, out_) * crisp(dist);
  float core = smoothstep(0.25, 0.6, out_);
  float lvl = max(HR_STROBE * hat, clamp(HR_SHIMMER * highsNow() + HR_HOLD * amount + kickAmt * 0.75 * k + midsAmt * 0.6 * midsNow(), 0.0, 1.0));
  vec3 c = hue(fract(hatSeed() * 0.618034));
  c = mix(c, vec3(1.0), clamp(HR_WHITE_CORE * core + 0.6 * k * (1.0 - hat), 0.0, 1.0));
  return cam + c * rim * lvl;
}

vec3 lookBeatHalo(vec2 uv, vec3 cam, float dist) {
  float amt = engage();
  float downbeat = mod(floor(BEAT), 4.0) < 0.5 ? BH_DOWNBEAT : 1.0;
  float pulse = pow(1.0 - PHASE, BH_CURVE) * downbeat * musicOn() * reactK();
  float r = BH_R_MIN + mix(BH_R_MAX_LO, BH_R_MAX_HI, amount) * pulse;
  float outside = 1.0 - crisp(dist);
  float band = (1.0 - smoothstep(r - aaW(), r + aaW(), dist)) * outside;
  float halo = (1.0 - smoothstep(r, r * 1.7 + 0.006, dist)) * outside * (1.0 - band);
  vec3 c = mix(cam, BH_BAND, band * amt);
  return c + BH_HALO_C * halo * BH_HALO * amt * (0.4 + 0.6 * pulse);
}

vec3 lookPuppet(vec2 uv, vec3 cam, float dist) {
  float amt = engage();
  float fig = crisp(dist);
  float halo = discCov(dist, SP_HALO_R) * (1.0 - fig);
  float t = TIME;
  float flick = 1.0 - (SP_FLICKER + SP_MIDS_FLICK * midsNow()) * (0.5 + 0.5 * sin(t * 23.0) * sin(t * 7.3 + 1.0));
  float lamp = 1.0 - SP_VIGNETTE * length(uv - vec2(0.5, 0.55));
  vec3 ndi = ndiAt(uv);
  vec3 scr = mix(ndi, SP_WARM * (0.25 + 0.95 * lumaOf(ndi)), SP_SCREEN_WARM) * lamp * flick;
  scr += SP_WARM * halo * SP_HALO * (1.0 + SP_KICK_FLARE * kickNow()) * flick;
  vec3 rm = mix(ndi, scr, amt);
  return mix(rm, mix(cam, SP_SHADOW, amt), fig);
}

vec3 lookPaper(vec2 uv, vec3 cam) {
  float k = kickNow(); float amt = engage();
  float st = floor(TIME * PC_WOBBLE_FPS);
  vec2 wob = (vec2(h21(vec2(st, 1.0)), h21(vec2(st, 2.0))) - 0.5) * 2.0 * PC_WOBBLE * amt * (1.0 + PC_MIDS_WOBBLE * midsNow());
  float lift = 1.0 + PC_KICK_LIFT * k;
  vec2 cu = uv - wob * vec2(1.0 / aspectOf(), 1.0) - vec2(0.0, 0.006 * PC_KICK_LIFT * k);
  float d = edgeDist(cu);
  float fig = crisp(d);
  float cut = crisp(d - PC_BORDER * amt);
  vec2 su = cu - PC_SHADOW_OFF * amt * lift * vec2(1.0 / aspectOf(), 1.0);
  float sh = discCov(edgeDist(su) - PC_BORDER * amt, PC_SHADOW_SOFT * 2.0) * PC_SHADOW * amt;
  vec3 rm = ndiAt(uv) * (1.0 - sh * (1.0 - cut));
  vec3 c2 = camAt(cu);
  c2 = mix(c2, floor(c2 * PC_POSTER + 0.5) / PC_POSTER, amt);
  float grain = (h21(floor(uv * RENDERSIZE * 0.5)) - 0.5) * PC_GRAIN * amt;
  vec3 c = mix(rm, PC_PAPER + grain, cut);
  c = mix(c, c2 + grain, fig);
  return mix(cam, c, smoothstep(0.0, 0.05, amt));
}

float stencilInk(vec2 uv, float cut) {
  float l = lumaOf(camAt(uv));
  return figureAt(clamp(uv, 0.0, 1.0)) * (1.0 - smoothstep(cut - 0.03, cut + 0.03, l));
}
vec3 lookStencil(vec2 uv, vec3 cam, float dist) {
  float k = engage();
  float mr = k * (1.0 + ST_KICK_JOLT * kickNow());
  float a = stencilInk(uv + ST_MISREG_A * mr, ST_CUT_A);
  float b = stencilInk(uv + ST_MISREG_B * mr, ST_CUT_B + ST_MIDS_INK * midsNow());
  float g = 1.0 - ST_GRAIN * h21(floor((isf_FragNormCoord * RENDERSIZE) / 2.0));
  a *= g; b *= g;
  vec3 c = mix(ndiAt(uv), ST_PAPER, crisp(dist));
  c *= mix(vec3(1.0), ST_INK_B, b);
  c *= mix(vec3(1.0), ST_INK_A, a);
  return mix(cam, c, smoothstep(0.0, 0.25, k));
}

vec3 lookXray(vec2 uv, vec3 cam, float dist) {
  float amt = engage();
  float fig = crisp(dist);
  float rimW = XR_RIM_W * (1.0 + XR_MIDS_RIM * midsNow());
  float rim = clamp(fig * ringFrac(-dist, rimW) * 2.0, 0.0, 1.0);
  float l = 1.0 - lumaOf(ndiAt(uv));
  vec3 x = XR_TINT * pow(l, XR_GAMMA);
  x += XR_CAM_GHOST * vec3(lumaOf(cam));
  x *= 1.0 - XR_SCAN * (0.5 + 0.5 * sin(uv.y * RENDERSIZE.y * 1.5 + TIME * 40.0));
  x += XR_RIM_C * rim * XR_RIM_GAIN * (1.0 + XR_KICK_PUNCH * kickNow());
  return mix(cam, x, fig * amt);
}

vec3 lookDouble(vec2 uv, vec3 cam, float dist) {
  float amt = engage();
  float m = discCov(dist, DE_EDGE_SOFT);
  vec2 nuv = (uv - 0.5) * (1.0 - DE_ZOOM * amt) + 0.5 + DE_MIDS_DRIFT * midsNow() * vec2(sin(TIME * 0.3), cos(TIME * 0.23));
  vec3 n = roomTone(plateAt(nuv));          // the room behind the figure, as the second exposure
  float g = pow(lumaOf(cam), DE_CAM_GAMMA) * DE_CAM_SCREEN * (1.0 + DE_KICK_BURN * kickNow());
  vec3 inside = 1.0 - (1.0 - n) * (1.0 - vec3(clamp(g, 0.0, 1.0)));
  vec3 rm = mix(DE_PAPER, DE_PAPER * ndiAt(uv), DE_ROOM_GHOST);
  return mix(cam, mix(rm, inside, m), amt);
}

vec3 lookGhost(vec2 uv, vec3 cam) {
  float k = engage(); float kick = kickNow();
  vec2 guv = clamp(uv + k * GH_WOBBLE * (1.0 + GH_MIDS_WOBBLE * midsNow()) * vec2(sin(uv.y * 18.0 + TIME * 2.0), 0.0), 0.0, 1.0);
  vec3 ndi = ndiAt(uv);
  vec3 c = camAt(guv) * mix(vec3(1.0), GH_TINT, k);
  float d = edgeDist(guv);
  float fig = crisp(d);
  float rim = fig * (1.0 - smoothstep(GH_RIM_W * 0.6, GH_RIM_W, -d));
  float op = mix(1.0, GH_MIN_OPACITY, k);
  vec3 alphaMix = mix(ndi, c, op);
  vec3 screenMix = 1.0 - (1.0 - ndi) * (1.0 - c * op);
  vec3 ghost = mix(alphaMix, screenMix, GH_SCREEN * k);
  float flash = GH_KICK_FLASH * kick;
  ghost += GH_RIM * k * rim * GH_TINT * 0.6 * (1.0 + 2.0 * flash);
  ghost += k * flash * 0.25 * c * GH_TINT;
  return mix(ndi, ghost, fig);
}

vec2 figCentreCoarse() {             // 4×3 grid of the cut
  vec2 s = vec2(0.0); float w = 0.0;
  for (int j = 0; j < 3; j++) {
    for (int i = 0; i < 4; i++) {
      vec2 q = (vec2(float(i), float(j)) + 0.5) / vec2(4.0, 3.0);
      float m = maskAt(q).b;
      s += q * m; w += m;
    }
  }
  return mix(vec2(0.5), s / max(w, 0.001), step(0.3, w));
}
float splash(vec2 p, float id, vec2 c, float aspect) {
  float ink = 0.0;
  for (int ii = 0; ii < 9; ii++) {
    float i = float(ii);
    float a = h11(id * 13.1 + i) * 6.2831853;
    float r = IP_SPLASH_R * (0.25 + 0.75 * h11(id * 7.7 + i * 1.3));
    vec2 dp = (c - 0.5) * vec2(aspect, 1.0) + vec2(cos(a), sin(a)) * r;
    float s = IP_DROP_SIZE * (0.25 + 0.75 * pow(h11(id * 3.3 + i * 2.9), 2.0));
    float wob = 1.0 + 0.25 * (vnoise(p * 60.0 + i) - 0.5);
    ink = max(ink, 1.0 - smoothstep(s * 0.85, s, length(p - dp) * wob));
    for (int jj = 1; jj < 4; jj++) {
      float j = float(jj);
      vec2 sp = dp + vec2(cos(a), sin(a)) * s * (1.2 + j * 1.1);
      float ss = s * 0.28 / j;
      ink = max(ink, 1.0 - smoothstep(ss * 0.8, ss, length(p - sp)));
    }
  }
  return ink;
}
vec3 lookInkPaper(vec2 uv, vec3 cam, float dist) {
  float amt = engage();
  float aspect = aspectOf();
  vec2 px = (isf_FragNormCoord * RENDERSIZE);
  float fig = crisp(dist);
  float fibre = 0.93 + 0.07 * vnoise(px * vec2(0.9, 0.12)) + 0.03 * vnoise(px * 0.05);
  vec3 paper = IP_PAPER * fibre * (1.0 - IP_WATERMARK * (1.0 - lumaOf(ndiAt(uv))));
  float dark = pow(1.0 - lumaOf(cam), IP_INK_GAMMA);
  float grain = 0.85 + 0.3 * vnoise(px * 0.35);
  float edge = clamp(fig * ringFrac(-dist, IP_EDGE_W) * 2.0, 0.0, 1.0);
  float ink = fig * clamp(max(dark, IP_INK_FLOOR) * grain, 0.0, 1.0);
  ink = max(ink, edge);
  // the last kick's splash, drying (the kick count seeds it)
  float age = kickAge();
  float id = IMG_NORM_PIXEL(plate, vec2(0.5)).a;
  if (age < IP_DRY) {
    float wet = (1.0 - age / IP_DRY) * min(reactK(), 1.0);
    vec2 p = (uv - 0.5) * vec2(aspect, 1.0);
    ink = max(ink, splash(p, id, figCentreCoarse(), aspect) * (0.35 + 0.65 * wet) * wet);
  }
  return mix(cam, mix(paper, IP_INK, ink), amt);
}

float inkMask(vec2 uv, float bleed, vec2 px) {
  float m = discCov(edgeDist(uv), bleed * 0.75);
  float fib = vnoise(px * 0.08) * 0.6 + vnoise(px * 0.35) * 0.4;
  return smoothstep(0.15, 0.6, m + (fib - 0.5) * SI_RAG);
}
vec3 lookShadowInk(vec2 uv, vec3 cam) {
  float k = kickNow(); float amt = engage();
  float bleed = SI_BLEED + SI_KICK_BLEED * k;
  float dens = clamp(SI_INK_BASE + SI_KICK_DENS * k, 0.0, 1.0);
  float m = inkMask(uv, bleed, (isf_FragNormCoord * RENDERSIZE));
  float sh = inkMask(clamp(uv - SI_SHADOW_OFF, 0.0, 1.0), bleed * 2.5, (isf_FragNormCoord * RENDERSIZE) + 37.0);
  float kk = dens * mix(1.0, 1.25 - lumaOf(cam), SI_TONE);
  vec3 tone = mix(vec3(1.0), SI_INK, clamp(kk, 0.0, 1.0) * m);
  tone *= mix(vec3(1.0), SI_INK * 1.5 + 0.35, SI_SHADOW * sh * (1.0 - m));
  return mix(cam, ndiAt(uv) * tone, amt);
}

vec3 figLight(vec2 uv) { return camAt(uv) * figureAt(clamp(uv, 0.0, 1.0)); }
vec3 lookLightComp(vec2 uv, vec3 cam, float dist) {
  float amt = engage();
  float aspect = aspectOf();
  vec3 rm = ndiAt(uv);
  float fig = crisp(dist);
  vec3 over = mix(rm, cam, fig);
  float gn = LC_GAIN * (1.0 + LC_KICK_FLASH * kickNow());
  vec3 L = cam * fig * gn;
  vec3 lit = mix(rm + L, 1.0 - (1.0 - rm) * (1.0 - clamp(L, 0.0, 1.0)), LC_SCREEN);
  float g = LC_GLOW_IDLE + (1.0 - LC_GLOW_IDLE) * midsNow();
  float R = LC_GLOW_R * g;
  // bloom: the glow's shape from the edge distance (smooth), its colour from a
  // figure-weighted spiral of taps (a raw 16-tap spiral of the cut shows copies)
  vec3 bc = vec3(0.0); float bw = 0.0;
  for (int i = 0; i < 12; i++) {
    float fi = float(i) + 0.5;
    float r = sqrt(fi / 12.0) * max(R, 0.02) * 1.5;
    float a = fi * 2.39996;
    vec2 q = clamp(uv + vec2(cos(a) / aspect, sin(a)) * r, 0.0, 1.0);
    float w = figureAt(q);
    bc += camAt(q) * w; bw += w;
  }
  vec3 bloom = bc / max(bw, 1e-3) * discCov(dist + 0.3 * R, R) * step(1e-3, bw);
  lit += bloom * LC_GLOW * g * gn;
  return mix(over, lit, amt);
}

vec3 lookWrap(vec2 uv, vec3 cam, vec3 E) {
  float k = kickNow(); float mi = midsNow();
  float fig = crisp(E.x);
  if (fig < 0.002) return cam;                 // room untouched
  float relAmt = engage();
  float wrapAmt = clamp(AW_MIDS_WRAP * mi + AW_PRESS_WRAP * amount + kickAmt * 0.75 * k, 0.0, 1.0);
  float aspect = aspectOf();
  vec3 amb = vec3(0.0);                        // the room plate's overall light, 3×2
  for (int j = 0; j < 2; j++) {
    for (int i = 0; i < 3; i++) amb += plateAt((vec2(float(i), float(j)) + 0.5) / vec2(3.0, 2.0));
  }
  amb /= 6.0;
  vec3 loc = vec3(0.0);
  for (int i = 0; i < 8; i++) {
    float a = float(i) * 0.785398;
    loc += plateAt(uv + vec2(cos(a) / aspect, sin(a)) * AW_LOCAL_R);
  }
  vec3 light = roomTone(mix(amb, loc / 8.0, AW_LOCAL));
  float ll = max(lumaOf(light), 0.03);
  vec3 relit = cam * (light / ll);
  relit *= mix(1.0, clamp(ll / 0.45, 0.35, 1.6), AW_EXPOSURE);
  vec3 c = mix(cam, relit, AW_RELIGHT * relAmt);
  // light wrap: the room just beyond the edge bleeds over the rim
  float r = AW_WRAP * (0.25 + 0.75 * wrapAmt);
  float s = max(-E.x, 0.0);
  float rim = 0.5 * (ringFrac(s, r) + ringFrac(s, r * 0.5));
  vec2 nrm = E.yz / vec2(aspect, 1.0);
  vec3 wrapCol = (ndiAt(uv + nrm * (s + r * 0.3)) + ndiAt(uv + nrm * (s + r * 0.7))) * 0.5;
  float kw = clamp(rim * 2.0 * wrapAmt * AW_WRAP_GAIN, 0.0, 1.0);
  c = mix(c, 1.0 - (1.0 - c) * (1.0 - wrapCol), kw);
  c = mix(c, wrapCol, 0.35 * kw * rim);
  return mix(cam, c, fig);
}

void main() {
  vec2 uv = isf_FragNormCoord;
  if (PASSINDEX == 0.0) { gl_FragColor = sigPass(); return; }
  if (PASSINDEX == 1.0) { gl_FragColor = depthPass(uv); return; }
  if (PASSINDEX == 2.0) { gl_FragColor = maskPass(uv); return; }
  if (PASSINDEX == 3.0) { gl_FragColor = platePass(uv); return; }

  int m = int(floor(mode + 0.5));
  vec3 cam = camAt(uv);
  vec3 E = edgeInfo(uv);
  vec3 c = cam;
  if (m == 0) c = lookOutline(uv, cam, E.x);
  else if (m == 1) c = lookNeon(uv, cam, E.x);
  else if (m == 2) c = lookAura(uv, cam, E.x);
  else if (m == 3) c = lookKeyGlow(uv, cam, E.x);
  else if (m == 4) c = lookLightning(uv, cam);
  else if (m == 5) c = lookHatRim(uv, cam, E.x);
  else if (m == 6) c = lookBeatHalo(uv, cam, E.x);
  else if (m == 7) c = lookPuppet(uv, cam, E.x);
  else if (m == 8) c = lookPaper(uv, cam);
  else if (m == 9) c = lookStencil(uv, cam, E.x);
  else if (m == 10) c = lookXray(uv, cam, E.x);
  else if (m == 11) c = lookDouble(uv, cam, E.x);
  else if (m == 12) c = lookGhost(uv, cam);
  else if (m == 13) c = lookInkPaper(uv, cam, E.x);
  else if (m == 14) c = lookShadowInk(uv, cam);
  else if (m == 15) c = lookLightComp(uv, cam, E.x);
  else c = lookWrap(uv, cam, E);
  gl_FragColor = vec4(mix(cam, c, wet()), 1.0);
}
