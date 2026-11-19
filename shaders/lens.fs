/*{
  "DESCRIPTION": "audiodev lens: blur and focus, depth-aware. Gaussian blur / rack focus / dreamy figure, pressure breath, mids focus pull, tilt-shift, zoom blur, motion blur, hex bokeh, depth fog, smoke room. `where` picks the layer that goes soft (0 all, 1 figure, 2 room).",
  "CREDIT": "simply-creative FX 1, 55, 114, 115, 148, 190, 197, 227, 257, 287, 300, ported to tussen ISF",
  "CATEGORIES": ["blur", "depth", "sound", "beat", "modes", "pressure"],
  "INPUTS": [
    { "NAME": "inputImage", "TYPE": "image" },
    { "NAME": "depth", "TYPE": "depth" },
    { "NAME": "wave", "TYPE": "audio" },
    { "NAME": "spectrum", "TYPE": "audioFFT" },
    { "NAME": "amount", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 1.0 },
    { "NAME": "react", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5, "DESCRIPTION": "how much the music moves it: 0 holds it still, 0.5 as tuned, 1 twice as much" },
    { "NAME": "mode", "TYPE": "long", "MIN": 0, "MAX": 8, "DEFAULT": 0,
      "VALUES": [0, 1, 2, 3, 4, 5, 6, 7, 8],
      "LABELS": ["blur / rack focus", "breath", "focus pull", "tilt-shift", "zoom blur", "motion blur", "bokeh", "depth fog", "smoke room"] },
    { "NAME": "where", "TYPE": "long", "MIN": 0, "MAX": 2, "DEFAULT": 0, "DESCRIPTION": "what goes soft: 0 everything · 1 the figure (near) · 2 the room (far)" },
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
    { "TARGET": "state", "WIDTH": "2", "HEIGHT": "1", "FLOAT": true, "PERSISTENT": true },
    { "TARGET": "flow", "WIDTH": "$WIDTH / 4", "HEIGHT": "$HEIGHT / 4", "FLOAT": true, "PERSISTENT": true },
    { "TARGET": "layers", "WIDTH": "$WIDTH", "HEIGHT": "$HEIGHT / 2", "FLOAT": true },
    {}
  ]
}*/

// mode 0 · blur          FX 1 Blur (where 0) · FX 55 Rack focus (where 2) · FX 300 Pressure blur figure (where 1)
// mode 1 · breath        FX 197 Press-breath blur (where 2 = the original, room only)
// mode 2 · focus pull    FX 227 Mids depth-of-field pull
// mode 3 · tilt-shift    FX 287 Mids tilt-shift
// mode 4 · zoom blur     FX 114 Zoom blur
// mode 5 · motion blur   FX 115 Motion blur (own optical flow, one frame back)
// mode 6 · bokeh         FX 148 Bokeh room (where 2 = the original)
// mode 7 · depth fog     FX 190 Depth fog
// mode 8 · smoke room    FX 257 Mids smoke room
//
// How the blur works: every blur mode asks radii(uv) for two radii, the
// room layer's and the figure layer's. The `layers` pass blurs each layer
// horizontally at half size (left half of the buffer = the room, right half
// = the figure), each weighted by its own depth mask so the figure never
// smears into the room (normalized convolution); the last pass blurs
// vertically and lays the figure over the room. On the luma fallback the
// "figure" is whatever is bright; the ML map makes the layers real.

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
// FX 1 blur (radii in frame heights; Chris's 48 px read at 1080p)
const float BLUR_MAX     = 0.044;  // radius at amount 1
const float BLUR_CURVE   = 1.6;
const float BLUR_PUNCH   = 0.5;    // kick widens the radius ×(1 + this)
// FX 55 rack focus
const float RACK_MAX     = 0.037;  // room radius at amount 1 (40 px)
const float RACK_PUNCH   = 0.7;
const float BG_DIM       = 0.15;   // the room darkens a touch as it defocuses
const float FIG_POP      = 0.06;   // figure contrast lift as focus racks
// FX 300 dreamy figure (never built: from the gallery line)
const float DREAM_MAX    = 0.03;
const float DREAM_GLOW   = 0.35;   // the soft figure glows
// FX 197 breath
const float BREATH_MAX   = 0.035;  // radius at full exhale
const float BREATH_GAIN  = 2.2;    // pressure velocity → breath
const float BREATH_SETTLE = 0.9;   // s back to rest
const float SHARPEN      = 2.2;    // unsharp gain at full inhale
const float FOCUS_POP    = 0.12;
const float PRESS_DEPTH  = 0.6;    // held pressure deepens the breath
// FX 227 focus pull
const float PULL_MAX     = 0.022;
const float PULL_BREATHE = 0.012;  // lens breathing: the room scales as focus moves
const float PULL_LO      = 0.25;   // mids below → figure sharp
const float PULL_HI      = 0.75;   // mids above → room sharp
const float PULL_GLIDE   = 0.25;   // s
const float PULL_BOKEH   = 0.35;   // highlights bloom in the soft layer
// FX 287 tilt-shift
const float BAND_LO      = 0.25;
const float BAND_HI      = 0.75;
const float BAND_HALF    = 0.08;
const float BAND_RAMP    = 0.3;
const float TILT_MAX     = 0.025;
const float TILT_SAT     = 0.6;
const float TILT_CONTRAST = 0.25;
// FX 114 zoom blur
const float ZOOM_STRENGTH = 0.35;
const float ZOOM_CURVE   = 1.4;
const float ZOOM_PUNCH   = 0.6;
const float ZOOM_WANDER  = 0.08;
const float ZOOM_GLOW    = 0.15;
// FX 115 motion blur
const float SMEAR_S      = 0.25;   // s of motion the smear covers
const float FLOW_MAX     = 2.0;    // uv/s
const float STILL        = 0.03;   // flow / FLOW_MAX below which nothing smears
const float GHOST        = 0.0;    // 0 centred · 1 trail behind
const float SMEAR_STRETCH = 0.8;
const float FLOW_SMOOTH  = 0.75;
// FX 148 bokeh
const float BOKEH_MAX    = 0.045;
const float HI_LO        = 0.55;
const float HI_GAIN      = 6.0;
const float BOKEH_ROT    = 0.26;
const float KICK_FLARE   = 1.0;
const float MIDS_FLARE   = 0.8;
// FX 190 depth fog (depth here is "far-ness" 0 near .. 1 far, as in Chris's)
const float FOG_BEFORE   = 0.12;   // fog starts this far in front of the cut
const float DENS_MIN     = 1.5;
const float DENS_MAX     = 9.0;
const float FOG_SCATTER  = 0.02;   // the fogged room goes soft
const float FOG_DRIFT    = 0.35;
const float FOG_SPEED    = 0.05;
const float KICK_SWALLOW = 0.25;
const float MIDS_DRIFT   = 2.0;
const vec3  HAZE         = vec3(0.62, 0.66, 0.72);  // the fog itself (was the NDI)
const float HAZE_MIX     = 0.55;
// FX 257 smoke
const float SMOKE_MIN    = 0.15;
const float SMOKE_MAX    = 0.95;
const float SMOKE_PRESS  = 0.4;    // pressure adds standing density
const float SMOKE_SCALE  = 2.6;
const vec3  SMOKE_LIT    = vec3(0.86, 0.88, 0.95);
const vec3  SMOKE_DARK   = vec3(0.25, 0.27, 0.33);
const float SMOKE_TINT   = 0.35;
const float SMOKE_KICK   = 0.4;
const float SMOKE_BASE   = 0.06;   // drift phase speed
const float SMOKE_MIDS   = 0.45;
const float SMOKE_KICKS  = 0.6;
const float SMOKE_DEEP   = 0.4;    // farther room gets thicker smoke
// layers
const float FILL         = 0.03;   // room weight left inside the figure (fills the hole)
// ──────────────────────────────────────────────────────────────────

float modeN() { return floor(mode + 0.5); }
float whereN() { return floor(where + 0.5); }
float engage() { return clamp(amount + kickAmt * kickNow() + midsAmt * midsNow(), 0.0, 1.0); }
float aspectIn() { vec2 s = IMG_SIZE(inputImage); return s.x / max(s.y, 1.0); }
vec3 camAt(vec2 uv) { return IMG_NORM_PIXEL(inputImage, clamp(uv, 0.0, 1.0)).rgb; }

float h21(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vnoise(vec2 p) {
  vec2 i = floor(p); vec2 u = fract(p); u = u * u * (3.0 - 2.0 * u);
  return mix(mix(h21(i), h21(i + vec2(1.0, 0.0)), u.x), mix(h21(i + vec2(0.0, 1.0)), h21(i + vec2(1.0, 1.0)), u.x), u.y);
}

// ── state: 2×1, fp16-safe (no absolute times) ──
//   px0: r last amount · g pressure velocity · b breath · a fract(TIME)
//   px1: r focus glide · g smoke phase (whole) · b smoke phase (fraction)
vec4 stA() { return IMG_NORM_PIXEL(state, vec2(0.25, 0.5)); }
vec4 stB() { return IMG_NORM_PIXEL(state, vec2(0.75, 0.5)); }
vec4 statePass() {
  vec4 a = stA(); vec4 b = stB();
  float first = step(FRAMEINDEX, 0.5);
  float dt = clamp(fract(TIME - a.a), 1.0 / 240.0, 0.1);
  if (isf_FragNormCoord.x < 0.5) {
    float v = (amount - a.r) / dt;
    float pv = mix(a.g, clamp(v / 4.0, -1.0, 1.0), 0.25) * (1.0 - first);
    float br = a.b * (1.0 - first);
    float target = clamp(-pv * BREATH_GAIN, -1.0, 1.0);
    if (abs(target) > abs(br) || sign(target) != sign(br)) br = mix(br, target, abs(target) > 0.05 ? 0.35 : 0.0);
    br *= exp(-dt / BREATH_SETTLE);
    return vec4(amount, pv, br, fract(TIME));
  }
  float tgt = clamp((midsNow() - PULL_LO) / (PULL_HI - PULL_LO), 0.0, 1.0);
  float f = b.r + (1.0 - exp(-dt / PULL_GLIDE)) * (tgt - b.r);
  float ph = b.b * (1.0 - first) + dt * (SMOKE_BASE + SMOKE_MIDS * midsNow() + SMOKE_KICKS * kickNow());
  float whole = b.g * (1.0 - first) + floor(ph);
  return vec4(f, mod(whole, 2048.0), fract(ph), 1.0);
}
float breathNow() { return stA().b; }
float focusNow() { float f = clamp(stB().r, 0.0, 1.0); return f * f * (3.0 - 2.0 * f); }
float smokePhase() { vec4 b = stB(); return b.g + b.b; }

// ── flow (FX 115): Lucas–Kanade on luma, now vs one frame back, at ¼ size ──
//   rg flow in uv/s · b this frame's luma (next frame's past) · a fract(TIME)
vec4 flowPass(vec2 uv) {
  vec4 me = IMG_NORM_PIXEL(flow, uv);
  float l0 = lumaOf(camAt(uv));
  if (modeN() != 5.0) return vec4(0.0, 0.0, l0, fract(TIME));
  vec2 st = 1.0 / RENDERSIZE;
  float a = 0.0; float b = 0.0; float c = 0.0; float p = 0.0; float q = 0.0;
  for (int j = -2; j <= 2; j++) {
    for (int i = -2; i <= 2; i++) {
      vec2 o = uv + vec2(float(i), float(j)) * st;
      float ix = (lumaOf(camAt(o + vec2(st.x, 0.0))) - lumaOf(camAt(o - vec2(st.x, 0.0)))) * 0.5;
      float iy = (lumaOf(camAt(o + vec2(0.0, st.y))) - lumaOf(camAt(o - vec2(0.0, st.y)))) * 0.5;
      float it = lumaOf(camAt(o)) - IMG_NORM_PIXEL(flow, o).b;
      a += ix * ix; b += ix * iy; c += iy * iy; p += ix * it; q += iy * it;
    }
  }
  a += 0.002; c += 0.002;
  float det = a * c - b * b;
  vec2 v = vec2(0.0);
  if (det > 1e-7) v = -vec2(c * p - b * q, a * q - b * p) / det;
  float dt = clamp(fract(TIME - me.a), 1.0 / 240.0, 0.1);
  v *= st / dt;
  v = clamp(v, -FLOW_MAX, FLOW_MAX) * clamp(det * 4000.0, 0.0, 1.0);
  vec2 pv = FRAMEINDEX < 2.0 ? vec2(0.0) : me.rg;
  v = mix(v, pv, FLOW_SMOOTH);
  return vec4(v, l0, fract(TIME));
}

// ── depth fog amount (FX 190), shared by the layers pass and the look ──
float fogAt(vec2 uv) {
  float k = kickNow(); float m = midsNow();
  float far = 1.0 - depthAt(uv);
  float tt = TIME * FOG_SPEED * (1.0 + MIDS_DRIFT * m);
  float n = vnoise(uv * vec2(3.0, 5.0) + vec2(tt * 3.0, tt)) * 0.6 + vnoise(uv * 9.0 - tt * 5.0) * 0.4;
  float dens = mix(DENS_MIN, DENS_MAX, amount) * (1.0 + FOG_DRIFT * (n - 0.5) * 2.0);
  float start = (1.0 - figure) - FOG_BEFORE - KICK_SWALLOW * k;
  return 1.0 - exp(-dens * max(far - start, 0.0));
}

// ── the blur radii: x room layer, y figure layer (frame heights), z 1 = layers apart ──
vec3 radii(vec2 uv) {
  float md = modeN(); float w = whereN();
  float k = kickNow(); float m = midsNow();
  float amt = engage();
  float r = 0.0;
  if (md == 0.0) {
    if (w == 1.0) return vec3(0.0, DREAM_MAX * pow(amt, 1.2) * (1.0 + BLUR_PUNCH * k), 1.0);
    if (w == 2.0) return vec3(RACK_MAX * amt * (1.0 + RACK_PUNCH * k), 0.0, 1.0);
    r = BLUR_MAX * pow(amt, BLUR_CURVE) * (1.0 + BLUR_PUNCH * k);
  } else if (md == 1.0) {
    float ex = clamp(max(breathNow(), 0.0) * (1.0 + PRESS_DEPTH * amount) + kickAmt * k + midsAmt * m, 0.0, 1.0);
    r = ex > 0.002 ? BREATH_MAX * ex * ex + 0.002 : 0.0;
  } else if (md == 2.0) {
    float f = focusNow();
    return vec3(PULL_MAX * amt * (1.0 - f), PULL_MAX * amt * f, 1.0);
  } else if (md == 3.0) {
    float yc = mix(BAND_LO, BAND_HI, m);
    float dist = max(abs(uv.y - yc) - BAND_HALF, 0.0);
    r = TILT_MAX * amt * smoothstep(0.0, BAND_RAMP, dist);
  } else if (md == 7.0) {
    return vec3(FOG_SCATTER * fogAt(uv) * amt, 0.0, 1.0);
  }
  // whole-picture blurs: `where` keeps one layer sharp
  return vec3(w == 1.0 ? 0.0 : r, w == 2.0 ? 0.0 : r, w > 0.5 ? 1.0 : 0.0);
}

// ── the layers pass: horizontal half of each layer's blur ──
vec4 layersPass(vec2 bufUv) {
  float side = step(0.5, bufUv.x);
  vec2 uv = vec2(fract(bufUv.x * 2.0), bufUv.y);
  vec3 rr = radii(uv);
  float r = side > 0.5 ? rr.y : rr.x;
  if (r < 0.0006) return vec4(0.0);
  float md = modeN();
  vec2 stepv = vec2(r / 6.0 / aspectIn(), 0.0);
  vec4 acc = vec4(0.0); float wsum = 0.0;
  for (int i = -6; i <= 6; i++) {
    float x = float(i) / 3.0;
    float w = exp(-0.5 * x * x);
    vec2 s = uv + float(i) * stepv;
    vec3 c = camAt(s);
    float f = figureAt(s);
    float msk = side > 0.5 ? f : (rr.z > 0.5 ? max(1.0 - f, FILL) : 1.0);
    if (md == 2.0) msk *= 1.0 + PULL_BOKEH * 4.0 * max(lumaOf(c) - 0.7, 0.0);
    acc += w * vec4(c * msk, msk);
    wsum += w;
  }
  return acc / wsum;
}

// ── the last pass: vertical half; returns premultiplied colour + coverage ──
vec4 layerV(float side, vec2 uv, float r) {
  vec2 sz = IMG_SIZE(layers);
  float hx = 1.0 / max(sz.x, 1.0);
  vec2 stepv = vec2(0.0, r / 6.0);
  vec4 acc = vec4(0.0); float wsum = 0.0;
  for (int i = -6; i <= 6; i++) {
    float x = float(i) / 3.0;
    float w = exp(-0.5 * x * x);
    vec2 s = uv + float(i) * stepv;
    vec2 b = vec2(clamp(s.x, hx, 1.0 - hx) * 0.5 + 0.5 * side, clamp(s.y, 0.0, 1.0));
    acc += w * IMG_NORM_PIXEL(layers, b);
    wsum += w;
  }
  return acc / wsum;
}
float sharpToSoft(float r) { return smoothstep(0.0006, 0.004, r); }

vec3 disc(vec2 uv, float r, int taps) {
  float aspect = aspectIn();
  vec3 acc = vec3(0.0);
  for (int i = 0; i < 28; i++) {
    if (i >= taps) break;
    float t = (float(i) + 0.5) / float(taps);
    float a = float(i) * 2.39996;
    vec2 o = vec2(cos(a), sin(a)) * sqrt(t) * r;
    acc += camAt(uv + vec2(o.x / aspect, o.y));
  }
  return acc / float(taps);
}

vec3 layered(vec2 uv, vec3 cam) {
  float md = modeN(); float w = whereN();
  float amt = engage();
  vec3 rr = radii(uv);
  vec2 ruv = uv;
  if (md == 2.0) ruv = (uv - 0.5) * (1.0 - PULL_BREATHE * focusNow() * amt) + 0.5;
  vec3 room = cam;
  if (rr.x > 0.0006) {
    vec4 L = layerV(0.0, ruv, rr.x);
    room = mix(cam, L.rgb / max(L.a, 1e-4), sharpToSoft(rr.x));
  }
  vec3 fig = cam;
  if (md == 0.0 && w == 2.0) {                     // FX 55
    room *= 1.0 - BG_DIM * amt;
    fig = (cam - 0.5) * (1.0 + FIG_POP * amt) + 0.5;
  }
  if (md == 1.0) {                                 // FX 197: the exhale glows, the inhale bites
    float br = breathNow();
    float ex = clamp(max(br, 0.0) * (1.0 + PRESS_DEPTH * amount) + kickAmt * kickNow() + midsAmt * midsNow(), 0.0, 1.0);
    room *= 1.0 + 0.15 * ex;
    float inhale = clamp(max(-br, 0.0) * (1.0 + PRESS_DEPTH * amount), 0.0, 1.0);
    if (inhale > 0.002) {
      vec3 sf = disc(uv, 0.006, 8);
      vec3 sh = cam + SHARPEN * inhale * (cam - sf);
      sh = clamp(0.5 + (sh - 0.5) * (1.0 + FOCUS_POP * inhale), 0.0, 1.5);
      room = mix(room, sh, regionAt(uv));
    }
  }
  vec3 col = room;
  if (rr.z > 0.5) {
    float a = figureAt(uv);
    if (rr.y > 0.0006) {
      vec4 F = layerV(1.0, uv, rr.y);
      float s = sharpToSoft(rr.y);
      fig = mix(fig, F.rgb / max(F.a, 1e-4), s);
      a = mix(a, F.a, s);
      if (md == 0.0 && w == 1.0) fig = fig * (1.0 + DREAM_GLOW * amt) + DREAM_GLOW * 0.25 * amt;  // FX 300 glow
    }
    col = mix(room, fig, a);
  }
  if (md == 3.0 && amt > 0.002) {                  // FX 287 toy look
    float l = lumaOf(col);
    col = mix(vec3(l), col, 1.0 + TILT_SAT * amt);
    col = clamp(0.5 + (col - 0.5) * (1.0 + TILT_CONTRAST * amt), 0.0, 1.0);
  }
  if (md == 7.0) {                                 // FX 190: far dissolves into the haze
    float fog = fogAt(uv);
    vec3 fc = mix(room * 0.85 + 0.12, HAZE, HAZE_MIX * fog);
    col = mix(cam, fc, clamp(fog * amt, 0.0, 1.0) * (1.0 - figureAt(uv) * (1.0 - amt)));
  }
  return col;
}

vec3 zoomBlur(vec2 uv, vec3 cam) {                 // FX 114
  float k = kickNow(); float m = midsNow();
  float amt = pow(engage(), ZOOM_CURVE) * (1.0 + ZOOM_PUNCH * k);
  vec2 c = vec2(0.5) + ZOOM_WANDER * m * vec2(sin(TIME * 0.7), cos(TIME * 0.53));
  vec2 d = (c - uv) * ZOOM_STRENGTH * amt;
  float j = fract(sin(dot((isf_FragNormCoord * RENDERSIZE), vec2(12.9898, 78.233))) * 43758.5453);
  vec3 acc = vec3(0.0); float wsum = 0.0;
  for (int i = 0; i < 28; i++) {
    float t = (float(i) + j) / 28.0;
    float w = 1.0 - 0.6 * t;
    acc += w * camAt(uv + d * t);
    wsum += w;
  }
  return mix(cam, acc / wsum * (1.0 + ZOOM_GLOW * amt), regionAt(uv));
}

vec3 motionBlur(vec2 uv, vec3 cam) {               // FX 115
  float amt = engage();
  vec2 fl = IMG_NORM_PIXEL(flow, uv).rg;
  fl *= smoothstep(STILL, STILL * 2.5, length(fl) / FLOW_MAX);
  vec2 len = fl * SMEAR_S * amt * (1.0 + SMEAR_STRETCH * kickNow());
  vec3 acc = vec3(0.0); float w = 0.0;
  for (int i = 0; i < 16; i++) {
    float t = float(i) / 15.0;
    float s = mix(t - 0.5, -t, GHOST);
    float wi = 1.0 - 0.5 * t;
    acc += camAt(uv + len * s) * wi; w += wi;
  }
  return mix(cam, acc / w, smoothstep(0.0, 0.05, amt) * regionAt(uv));
}

vec3 bokeh(vec2 uv, vec3 cam) {                    // FX 148: hex aperture, highlights bloom
  float amt = engage();
  if (amt <= 0.001) return cam;
  float k = kickNow(); float m = midsNow(); float w = whereN();
  float aspect = aspectIn();
  float sp = BOKEH_MAX * amt / 4.0;
  float cs = cos(BOKEH_ROT); float sn = sin(BOKEH_ROT);
  vec2 a1 = vec2(cs, sn); vec2 a2 = vec2(0.5 * cs - 0.8660254 * sn, 0.5 * sn + 0.8660254 * cs);
  vec2 jit = (vec2(h21((isf_FragNormCoord * RENDERSIZE)), h21((isf_FragNormCoord * RENDERSIZE) + 17.3)) - 0.5) * sp;
  float g = HI_GAIN * (1.0 + MIDS_FLARE * m) * (1.0 + KICK_FLARE * k);
  vec3 acc = vec3(0.0); float cnt = 0.0; vec3 all = vec3(0.0); float n = 0.0;
  for (int j = -4; j <= 4; j++) {
    for (int i = -4; i <= 4; i++) {
      if (abs(i + j) > 4) continue;
      vec2 s = uv + ((float(i) * a1 + float(j) * a2) * sp + jit) / vec2(aspect, 1.0);
      vec3 c = camAt(s);
      float msk = 1.0;
      if (w == 2.0) msk = max(1.0 - figureAt(s), FILL);
      if (w == 1.0) msk = max(figureAt(s), FILL);
      vec3 lit = c * (1.0 + g * smoothstep(HI_LO, 1.0, lumaOf(c)));
      acc += msk * lit; cnt += msk;
      all += lit; n += 1.0;
    }
  }
  // few samples of our own layer: lean on the plain average (the other layer glows over)
  vec3 c = mix(all / n, acc / max(cnt, 1e-4), smoothstep(0.1, 0.4, cnt / n));
  c = c / (1.0 + 0.15 * max(c - 1.0, 0.0));
  c = mix(cam, c, smoothstep(0.0, 0.05, amt));
  return mix(cam, c, regionAt(uv));
}

float fbm(vec2 p) {
  float s = 0.0; float a = 0.5;
  for (int i = 0; i < 5; i++) { s += a * vnoise(p); p = p * 2.03 + vec2(1.7, 9.2); a *= 0.5; }
  return s;
}
float smokeAt(vec2 p, float t) {
  vec2 w = vec2(fbm(p + vec2(0.0, t * 0.6)), fbm(p + vec2(5.2, 1.3) - t * 0.4));
  return fbm(p + 1.8 * w + vec2(t, t * 0.35));
}
vec3 smokeRoom(vec2 uv, vec3 cam) {                // FX 257
  float k = kickNow();
  float amt = clamp(amount * SMOKE_PRESS + midsAmt * midsNow() + SMOKE_KICK * k, 0.0, 1.0);
  if (amt <= 0.002) return cam;
  vec2 p = uv * vec2(aspectIn(), 1.0) * SMOKE_SCALE;
  float t = smokePhase();
  float s = smokeAt(p, t);
  float sl = smokeAt(p + vec2(0.0, 0.08), t);      // toward the light, above
  float dens = mix(SMOKE_MIN, SMOKE_MAX, amt) * smoothstep(0.3, 0.75, s + 0.15 * k);
  float lit = clamp(0.55 + (s - sl) * 4.0, 0.0, 1.0);
  vec3 sc = mix(SMOKE_DARK, SMOKE_LIT, lit);
  sc = mix(sc, sc * (0.5 + camAt(uv + (s - 0.5) * 0.1)), SMOKE_TINT);
  float a = clamp(dens * (0.4 + 0.6 * amt), 0.0, 0.95) * smoothstep(0.0, 0.05, amt);
  float f = figureAt(uv);
  // the figure cuts through sharp; the farther the room, the thicker the smoke
  float msk = whereN() == 1.0 ? f : (1.0 - f) * mix(1.0, smoothstep(0.0, 0.7, 1.0 - depthAt(uv)), SMOKE_DEEP);
  return mix(cam, sc, a * msk);
}

void main() {
  vec2 uv = isf_FragNormCoord;
  if (PASSINDEX == 0.0) { gl_FragColor = sigPass(); return; }
  if (PASSINDEX == 1.0) { gl_FragColor = depthPass(uv); return; }
  if (PASSINDEX == 2.0) { gl_FragColor = statePass(); return; }
  if (PASSINDEX == 3.0) { gl_FragColor = flowPass(uv); return; }
  if (PASSINDEX == 4.0) { gl_FragColor = layersPass(uv); return; }

  vec3 cam = IMG_NORM_PIXEL(inputImage, uv).rgb;
  float md = modeN();
  vec3 col = cam;
  if (md == 4.0) col = zoomBlur(uv, cam);
  else if (md == 5.0) col = motionBlur(uv, cam);
  else if (md == 6.0) col = bokeh(uv, cam);
  else if (md == 8.0) col = smokeRoom(uv, cam);
  else col = layered(uv, cam);
  gl_FragColor = vec4(col, 1.0);
}
