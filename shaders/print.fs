/*{
  "DESCRIPTION": "audiodev print: the picture as cells and dots (mosaic, halftone, dot swap, glyphs, matrix rain, dither, LED wall, stained glass, pointillism).",
  "CREDIT": "simply-creative FX library (FX 11 20 21 67 110 111 124 125 126 210 240 280), ported to tussen ISF",
  "CATEGORIES": ["audiodev"],
  "INPUTS": [
    { "NAME": "inputImage", "TYPE": "image" },
    { "NAME": "depth", "TYPE": "depth" },
    { "NAME": "wave", "TYPE": "audio" },
    { "NAME": "spectrum", "TYPE": "audioFFT" },
    { "NAME": "mode", "TYPE": "long", "MIN": 0, "MAX": 8, "DEFAULT": 0,
      "VALUES": [0, 1, 2, 3, 4, 5, 6, 7, 8],
      "LABELS": ["mosaic", "halftone", "dot swap", "glyphs", "matrix rain", "dither", "LED wall", "stained glass", "pointillism"] },
    { "NAME": "amount", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 1.0, "DESCRIPTION": "the old FX knob: how far, and for most modes how big the cells" },
    { "NAME": "where", "TYPE": "long", "MIN": 0, "MAX": 2, "DEFAULT": 0, "DESCRIPTION": "0 everywhere · 1 the figure (near) · 2 the room (far)" },
    { "NAME": "kickAmt", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.85 },
    { "NAME": "midsAmt", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.25 },
    { "NAME": "highsAmt", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.6, "DESCRIPTION": "hats engage dither and pointillism" },
    { "NAME": "beatSync", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0, "DESCRIPTION": "kicks from the Link beat instead of the sound" },
    { "NAME": "gain", "TYPE": "float", "MIN": 0.25, "MAX": 8.0, "DEFAULT": 1.0 },
    { "NAME": "figure", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5, "DESCRIPTION": "the cut: nearer than this is the figure" },
    { "NAME": "soft", "TYPE": "float", "MIN": 0.01, "MAX": 0.3, "DEFAULT": 0.08 },
    { "NAME": "depthFallback", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0, "DESCRIPTION": "1: depth from blurred brightness instead of the ML map" },
    { "NAME": "depthSmooth", "TYPE": "float", "MIN": 0.0, "MAX": 0.95, "DEFAULT": 0.5 }
  ],
  "PASSES": [
    { "TARGET": "sig", "WIDTH": "3", "HEIGHT": "1", "FLOAT": true, "PERSISTENT": true },
    { "TARGET": "depthBuf", "WIDTH": "$WIDTH / 4", "HEIGHT": "$HEIGHT / 4", "FLOAT": true, "PERSISTENT": true },
    {}
  ]
}*/

// mode 0 · mosaic       FX 11 Pixelate (where=0), FX 110 Mosaic room (where=2, grout), FX 111 Mosaic figure (where=1, blocky silhouette + 8-bit levels)
// mode 1 · halftone     FX 20 Halftone
// mode 2 · dot swap     FX 210 Dot swap (one input: figure dots and room dots in two inks, the cut goes dot-shaped)
// mode 3 · glyphs       FX 21 Glyph blocks
// mode 4 · matrix rain  FX 126 Matrix rain (where=1: the figure is made of code)
// mode 5 · dither       FX 67 Bayer dither (where=0), FX 240 Hat dither room (where=2; hats jitter the threshold)
// mode 6 · LED wall     FX 125 LED wall
// mode 7 · glass        FX 124 Stained glass
// mode 8 · pointillism  FX 280 Highs pointillism
//
// Pixel sizes are Chris's at 720 lines and scale with the frame height.
// Signals: amount = the old u_fx; kickAmt / midsAmt / highsAmt = the old
// KICK_ / MIDS_ / HIGHS_ENGAGE (one set for the shader, his per-FX values in
// the map's notes).

// ════ shared: signals (copy verbatim) ══════════════════════════════════
// sig is 3×1 and kept from frame to frame (pixel 2: ML depth presence, see depthReadyPass):
//   pixel 0: r kick 0..1 (1 on an onset, decays) · g bass envelope · b fract(TIME) · a s since the onset
// (FLOAT buffers are half floats: never store absolute TIME, only fract(TIME) and ages)
//   pixel 1: r lows 0..1 · g mids 0..1 · b highs 0..1 · a mids slow average
// The kick is Chris's waveform-domain detector: 150 Hz low-pass RMS over
// the 512-sample program, an onset when it clears 1.35× its own envelope.
// ML depth present? A map's nearest point reads 1; with no model (not
// fetched, failed, not a Mac) the map reads 0 everywhere. The max over a
// 3×3 grid, smoothed over a few frames (sig pixel 2: r presence · g raw max),
// so one bad frame never flips it. depthReady() is the one place to read it.
vec4 depthReadyPass(float dt) {
  float m = 0.0;
  for (int y = 0; y < 3; y++) {
    for (int x = 0; x < 3; x++) {
      m = max(m, IMG_NORM_PIXEL(depth, (vec2(float(x), float(y)) + 0.5) / 3.0).r);
    }
  }
  float prev = IMG_NORM_PIXEL(sig, vec2(2.5 / 3.0, 0.5)).r * step(1.0, FRAMEINDEX);
  return vec4(prev + (1.0 - exp(-dt / 0.15)) * (step(0.02, m) - prev), m, 0.0, 1.0);
}
vec4 sigPass() {
  vec4 p0 = IMG_NORM_PIXEL(sig, vec2(0.5 / 3.0, 0.5));
  vec4 p1 = IMG_NORM_PIXEL(sig, vec2(1.5 / 3.0, 0.5));
  float dt = mix(clamp(fract(fract(TIME) - p0.b + 1.0), 1.0 / 240.0, 0.1), 1.0 / 60.0, 1.0 - step(1.0, FRAMEINDEX));
  if (isf_FragNormCoord.x > 2.0 / 3.0) { return depthReadyPass(dt); }
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

  if (isf_FragNormCoord.x < 1.0 / 3.0) {
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
vec4 sigA() { return IMG_NORM_PIXEL(sig, vec2(0.5 / 3.0, 0.5)); }
vec4 sigB() { return IMG_NORM_PIXEL(sig, vec2(1.5 / 3.0, 0.5)); }
float kickAge() { return sigA().a; }   // seconds since the last kick (60 = none for a minute)
float depthReady() { return IMG_NORM_PIXEL(sig, vec2(2.5 / 3.0, 0.5)).r; }
float kickNow() { return max(sigA().r, beatSync * exp(-6.0 * PHASE)); }
float lowsNow() { return sigB().r; }
float midsNow() { return sigB().g; }
float highsNow() { return sigB().b; }

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
// mosaic (FX 11 / 110 / 111)
const float MAX_BLOCK   = 96.0;  // block size in px at full amount (720 lines)
const float CURVE       = 2.0;   // >1 = fine control near zero, big jumps at the top
const float KICK_JUMP   = 0.5;   // kick: blocks jump this much bigger
const float GRID_DARK   = 0.15;  // FX 11 seam (1 px)
const float GROUT       = 0.08;  // FX 110 grout width, fraction of a tile
const float GROUT_DARK  = 0.35;  // FX 110 grout darkness
const float KICK_FLASH  = 0.15;  // FX 110 kick brightens the tiles
const float LEVELS_FIG  = 6.0;   // FX 111 colour levels at full (8-bit avatar)
const float GRID_LINE   = 0.12;  // FX 111 block edges
// halftone (FX 20)
const float HT_CELL_MIN = 5.0;
const float HT_CELL_MAX = 28.0;
const float HT_ANGLE    = 0.785;
const vec3  HT_PAPER    = vec3(0.04, 0.035, 0.05);
const float HT_BOOST    = 1.2;
const float HT_SWELL    = 0.45;  // kick swells every dot
const float HT_TURN     = 0.35;  // mids turn the screen, rad
// dot swap (FX 210)
const float DS_CELL_MIN = 6.0;
const float DS_CELL_MAX = 34.0;
const float DS_ANGLE    = 0.3;
const float DS_GAIN     = 1.25;
const vec3  DS_PAPER    = vec3(0.03, 0.03, 0.05);
const float DS_SWELL    = 0.35;
const float DS_TURN     = 0.25;
const vec3  INK_FIG     = vec3(1.0, 0.42, 0.18);  // figure dots (stands in for "coloured by the NDI")
const vec3  INK_ROOM    = vec3(0.2, 0.62, 1.0);   // room dots
const float INK_MIX     = 0.7;   // 0 = the camera's own colours, 1 = pure two inks
// glyphs (FX 21)
const float GL_CELL_MIN = 7.0;
const float GL_CELL_MAX = 48.0;
const float GL_MONO     = 0.0;   // 0 = cell colour · 1 = terminal green
const vec3  GL_MONO_COL = vec3(0.35, 1.0, 0.45);
const float GL_BOOST    = 1.6;
const vec3  GL_BG       = vec3(0.02, 0.02, 0.03);
const float GL_CONTRAST = 1.3;
const float GL_KICK_CELL = 0.6;
const float GL_KICK_INK  = 0.8;
const float GL_MIDS_DENSE = 0.35;
// matrix rain (FX 126)
const float MX_COLS     = 70.0;
const float MX_SPEED    = 9.0;
const float MX_TRAIL    = 14.0;
const float MX_FLIP     = 6.0;
const vec3  MX_GREEN    = vec3(0.25, 1.0, 0.4);
const float MX_FIG_SHOW = 0.35;
const float MX_ROOM_DIM = 0.3;
const float MX_SURGE    = 1.2;
const float MX_DENSE    = 0.4;
// dither (FX 67 / 240)
const float DI_LEVELS   = 8.0;
const float DI_DOT_MAX  = 4.0;
const float DI_MONO_FROM = 0.75;
const vec3  DI_INK      = vec3(0.05, 0.03, 0.12);
const vec3  DI_PAPER    = vec3(0.95, 1.0, 0.85);
const float DI_KICK_DOT = 3.0;
const float DI_KICK_INV = 1.0;
const float DI_JITTER   = 0.35;  // FX 240: threshold shimmer at full highs
const float DI_HAT_THROW = 0.25; // FX 240: re-throw while the hats are hot
// LED wall (FX 125)
const float LED_FINE    = 140.0;
const float LED_COARSE  = 18.0;
const float LED_CURVE   = 0.7;
const float LED_DOT     = 0.38;
const float LED_GLOW    = 0.35;
const float LED_BLACK   = 0.04;
const float LED_GAIN    = 1.35;
const float LED_FLASH   = 0.4;
const float LED_MIDS_GLOW = 0.6;
// stained glass (FX 124)
const float SG_FINE     = 70.0;
const float SG_BIG      = 7.0;
const float SG_LEAD     = 0.06;
const vec3  SG_LEAD_COL = vec3(0.03, 0.03, 0.04);
const float SG_SAT      = 1.6;
const float SG_FADE     = 0.2;
const float SG_JITTER   = 0.85;
const float SG_KICK_LIGHT = 0.6;
const float SG_MIDS_LEAD  = 0.8;
// pointillism (FX 280)
const float PT_COARSE   = 22.0;
const float PT_FINE     = 6.0;
const float PT_STEPS    = 4.0;
const float PT_R        = 0.62;
const float PT_SAT      = 1.5;
const float PT_HUE_JIT  = 0.06;
const vec3  PT_CANVAS   = vec3(0.93, 0.9, 0.82);
const float PT_CANVAS_SHOW = 0.55;
// ──────────────────────────────────────────────────────────────────

// pixel position, y up (gl_FragCoord runs y down in tussen; the old f.px ran up)
vec2 fragPx() { return isf_FragNormCoord * RENDERSIZE; }
vec3 camAt(vec2 uv) { return IMG_NORM_PIXEL(inputImage, clamp(uv, 0.0, 1.0)).rgb; }
float h11(float x) { return fract(sin(x * 127.1) * 43758.5453); }
float h21(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
vec2 h22(vec2 p) {
  p = vec2(dot(p, vec2(127.1, 311.7)), dot(p, vec2(269.5, 183.3)));
  return fract(sin(p) * 43758.5453);
}
float bayer2(vec2 p) { p = floor(mod(p, 2.0)); return p.x * 2.0 - 4.0 * p.x * p.y + 3.0 * p.y; }
float bayer8(vec2 p) { return bayer2(p) * 16.0 + bayer2(floor(p / 2.0)) * 4.0 + bayer2(floor(p / 4.0)); }

// mode 0 · mosaic. Returns the final colour (region handled inside: the
// figure variant cuts at block centres so its silhouette goes 8-bit too).
vec3 mosaic(vec2 uv, vec3 cam, float amt, float k, float S, float w) {
  float s = floor(1.0 + (MAX_BLOCK * S - 1.0) * pow(amt, CURVE) * (1.0 + KICK_JUMP * k));
  if (s <= 1.0) return cam;
  vec2 px = fragPx();
  vec2 centre = RENDERSIZE * 0.5;
  vec2 cell = floor((px - centre) / s);
  vec2 cuv = clamp((centre + (cell + 0.5) * s) / RENDERSIZE, 0.0, 1.0);
  vec2 q = fract((px - centre) / s);
  vec3 c = camAt(cuv);
  float on = smoothstep(0.0, 0.05, amt);
  if (w < 0.5) {                                   // FX 11
    float seam = step(1.0 - 1.0 / s, max(q.x, q.y));
    c *= 1.0 - GRID_DARK * seam * smoothstep(3.0, 8.0, s);
    return mix(cam, c, on);
  }
  if (w < 1.5) {                                   // FX 111
    float lv = mix(64.0, LEVELS_FIG, amt);
    c = floor(c * lv + 0.5) / lv;
    vec2 e = min(q, 1.0 - q);
    c *= 1.0 - GRID_LINE * amt * (1.0 - smoothstep(0.0, 0.08, min(e.x, e.y)));
    return mix(cam, c, figureAt(cuv) * smoothstep(0.0, 0.15, amt));
  }
  float edge = min(min(q.x, q.y), min(1.0 - q.x, 1.0 - q.y));    // FX 110
  float grout = 1.0 - smoothstep(GROUT * 0.5, GROUT * 0.5 + 1.5 / s, edge);
  c *= 1.0 - GROUT_DARK * grout * smoothstep(4.0 * S, 12.0 * S, s);
  c *= 1.0 + KICK_FLASH * k;
  return mix(cam, c, on * (1.0 - figureAt(uv)));
}

// a rotated dot screen: xy = cell centre uv, z = distance from it (cells), w unused
vec4 screenCell(float cell, float ang) {
  mat2 R = mat2(cos(ang), sin(ang), -sin(ang), cos(ang));
  vec2 p = R * fragPx() / cell;
  vec2 id = floor(p) + 0.5;
  vec2 cuv = (transpose(R) * (id * cell)) / RENDERSIZE;
  return vec4(clamp(cuv, 0.0, 1.0), length(p - id), 0.0);
}

// FX 21: 5×5 bitmaps, bit = row*5 + col (row 0 = top), by ink density
int glyphBits(int i) {
  int g[11] = int[11](0, 4194304, 131200, 14336, 1016800, 4357252,
                      18157905, 22511061, 11512810, 27070835, 33224255);
  return g[i];
}

vec3 glyphs(vec2 uv, vec3 cam, float amt, float k, float m, float S) {
  float cell = mix(GL_CELL_MIN, GL_CELL_MAX, amount * amount) * S * (1.0 + GL_KICK_CELL * k);
  vec2 px = fragPx();
  vec2 cuv = (floor(px / cell) + 0.5) * cell / RENDERSIZE;
  vec2 q = fract(px / cell);
  vec2 o = 0.25 * cell / RENDERSIZE;
  vec3 cc = 0.25 * (camAt(cuv - o) + camAt(cuv + vec2(o.x, -o.y)) + camAt(cuv + vec2(-o.x, o.y)) + camAt(cuv + o));
  float l = clamp((lumaOf(cc) - 0.5) * GL_CONTRAST + 0.5 + GL_MIDS_DENSE * m, 0.0, 1.0);
  int gi = int(clamp(floor(l * 11.0), 0.0, 10.0));
  ivec2 g = ivec2(floor(q * 6.0));
  int row = 4 - (g.y - 1);                         // y up → row 0 at the top
  float on = 0.0;
  if (g.x < 5 && g.y >= 1) { on = float((glyphBits(gi) >> (row * 5 + g.x)) & 1); }
  vec3 ink = mix(cc / max(max(cc.r, max(cc.g, cc.b)), 0.15), GL_MONO_COL * (0.3 + 0.7 * l), GL_MONO);
  vec3 glyph = mix(GL_BG, ink * GL_BOOST * mix(0.6, 1.0, l) * (1.0 + GL_KICK_INK * k), on);
  return mix(cam, glyph, amt);
}

vec3 matrixRain(vec2 uv, vec3 cam, float amt, float k, float m) {
  float aspect = RENDERSIZE.x / RENDERSIZE.y;
  vec2 grid = vec2(MX_COLS, MX_COLS / aspect * 0.75);
  vec2 p = uv * grid;
  vec2 cell = floor(p); vec2 fr = fract(p);
  float col = cell.x;
  float sp = MX_SPEED * (0.5 + h11(col + 3.1));
  float head = TIME * sp + h11(col) * 200.0;
  float rowFromTop = grid.y - cell.y;
  float trail = MX_TRAIL * (1.0 + MX_SURGE * k);
  float behind = mod(head - rowFromTop, MX_TRAIL * 2.5);
  float on = step(behind, trail) * (1.0 - behind / trail);
  float live = step(h11(col + 9.7), 0.55 + MX_DENSE * m);
  on *= live;
  float seed = floor(TIME * MX_FLIP * (0.5 + h21(cell)));
  vec2 gg = floor(fr * vec2(6.0, 8.0));
  float g = (gg.x < 0.5 || gg.y < 0.5) ? 0.0 : step(0.5, h21(cell * 7.13 + gg * 1.7 + seed));
  float isHead = step(behind, 1.0) * live;
  vec3 code = MX_GREEN * g * on * on;
  code = mix(code, vec3(0.85, 1.0, 0.9) * g, isHead * (0.6 + 0.4 * MX_SURGE * k));
  code *= mix(1.0, 0.4 + 1.2 * lumaOf(cam), MX_FIG_SHOW);
  vec3 figOut = mix(cam, code + cam * 0.08, amt);
  vec3 room = cam * (1.0 - MX_ROOM_DIM * amt);
  return mix(room, figOut, regionAt(uv));
}

vec3 dither(vec2 uv, vec3 cam, float amt, float k, float hi, float S) {
  float eng = smoothstep(0.0, 0.08, amt);
  float cpx = max(1.0, floor(mix(1.0, DI_DOT_MAX + 0.99, amt) * S + DI_KICK_DOT * k * eng));
  vec2 cell = floor(fragPx() / cpx);
  vec3 src = camAt((cell + 0.5) * cpx / RENDERSIZE);
  // FX 240: hats slide the pattern and jitter / re-throw the threshold
  float n = floor(TIME * 8.0) * step(0.35, hi);
  vec2 bc = cell + vec2(mod(n, 4.0), mod(floor(n / 4.0), 4.0));
  float shimmer = (h11(floor(TIME * 40.0)) - 0.5) * 2.0 * DI_JITTER * hi;
  float thrw = (h11(n + 0.5) - 0.5) * 2.0 * DI_HAT_THROW * smoothstep(0.35, 0.8, hi);
  float th = (bayer8(bc) + 0.5) / 64.0 - 0.5 + shimmer + thrw;
  float lv = max(2.0, floor(mix(DI_LEVELS, 2.0, smoothstep(0.08, DI_MONO_FROM, amt)) + 0.5));
  vec3 q = floor(src * (lv - 1.0) + 0.5 + th) / (lv - 1.0);
  float bit = step(0.5, lumaOf(src) + th);
  bit = mix(bit, 1.0 - bit, step(0.5, DI_KICK_INV * k));
  q = mix(q, mix(DI_INK, DI_PAPER, bit), smoothstep(DI_MONO_FROM, 1.0, amt));
  return mix(cam, clamp(q, 0.0, 1.0), eng);
}

vec3 ledWall(vec2 uv, vec3 cam, float amt, float k, float m) {
  float rows = mix(LED_FINE, LED_COARSE, pow(amt, LED_CURVE));
  vec2 grid = vec2(rows * RENDERSIZE.x / RENDERSIZE.y, rows);
  vec2 g = uv * grid;
  vec2 cell = floor(g);
  vec3 c = vec3(LED_BLACK);
  float glow = LED_GLOW + LED_MIDS_GLOW * m;
  float drive = LED_GAIN * (1.0 + LED_FLASH * k);
  for (int j = -1; j <= 1; j++) {
    for (int i = -1; i <= 1; i++) {
      vec2 cc = cell + vec2(float(i), float(j));
      vec3 col = camAt((cc + 0.5) / grid) * drive;
      float d = length(g - cc - 0.5);
      c += col * (smoothstep(LED_DOT, LED_DOT - 0.08, d) + glow * exp(-d * d * 4.0) * 0.5);
    }
  }
  return mix(cam, c, smoothstep(0.0, 0.12, amt));
}

vec3 stainedGlass(vec2 uv, vec3 cam, float amt, float k, float m) {
  float aspect = RENDERSIZE.x / RENDERSIZE.y;
  float cells = mix(SG_FINE, SG_BIG, amt);
  vec2 p = vec2(uv.x * aspect, uv.y) * cells;
  vec2 ip = floor(p); vec2 fp = fract(p);
  vec2 mg = vec2(0.0); vec2 mr = vec2(0.0);
  float md = 8.0;
  for (int j = -1; j <= 1; j++) {
    for (int i = -1; i <= 1; i++) {
      vec2 g = vec2(float(i), float(j));
      vec2 r = g + 0.5 + SG_JITTER * (h22(ip + g) - 0.5) - fp;
      float d = dot(r, r);
      if (d < md) { md = d; mr = r; mg = g; }
    }
  }
  float border = 8.0;
  for (int j = -2; j <= 2; j++) {
    for (int i = -2; i <= 2; i++) {
      vec2 g = mg + vec2(float(i), float(j));
      vec2 r = g + 0.5 + SG_JITTER * (h22(ip + g) - 0.5) - fp;
      if (dot(mr - r, mr - r) > 1e-5) { border = min(border, dot(0.5 * (mr + r), normalize(r - mr))); }
    }
  }
  vec2 seed = (ip + fp + mr) / cells;
  vec3 glass = camAt(vec2(seed.x / aspect, seed.y));
  float l = lumaOf(glass);
  glass = clamp(mix(vec3(l), glass, SG_SAT), 0.0, 1.0);
  glass *= 0.8 + 0.35 * smoothstep(0.0, 0.5, border);
  glass *= 1.0 + SG_KICK_LIGHT * k;
  float lw = SG_LEAD * (1.0 + SG_MIDS_LEAD * m);
  float aa = 1.5 * cells / RENDERSIZE.y;
  vec3 win = mix(glass, SG_LEAD_COL, 1.0 - smoothstep(lw - aa, lw + aa, border));
  return mix(cam, win, smoothstep(0.0, SG_FADE, amt));
}

vec3 pointillism(vec2 uv, vec3 cam, float amt, float hi, float S) {
  float st = min(floor(hi * PT_STEPS), PT_STEPS - 1.0);          // the jump
  float cell = mix(PT_COARSE, PT_FINE, st / max(PT_STEPS - 1.0, 1.0)) * S;
  vec2 p = fragPx() / cell;
  vec2 ip = floor(p);
  vec3 col = mix(cam * 0.45, PT_CANVAS, PT_CANVAS_SHOW);
  float best = 1e9; vec2 bc = vec2(0.0); vec2 br = vec2(0.0); float brad = 0.0; float bd = 1e9;
  for (int y = -1; y <= 1; y++) {
    for (int x = -1; x <= 1; x++) {
      vec2 g = ip + vec2(float(x), float(y));
      vec2 r = h22(g + st * 17.31);
      vec2 c = g + 0.5 + (r - 0.5) * 0.8;
      float rad = PT_R * (0.75 + 0.5 * r.y);
      float d = length(p - c);
      if (d < rad && r.x < best) { best = r.x; bc = c; br = r; brad = rad; bd = d; }
    }
  }
  if (best < 1e8) {
    vec3 dc = camAt(bc * cell / RENDERSIZE);
    float l = lumaOf(dc);
    dc = clamp(mix(vec3(l), dc, PT_SAT) + (vec3(br.x, br.y, 1.0 - br.x) - 0.5) * PT_HUE_JIT * 2.0, 0.0, 1.0);
    col = mix(col, dc, smoothstep(brad, brad - 0.12, bd));
  }
  return mix(cam, col, smoothstep(0.0, 0.6, amt));
}

void main() {
  vec2 uv = isf_FragNormCoord;
  if (PASSINDEX == 0.0) { gl_FragColor = sigPass(); return; }
  if (PASSINDEX == 1.0) { gl_FragColor = depthPass(uv); return; }

  float md = floor(mode + 0.5);
  float w = floor(where + 0.5);
  vec3 cam = IMG_NORM_PIXEL(inputImage, uv).rgb;
  float k = kickNow();
  float m = midsNow();
  float hi = highsNow();
  float S = RENDERSIZE.y / 720.0;                  // Chris's px were at 720 lines
  float amt = clamp(amount + kickAmt * k + midsAmt * m, 0.0, 1.0);
  vec3 col = cam;

  if (md < 0.5) {
    gl_FragColor = vec4(mosaic(uv, cam, amt, k, S, w), 1.0);
    return;
  } else if (md < 1.5) {                           // FX 20 halftone
    float cell = mix(HT_CELL_MIN, HT_CELL_MAX, amount) * S;
    vec4 sc = screenCell(cell, HT_ANGLE + HT_TURN * m);
    vec3 cc = camAt(sc.xy);
    float r = 0.72 * sqrt(clamp(lumaOf(cc) * 1.15, 0.0, 1.0)) * (1.0 + HT_SWELL * k);
    float aa = 1.5 / cell;
    vec3 ht = mix(HT_PAPER, cc * HT_BOOST, 1.0 - smoothstep(r - aa, r + aa, sc.z));
    float a = clamp(smoothstep(0.0, 0.08, amount) + kickAmt * k + midsAmt * m, 0.0, 1.0);
    col = mix(cam, ht, a);
  } else if (md < 2.5) {                           // FX 210 dot swap
    float cell = mix(DS_CELL_MIN, DS_CELL_MAX, amount) * S;
    vec4 sc = screenCell(cell, DS_ANGLE + DS_TURN * m);
    vec3 cc = camAt(sc.xy);
    float figC = step(0.5, figureAt(sc.xy));      // each cell picks its side at its centre
    vec3 ink = mix(cc, mix(INK_ROOM, INK_FIG, figC) * (0.35 + 0.65 * sqrt(lumaOf(cc))), INK_MIX);
    float r = 0.72 * sqrt(clamp(lumaOf(cc), 0.0, 1.0)) * (1.0 + DS_SWELL * k);
    float aa = 1.5 / cell;
    vec3 tone = mix(DS_PAPER, ink * DS_GAIN, 1.0 - smoothstep(r - aa, r + aa, sc.z));
    col = mix(cam, tone, amt);
  } else if (md < 3.5) {
    float a = clamp(smoothstep(0.0, 0.06, amount) + kickAmt * k + midsAmt * m, 0.0, 1.0);
    col = glyphs(uv, cam, a, k, m, S);
  } else if (md < 4.5) {
    gl_FragColor = vec4(matrixRain(uv, cam, amt, k, m), 1.0);
    return;
  } else if (md < 5.5) {
    col = dither(uv, cam, clamp(amt + highsAmt * hi, 0.0, 1.0), k, hi, S);
  } else if (md < 6.5) {
    col = ledWall(uv, cam, amt, k, m);
  } else if (md < 7.5) {
    col = stainedGlass(uv, cam, amt, k, m);
  } else {
    col = pointillism(uv, cam, clamp(amt + highsAmt * hi, 0.0, 1.0), hi, S);
  }
  gl_FragColor = vec4(mix(cam, col, regionAt(uv)), 1.0);
}
