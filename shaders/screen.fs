/*{
  "DESCRIPTION": "audiodev screen: screens and film stock — CRT, film print, VHS, night vision, hologram figure, hat static, hat scanline shove, hat grain. One mode per look, `where` puts it on the whole picture, the figure or the room.",
  "CREDIT": "simply-creative FX library (FX 22 57 58 87 92 180 212 255), ported to tussen ISF",
  "CATEGORIES": ["texture", "depth", "sound", "beat", "modes", "pressure"],
  "INPUTS": [
    { "NAME": "inputImage", "TYPE": "image" },
    { "NAME": "depth", "TYPE": "depth" },
    { "NAME": "wave", "TYPE": "audio" },
    { "NAME": "spectrum", "TYPE": "audioFFT" },
    { "NAME": "amount", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 1.0, "DESCRIPTION": "how strong the screen is: 1 rests where it was tuned and kicks, mids and hats push it up; 0 the picture untouched" },
    { "NAME": "react", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5, "DESCRIPTION": "how much the music moves it: 0 holds it still, 0.5 as tuned, 1 twice as much" },
    { "NAME": "mode", "TYPE": "long", "MIN": 0, "MAX": 7, "DEFAULT": 0,
      "VALUES": [0, 1, 2, 3, 4, 5, 6, 7],
      "LABELS": ["CRT", "film", "VHS", "night vision", "hologram", "hat static", "hat scanlines", "hat grain"] },
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
    { "TARGET": "hat", "WIDTH": "1", "HEIGHT": "1", "FLOAT": true, "PERSISTENT": true },
    { "TARGET": "plate", "WIDTH": "$WIDTH / 2", "HEIGHT": "$HEIGHT / 2", "PERSISTENT": true },
    {}
  ]
}*/

// mode 0 · CRT            FX 22 CRT (scanlines, barrel, phosphor mask)
// mode 1 · film           FX 57 Film (grain, leaks, flicker, weave, dust, scratches)
// mode 2 · VHS            FX 58 VHS (tracking, chroma bleed, rolling band, head switch)
// mode 3 · night vision   FX 87 Night vision
// mode 4 · hologram       FX 92 Hologram (always the figure; see-through to the room plate)
// mode 5 · hat static     FX 180 Hat static room (where=2)
// mode 6 · hat scanlines  FX 212 Hat scanline room (where=2)
// mode 7 · hat grain      FX 255 Highs grain figure (where=1)
// Modes 0–3 are whole-picture looks (where=0 is the original); 5–7 are the
// hat-driven ones, their original `where` noted above, any `where` works.

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
// 1 while music plays, 0 in silence (the lows or the highs heard): a pulse
// on the beat clock alone is gated on it, so a look sits still in silence.
float musicOn() { return smoothstep(0.05, 0.2, max(sigB().r, sigB().b)); }
float kickAge() { return sigA().a; }   // seconds since the last kick (60 = none for a minute)
float depthReady() { return DEPTH_READY; }   // tussen's own: 1 while the depth model's map is bound
// react 0.5 is the music as the shader was tuned; 0 holds it still, 1 doubles it.
float reactK() { return 2.0 * react; }
// amount 1 is the look as tuned: it rests at REST and the music pushes it
// from there. Lower, it rests lower (near 0 only the music brings it in),
// and below 0.1 it fades out to the picture as it came in at 0.
const float REST = 0.25;
float press() { return REST * amount; }
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
// react 0 holds it: the seed stops changing on kicks
float kickSeed() { return fract(floor((TIME - kickAge()) * 20.0) * 0.6180339) * 977.0 * step(0.001, react) + beatSync * floor(BEAT); }

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
  return clamp(press() + kickAmt * ke * kickNow() + midsAmt * me * midsNow() + highsAmt * he * highsNow(), 0.0, 1.0);
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

// ── mode 0 · CRT (FX 22) — grab these first ─────────────────────────
const float CRT_CURVE      = 0.45;   // barrel distortion at full knob
const float CRT_SCANLINES  = 260.0;  // scanline count over the frame height
const float CRT_SCAN_DEPTH = 0.55;   // how dark the gaps between lines get
const float CRT_MASK_PX    = 3.0;    // phosphor triad width, screen px
const float CRT_MASK_DEPTH = 0.35;   // phosphor mask strength
const float CRT_VIGNETTE   = 0.55;   // corner darkening
const float CRT_BLEED      = 1.5;    // horizontal RGB misconvergence, px
const float CRT_GLOW       = 1.25;   // brightness make-up for the darkening
const float CRT_FLICKER    = 0.03;   // 60 Hz-ish brightness wobble
const float CRT_KICK_BULGE = 0.6;    // extra curvature on a kick (× CURVE)
const float CRT_KICK_SURGE = 0.35;   // brightness surge on a kick
const float CRT_MIDS_SCAN  = 0.6;    // scanline gaps deepen with the mids
const float CRT_KICK_ENGAGE = 0.9;
const float CRT_MIDS_ENGAGE = 0.25;

vec3 crt(vec2 uv, vec2 px) {
  float kick = kickNow(); float mids = midsNow();
  float k = engage(CRT_KICK_ENGAGE, CRT_MIDS_ENGAGE, 0.0);
  vec2 cc = uv - 0.5;
  float r2 = dot(cc, cc);
  float cv = CRT_CURVE * k * (1.0 + CRT_KICK_BULGE * kick);
  vec2 cuv = 0.5 + cc * (1.0 + cv * r2) / (1.0 + cv * 0.25);
  vec2 e = smoothstep(vec2(0.0), vec2(0.006), cuv) * smoothstep(vec2(0.0), vec2(0.006), 1.0 - cuv);
  float inside = e.x * e.y;
  vec2 px1 = vec2(CRT_BLEED * k / RENDERSIZE.x, 0.0);
  vec3 c = vec3(camAt(cuv + px1).r, camAt(cuv).g, camAt(cuv - px1).b);
  float s = 0.5 + 0.5 * cos(cuv.y * CRT_SCANLINES * 6.2831853);
  float sd = mix(CRT_SCAN_DEPTH, 0.9, CRT_MIDS_SCAN * mids);
  c *= mix(1.0, 1.0 - sd * (1.0 - s), k);
  float m = mod(floor(px.x / CRT_MASK_PX), 3.0);
  vec3 mask = vec3(1.0 - step(0.5, m), step(0.5, m) * (1.0 - step(1.5, m)), step(1.5, m));
  c *= mix(vec3(1.0), 0.6 + 1.2 * mask, CRT_MASK_DEPTH * k);
  float vig = 1.0 - CRT_VIGNETTE * k * smoothstep(0.1, 0.55, length(cc) * 1.1);
  float flick = 1.0 + CRT_FLICKER * k * sin(TIME * 377.0);
  c *= vig * flick * mix(1.0, CRT_GLOW, k) * (1.0 + CRT_KICK_SURGE * k * kick);
  return c * inside;
}

// ── mode 1 · film (FX 57) — grab these first ────────────────────────
const float FILM_GRAIN     = 0.22;
const float FILM_GRAIN_PX  = 1.6;
const float FILM_LEAK      = 0.9;
const vec3  FILM_LEAK_COLOR = vec3(1.0, 0.45, 0.15);
const float FILM_FLICKER   = 0.18;
const float FILM_WEAVE_PX  = 2.5;
const float FILM_SCRATCHES = 0.7;
const float FILM_VIGNETTE  = 0.55;
const float FILM_FADE_TONE = 0.6;
const float FILM_FPS       = 24.0;
const float FILM_KICK_FLASH = 0.35;
const float FILM_MIDS_LEAK  = 1.0;
const float FILM_KICK_ENGAGE = 0.7;
const float FILM_MIDS_ENGAGE = 0.25;

vec3 film(vec2 uv0, vec2 px) {
  float kick = kickNow(); float mids = midsNow();
  float k = engage(FILM_KICK_ENGAGE, FILM_MIDS_ENGAGE, 0.0);
  float frame = floor(TIME * FILM_FPS);
  vec2 res = RENDERSIZE;
  vec2 weave = (vec2(vnoise1(TIME * 3.0), vnoise1(TIME * 2.3 + 11.0)) - 0.5) * 2.0;
  weave.y += step(0.97, hash12(vec2(frame, 3.0))) * 1.5;
  weave.y += FILM_KICK_FLASH * 6.0 * kick * step(0.001, k);
  vec2 uv = uv0 + weave * FILM_WEAVE_PX * k / res;
  vec3 col = camAt(uv);
  float l = lumaOf(col);
  vec3 tone = mix(vec3(l), col, 0.7) * vec3(1.05, 0.97, 0.85) * 0.92 + vec3(0.05, 0.04, 0.03);
  col = mix(col, tone, FILM_FADE_TONE * k);
  float fl = (vnoise1(TIME * 14.0) - 0.5) * 2.0 + (hash12(vec2(frame, 1.0)) - 0.5);
  col *= 1.0 + FILM_FLICKER * k * fl + FILM_KICK_FLASH * k * kick;
  float aspect = res.x / max(res.y, 1.0);
  vec2 p = vec2(uv0.x * aspect, uv0.y);
  vec2 c1 = vec2(-0.1 + 0.25 * vnoise1(TIME * 0.15), 0.3 + 0.6 * vnoise1(TIME * 0.11 + 4.0));
  vec2 c2 = vec2(aspect + 0.1 - 0.3 * vnoise1(TIME * 0.13 + 9.0), 0.9 * vnoise1(TIME * 0.09 + 2.0));
  float leak = exp(-4.0 * dot(p - c1, p - c1)) + 0.7 * exp(-5.0 * dot(p - c2, p - c2));
  leak *= 0.6 + 0.4 * vnoise1(TIME * 0.7 + 5.0);
  vec3 lc = FILM_LEAK_COLOR * leak * FILM_LEAK * k * (1.0 + FILM_MIDS_LEAK * mids);
  col = 1.0 - (1.0 - col) * (1.0 - clamp(lc, 0.0, 1.0));
  float sc = 0.0;
  for (int i = 0; i < 2; i++) {
    float h = hash12(vec2(frame, float(i) + 20.0));
    float x = hash12(vec2(frame * 0.37, float(i) + 40.0));
    sc += step(0.55, h) * (1.0 - smoothstep(0.0, 1.2, abs(px.x - x * res.x))) * (0.5 + 0.5 * hash12(vec2(floor(px.y / 6.0), frame)));
  }
  float dust = step(0.9985, hash12(floor(px / 9.0) + frame * 1.37));
  col = mix(col, vec3(0.9, 0.88, 0.8), clamp(sc, 0.0, 1.0) * FILM_SCRATCHES * k * 0.6);
  col = mix(col, vec3(0.05), dust * FILM_SCRATCHES * k);
  float g = hash12(floor(px / FILM_GRAIN_PX) + frame * 17.0) - 0.5;
  float l2 = lumaOf(col);
  col += g * FILM_GRAIN * k * (0.5 + 2.0 * l2 * (1.0 - l2));
  vec2 v = uv0 - 0.5;
  float vd = length(v * vec2(aspect, 1.0)) / length(vec2(aspect, 1.0) * 0.5);
  col *= 1.0 - FILM_VIGNETTE * k * smoothstep(0.35, 1.0, vd);
  return max(col, 0.0);
}

// ── mode 2 · VHS (FX 58) — grab these first ─────────────────────────
const float VHS_WOBBLE       = 0.012;
const float VHS_CHROMA_SHIFT = 0.012;
const float VHS_CHROMA_BLUR  = 0.006;
const float VHS_BAND_SPEED   = 0.18;
const float VHS_BAND_WIDTH   = 0.08;
const float VHS_BAND_TEAR    = 0.04;
const float VHS_NOISE        = 0.18;
const float VHS_HEADSWITCH   = 0.04;
const float VHS_DESAT        = 0.35;
const float VHS_KICK_GLITCH  = 3.0;
const float VHS_KICK_JUMP    = 0.03;
const float VHS_MIDS_SMEAR   = 1.0;
const float VHS_KICK_ENGAGE  = 0.8;
const float VHS_MIDS_ENGAGE  = 0.25;

float vnoiseV(float x) { float i = floor(x); float t = fract(x); return mix(h11(i), h11(i + 1.0), t * t * (3.0 - 2.0 * t)); }
vec3 rgb2yiq(vec3 c) { return mat3(0.299, 0.596, 0.211, 0.587, -0.274, -0.523, 0.114, -0.322, 0.312) * c; }
vec3 yiq2rgb(vec3 c) { return mat3(1.0, 1.0, 1.0, 0.956, -0.272, -1.106, 0.621, -0.647, 1.703) * c; }

vec3 vhs(vec2 uv0, vec2 px) {
  float kick = kickNow(); float mids = midsNow();
  float k = engage(VHS_KICK_ENGAGE, VHS_MIDS_ENGAGE, 0.0);
  vec2 uv = uv0;
  float t = TIME;
  float line = floor(uv.y * RENDERSIZE.y / 2.0);
  float jit = (h11(line + floor(t * 30.0) * 17.0) - 0.5) * 0.3 + (vnoiseV(uv.y * 8.0 + t * 1.3) - 0.5);
  float gk = 1.0 + VHS_KICK_GLITCH * kick;
  uv.x += VHS_WOBBLE * k * jit * gk;
  uv.y += VHS_KICK_JUMP * k * kick * kick;
  float by = fract(1.0 - t * VHS_BAND_SPEED);
  float band = smoothstep(VHS_BAND_WIDTH, 0.0, abs(uv.y - by)) * k;
  uv.x += VHS_BAND_TEAR * band * gk * (h11(line + t) - 0.5);
  float hs = smoothstep(VHS_HEADSWITCH * k, 0.0, uv.y) * step(0.001, k);
  uv.x += hs * 0.05 * (h11(line * 3.1 + t * 60.0) - 0.3);
  vec3 y = rgb2yiq(camAt(uv));
  vec3 ch = vec3(0.0);
  for (int i = 0; i < 6; i++) {
    float o = VHS_CHROMA_SHIFT * k * (1.0 + VHS_MIDS_SMEAR * mids) + VHS_CHROMA_BLUR * k * (float(i) / 5.0 - 0.5);
    ch += rgb2yiq(camAt(uv - vec2(o, 0.0)));
  }
  ch /= 6.0;
  vec3 c = yiq2rgb(vec3(y.x, ch.yz * (1.0 - VHS_DESAT * k)));
  float g = h21(px + fract(t * 7.0) * 100.0);
  c += (g - 0.5) * VHS_NOISE * k;
  c = mix(c, vec3(g), band * 0.55);
  c = mix(c, vec3(g * 0.9), hs * 0.8);
  c *= 1.0 - 0.15 * k * (0.5 + 0.5 * sin(uv.y * RENDERSIZE.y * 1.5));
  return clamp(c, 0.0, 1.0);
}

// ── mode 3 · night vision (FX 87) — grab these first ────────────────
const vec3  NV_PHOSPHOR = vec3(0.35, 1.0, 0.3);
const float NV_GAIN     = 1.8;
const float NV_GRAIN    = 0.22;
const float NV_SCAN     = 0.12;
const float NV_SCAN_PX  = 3.0;
const float NV_VIGNETTE = 0.62;
const float NV_FLICKER  = 0.05;
const float NV_KICK_GAIN  = 0.8;
const float NV_KICK_GRAIN = 1.5;
const float NV_KICK_ENGAGE = 0.8;
const float NV_MIDS_ENGAGE = 0.12;
const float NV_MIDS_TUBE  = 0.12;

float hash3(vec3 p) { return fract(sin(dot(p, vec3(12.9898, 78.233, 37.719))) * 43758.5453); }

vec3 nightVision(vec2 uv, vec2 px) {
  float kick = kickNow(); float mids = midsNow();
  float fx = engage(NV_KICK_ENGAGE, NV_MIDS_ENGAGE, 0.0);
  vec3 base = camAt(uv);
  float l = lumaOf(base) * NV_GAIN * (1.0 + NV_KICK_GAIN * kick);
  l = l / (1.0 + l * 0.35);
  float n = hash3(vec3(floor(px * 0.5), floor(TIME * 30.0))) - 0.5;
  l += NV_GRAIN * (1.0 + NV_KICK_GRAIN * kick) * n * fx;
  l *= 1.0 - NV_SCAN * (0.5 + 0.5 * sin(6.2831853 * px.y / NV_SCAN_PX));
  l *= 1.0 - NV_FLICKER * hash3(vec3(floor(TIME * 24.0)));
  vec3 nv = NV_PHOSPHOR * l + vec3(0.02, 0.06, 0.02) * fx;
  float aspect = RENDERSIZE.x / RENDERSIZE.y;
  float r = length((uv - 0.5) * vec2(aspect, 1.0));
  float rad = mix(1.5, NV_VIGNETTE + NV_MIDS_TUBE * mids, fx);
  nv *= smoothstep(rad, rad - 0.18, r);
  return mix(base, nv, smoothstep(0.0, 0.35, fx));
}

// ── mode 4 · hologram (FX 92) — grab these first ────────────────────
const vec3  HOLO_COLOR    = vec3(0.25, 0.95, 1.0);
const float HOLO_OPACITY  = 0.65;
const float HOLO_LINE_PX  = 3.0;
const float HOLO_LINE_DARK = 0.45;
const float HOLO_ROLL     = 0.25;
const float HOLO_FLICKER  = 0.18;
const float HOLO_RIM      = 1.2;
const float HOLO_RIM_PX   = 8.0;
const float HOLO_GLITCH   = 0.03;
const float HOLO_GLITCH_P = 0.06;
const float HOLO_KICK_ENGAGE = 0.9;
const float HOLO_MIDS_ENGAGE = 0.25;
const float HOLO_KICK_GLITCH = 5.0;
const float HOLO_KICK_FLASH  = 0.6;
const float HOLO_MIDS_ROLL   = 1.5;

float hashB(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }

vec3 hologram(vec2 uv, vec2 px) {
  float kb = kickNow(); float mids = midsNow();
  float k = engage(HOLO_KICK_ENGAGE, HOLO_MIDS_ENGAGE, 0.0);
  float t = TIME;
  float slice = floor(uv.y * 60.0);
  float st = floor(t * 14.0) + 13.0 * floor(kb * 3.0);
  vec2 huv = uv;
  float glitch = step(hashB(vec2(slice, st)), HOLO_GLITCH_P * k * (1.0 + HOLO_KICK_GLITCH * kb));
  huv.x += glitch * (hashB(vec2(slice, st + 1.0)) - 0.5) * 2.0 * HOLO_GLITCH * k * (1.0 + kb);
  huv = clamp(huv, 0.0, 1.0);
  float fig = figureAt(huv);
  vec3 cam = camAt(huv);
  float l = lumaOf(cam);
  vec3 holo = HOLO_COLOR * (0.25 + 1.1 * l);
  float scan = 1.0 - HOLO_LINE_DARK * step(0.5, fract(px.y / HOLO_LINE_PX));
  float roll = smoothstep(0.92, 1.0, fract(uv.y - t * HOLO_ROLL)) * 0.6 * (1.0 + HOLO_MIDS_ROLL * mids);
  float flick = 1.0 - HOLO_FLICKER * (0.5 + 0.5 * sin(t * 47.0)) * step(0.7, hashB(vec2(floor(t * 20.0), 3.0)));
  holo = (holo * scan + HOLO_COLOR * roll) * flick * (1.0 + HOLO_KICK_FLASH * kb);
  vec2 tx = HOLO_RIM_PX / RENDERSIZE;
  float room = 0.0;
  for (int i = 0; i < 8; i++) {
    float a = float(i) * 0.7854;
    room += 1.0 - figureAt(clamp(huv + tx * vec2(cos(a), sin(a)), 0.0, 1.0));
  }
  float rim = room / 8.0;
  // see-through: the room plate stands where the figure blocks the room
  vec3 holoOver = roomAt(uv) * (1.0 - HOLO_OPACITY) + holo * HOLO_OPACITY + HOLO_COLOR * HOLO_RIM * rim;
  vec3 inFig = mix(cam, holoOver, k);
  vec3 haze = HOLO_COLOR * 0.25 * k * (1.0 - rim) * (1.0 - fig);
  return mix(camAt(uv) + haze, inFig, fig);
}

// ── mode 5 · hat static (FX 180) — grab these first ─────────────────
const float ST_STATIC_MAX  = 0.85;
const float ST_CHROMA      = 0.55;
const float ST_SMEAR_UV    = 0.012;
const float ST_GRAIN_PX    = 2.0;
const float ST_HIGHS_FINE  = 0.6;
const float ST_HIGHS_ENGAGE = 1.0;
const float ST_KICK_ENGAGE = 0.45;
const float ST_MIDS_ENGAGE = 0.15;
const float ST_LINE_BURSTS = 0.7;

vec3 hatStatic(vec2 uv, vec2 px) {
  float kick = kickNow(); float hi = highsNow();
  float amt = engage(ST_KICK_ENGAGE, ST_MIDS_ENGAGE, ST_HIGHS_ENGAGE);
  vec3 room = srcAt(uv);
  float t = floor(TIME * 60.0);
  float g = ST_GRAIN_PX * (1.0 - ST_HIGHS_FINE * hi) * (1.0 + 2.0 * kick);
  vec2 cell = floor(px / max(g, 1.0));
  float lum = h31(vec3(cell, t));
  vec3 chroma = vec3(h31(vec3(cell, t + 17.0)), h31(vec3(cell, t + 41.0)), h31(vec3(cell, t + 73.0))) - 0.5;
  float row = h31(vec3(0.0, floor(px.y / 3.0), t)) - 0.5;
  vec2 sh = vec2(row * ST_SMEAR_UV * amt * (1.0 + 2.0 * hi), 0.0);
  vec3 smeared = vec3(srcAt(clamp(uv + sh, 0.0, 1.0)).r, room.g, srcAt(clamp(uv - sh, 0.0, 1.0)).b);
  float line = step(1.0 - 0.08 * hi, h31(vec3(1.0, floor(px.y / 2.0), t))) * ST_LINE_BURSTS;
  vec3 snow = vec3(lum) + chroma * ST_CHROMA * 2.0;
  room = mix(smeared, snow, ST_STATIC_MAX * amt * (0.55 + 0.45 * lum));
  room += chroma * ST_CHROMA * amt * 0.6 + line * amt * 0.5;
  return mix(camAt(uv), room, step(0.002, amt));
}

// ── mode 6 · hat scanlines (FX 212) — grab these first ──────────────
const float SL_LINE_PX     = 3.0;
const float SL_DISP_UV     = 0.08;
const float SL_RIPPLE_UV   = 0.006;
const float SL_HIGHS_ENGAGE = 1.0;
const float SL_HIGHS_LINES = 0.85;
const float SL_BASE_LINES  = 0.25;
const float SL_REROLL_HZ   = 30.0;
const float SL_KICK_ENGAGE = 0.5;
const float SL_KICK_BAND   = 0.12;
const float SL_MIDS_ENGAGE = 0.15;
const float SL_GAP_DARK    = 0.35;
const float SL_CHROMA_SLIP = 0.3;

float h21b(vec2 p) { p = fract(p * vec2(123.34, 456.21)); p += dot(p, p + 45.32); return fract(p.x * p.y); }

vec3 hatScanlines(vec2 uv, vec2 px) {
  float kick = kickNow(); float hi = highsNow();
  float amt = engage(SL_KICK_ENGAGE, SL_MIDS_ENGAGE, SL_HIGHS_ENGAGE);
  float line = floor(px.y / SL_LINE_PX);
  float t = floor(TIME * SL_REROLL_HZ);
  float share = mix(SL_BASE_LINES, SL_HIGHS_LINES, clamp(hi, 0.0, 1.0));
  float pick = step(h21b(vec2(line, t)), share);
  float dir = h21b(vec2(line * 1.7, t + 9.0)) * 2.0 - 1.0;
  float dx = pick * dir * SL_DISP_UV * amt * (0.4 + 0.6 * hi);
  dx += sin(uv.y * 180.0 + TIME * 14.0) * SL_RIPPLE_UV * amt;
  float bandY = h21b(vec2(floor(TIME * 4.0), 3.3));
  float inBand = step(abs(uv.y - bandY), SL_KICK_BAND * 0.5);
  dx += inBand * kick * kickAmt * SL_KICK_ENGAGE * SL_DISP_UV * 2.0 * sign(dir + 0.001);
  float sl = SL_CHROMA_SLIP * dx;
  vec3 room = vec3(srcAt(vec2(fract(uv.x + dx + sl), uv.y)).r,
                   srcAt(vec2(fract(uv.x + dx), uv.y)).g,
                   srcAt(vec2(fract(uv.x + dx - sl), uv.y)).b);
  float gap = smoothstep(0.55, 1.0, fract(px.y / SL_LINE_PX));
  room *= 1.0 - SL_GAP_DARK * amt * gap;
  return mix(camAt(uv), room, step(0.002, amt));
}

// ── mode 7 · hat grain (FX 255) — grab these first ──────────────────
const float HG_HI_GAIN    = 1.2;
const float HG_GRAIN      = 0.28;
const float HG_GRAIN_PX   = 2.0;
const float HG_SPARK_DENS = 0.012;
const float HG_SPARK_CELL = 9.0;
const float HG_SPARK_LIFE = 0.18;
const float HG_SPARK_GAIN = 1.8;
const float HG_PRESS_DENS = 3.0;
const float HG_KICK_ENGAGE = 0.5;
const float HG_MIDS_WARM  = 0.5;

vec3 hatGrain(vec2 uv, vec2 px) {
  float mids = midsNow();
  float amt = engage(HG_KICK_ENGAGE, 0.0, HG_HI_GAIN);
  vec3 cam = camAt(uv);
  float seed = hatCount();
  vec2 g = floor(px / HG_GRAIN_PX);
  float n = hashB(g + seed * 17.13 + floor(TIME * 4.0) * 0.37) - 0.5;
  n += 0.5 * (hashB(g * 1.7 + seed * 3.1 + 5.0) - 0.5);
  vec3 tint = mix(vec3(1.0), vec3(1.15, 0.95, 0.7), HG_MIDS_WARM * mids);
  cam += n * HG_GRAIN * amt * tint * (0.4 + lumaOf(cam));
  vec2 cell = floor(px / HG_SPARK_CELL);
  float pick = hashB(cell + seed * 7.77);
  float dens = HG_SPARK_DENS * (1.0 + HG_PRESS_DENS * press()) * amt;
  vec2 c = (cell + 0.2 + 0.6 * vec2(hashB(cell + seed), hashB(cell - seed))) * HG_SPARK_CELL;
  vec2 d = abs(px - c);
  float star = max(exp(-d.x * 0.9) * exp(-d.y * 0.35), exp(-d.y * 0.9) * exp(-d.x * 0.35));
  float life = max(exp(-hatAge() / HG_SPARK_LIFE), 0.6 * press() * (0.5 + 0.5 * sin(TIME * 8.0 + pick * 300.0)));
  float bright = smoothstep(0.3, 0.8, lumaOf(cam));
  cam += step(pick, dens) * HG_SPARK_GAIN * star * life * (0.3 + bright) * vec3(1.0, 0.97, 0.9);
  return mix(camAt(uv), cam, step(0.002, amt));
}

void main() {
  vec2 uv = isf_FragNormCoord;
  if (PASSINDEX == 0.0) { gl_FragColor = sigPass(); return; }
  if (PASSINDEX == 1.0) { gl_FragColor = depthPass(uv); return; }
  if (PASSINDEX == 2.0) { gl_FragColor = hatPass(); return; }
  if (PASSINDEX == 3.0) { gl_FragColor = platePass(uv); return; }

  vec2 px = (isf_FragNormCoord * RENDERSIZE);
  float m = floor(mode + 0.5);
  vec3 cam = camAt(uv);
  vec3 col = cam;
  if (m < 0.5) col = crt(uv, px);
  else if (m < 1.5) col = film(uv, px);
  else if (m < 2.5) col = vhs(uv, px);
  else if (m < 3.5) col = nightVision(uv, px);
  else if (m < 4.5) { gl_FragColor = vec4(mix(cam, hologram(uv, px), wet()), 1.0); return; }
  else if (m < 5.5) col = hatStatic(uv, px);
  else if (m < 6.5) col = hatScanlines(uv, px);
  else col = hatGrain(uv, px);
  gl_FragColor = vec4(mix(cam, col, regionAt(uv) * wet()), 1.0);
}
