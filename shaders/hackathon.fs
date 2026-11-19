/*{
  "DESCRIPTION": "The hackathon look (tussen-composite v01–v21): the gel bench (now in magenta, the past in teal, summed as light), cel bands on the figure riding the hats, and on every kick the figure's outline frozen and dissolving into embers that radiate outward. Highs fringe the frame. Also FX 70 (dual gel), 47 (edge dust), 176/289 (hat glitter/shards), 284 (falling dust).",
  "CREDIT": "Chris Poucet, tussen hackathon session (tussen-composite v01–v21), ported to tussen ISF",
  "CATEGORIES": ["whole-look", "figure", "history", "depth", "sound", "beat", "pressure"],
  "INPUTS": [
    { "NAME": "inputImage", "TYPE": "image" },
    { "NAME": "depth", "TYPE": "depth" },
    { "NAME": "wave", "TYPE": "audio" },
    { "NAME": "spectrum", "TYPE": "audioFFT" },
    { "NAME": "amount", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 1.0, "DESCRIPTION": "the gels (0: plain colour, the bench's GEL)" },
    { "NAME": "react", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5, "DESCRIPTION": "how much the music moves it: 0 holds it still, 0.5 as tuned, 1 twice as much" },
    { "NAME": "fade", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0, "DESCRIPTION": "pressure: the present fades out, the past stays" },
    { "NAME": "cel", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 1.0 },
    { "NAME": "embers", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.8, "DESCRIPTION": "ember opacity (v17–v21)" },
    { "NAME": "outline", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0, "DESCRIPTION": "the live outline drawn in yellow (v12–v16)" },
    { "NAME": "ink", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0, "DESCRIPTION": "the kick's frozen outline as dark ink (v04–v10)" },
    { "NAME": "stream", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0, "DESCRIPTION": "embers peel off the outline all the time, not only on kicks (FX 47 edge dust)" },
    { "NAME": "hatStamp", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0, "DESCRIPTION": "the highs stamp embers too (FX 176, 289)" },
    { "NAME": "spread", "TYPE": "float", "MIN": 0.0, "MAX": 0.3, "DEFAULT": 0.07, "DESCRIPTION": "uv/s radial drift outward from the figure" },
    { "NAME": "lift", "TYPE": "float", "MIN": -0.3, "MAX": 0.3, "DEFAULT": 0.02, "DESCRIPTION": "uv/s upward (negative: falling dust, FX 284)" },
    { "NAME": "emberLife", "TYPE": "float", "MIN": 0.3, "MAX": 6.0, "DEFAULT": 2.4 },
    { "NAME": "reach", "TYPE": "float", "MIN": 0.05, "MAX": 1.4, "DEFAULT": 0.1, "DESCRIPTION": "seconds the past lags with no mids" },
    { "NAME": "midsReach", "TYPE": "float", "MIN": 0.0, "MAX": 1.4, "DEFAULT": 1.2, "DESCRIPTION": "seconds the mids push the past further back" },
    { "NAME": "sizzle", "TYPE": "float", "MIN": 0.0, "MAX": 0.04, "DEFAULT": 0.012, "DESCRIPTION": "radial colour fringe at full highs" },
    { "NAME": "beatSync", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0, "DESCRIPTION": "kicks from the Link beat instead of the sound" },
    { "NAME": "gain", "TYPE": "float", "MIN": 0.25, "MAX": 8.0, "DEFAULT": 1.0 },
    { "NAME": "figure", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5 },
    { "NAME": "soft", "TYPE": "float", "MIN": 0.01, "MAX": 0.3, "DEFAULT": 0.08 },
    { "NAME": "depthFallback", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "depthSmooth", "TYPE": "float", "MIN": 0.0, "MAX": 0.95, "DEFAULT": 0.5 }
  ],
  "PASSES": [
    { "TARGET": "sig", "WIDTH": "2", "HEIGHT": "1", "FLOAT": true, "PERSISTENT": true },
    { "TARGET": "depthBuf", "WIDTH": "$WIDTH / 4", "HEIGHT": "$HEIGHT / 4", "FLOAT": true, "PERSISTENT": true },
    { "TARGET": "ring", "PERSISTENT": true },
    {}
  ]
}*/

// ── grab these first ────────────────────────────────────────────────
// the composite (the gel bench's startup values)
const float SIGMA      = 0.0;    // occlusion; 0 = light sums (the bench start), 1 = over
const float EPSILON    = 0.02;   // soft z-test width
const vec3  GEL_F      = vec3(1.0, 0.239, 0.431);   // #FF3D6E — now
const vec3  GEL_G      = vec3(0.184, 0.831, 0.769); // #2FD4C4 — the past
const float EXPOSURE   = 1.0;
const float BEAT_FLASH = 0.0;    // exposure bump on the Link beat — try 0.35
const float PRESSURE_FADE_G = 0.5;
const float CEL_BASE   = 0.25;   // cel floor with no hats — the rest rides the highs
const float CEL_LEVELS = 4.0;
// the edge memory (v21)
const float STAMP_WINDOW = 0.06; // s after a kick during which the outline stamps
const float SOLID_UNTIL  = 0.7;  // life above this = frozen line; below, sparkles
const float GRID         = 160.0;
const float DOT          = 0.45;
const float TWINKLE_HZ   = 12.0;
const float TWINKLE_DUTY = 0.6;
const float FLICKER      = 0.10;
const vec2  CENTRE       = vec2(0.5, 0.55); // where the figure roughly is (y up)
const float EDGE_T       = 0.035; // depth gradient that reads as an edge
const vec3  SPARK_HOT    = vec3(1.0, 0.95, 0.1);   // just born — yellow
const vec3  SPARK_COOL   = vec3(1.0, 0.15, 0.9);   // dying — magenta
const vec3  LINE_COL     = vec3(1.0, 0.95, 0.1);
const float HIGHS_CURVE  = 1.5;
// the past: an atlas ring of 16 quarter-size frames
const float SLOT = 0.1;          // s per ring slot: 16 slots = 1.6 s
// ────────────────────────────────────────────────────────────────────

// ════ shared: signals ══════════════════════════════════════════════════
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
float midsNow() { return reactK() * sigB().g; }
float highsNow() { return reactK() * sigB().b; }

// ════ shared: depth ════════════════════════════════════════════════════
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
float depthAt(vec2 uv) { return IMG_NORM_PIXEL(depthBuf, uv).r; }
float figureAt(vec2 uv) { return smoothstep(figure - soft, figure + soft, depthAt(uv)); }
// ═══════════════════════════════════════════════════════════════════════

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }

// depthBuf: r depth (1 near) · g the ember life field · b fract(TIME) (dt; half floats hold no absolute TIME)
vec4 depthAndLifePass(vec2 uv) {
  vec4 prev = IMG_NORM_PIXEL(depthBuf, uv);
  float d = fallbackDepth(uv);
  d = mix(IMG_NORM_PIXEL(depth, uv).r, d, max(depthFallback, 1.0 - depthReady()));
  d = mix(d, prev.r, depthSmooth * step(1.0, FRAMEINDEX));
  float dt = mix(clamp(fract(fract(TIME) - prev.b + 1.0), 1.0 / 240.0, 0.1), 1.0 / 60.0, 1.0 - step(1.0, FRAMEINDEX));

  // the outline now: depth edges plus the figure cut (last frame's map)
  vec2 o = 1.0 / RENDERSIZE;
  float gd = abs(depthAt(uv + vec2(o.x, 0.0)) - depthAt(uv - vec2(o.x, 0.0)))
           + abs(depthAt(uv + vec2(0.0, o.y)) - depthAt(uv - vec2(0.0, o.y)));
  float gf = abs(figureAt(uv + vec2(o.x, 0.0)) - figureAt(uv - vec2(o.x, 0.0)))
           + abs(figureAt(uv + vec2(0.0, o.y)) - figureAt(uv - vec2(0.0, o.y)));
  float edge = max(smoothstep(EDGE_T, EDGE_T * 2.0, gd), smoothstep(0.25, 0.7, gf));

  // did a kick land just now? (the onset's time, or the beat with beatSync)
  float kick = step(kickAge(), STAMP_WINDOW);
  kick = max(kick, step(0.5, beatSync) * step(PHASE * 60.0 / max(TEMPO, 1.0), STAMP_WINDOW));

  // the memory: ageing, drifting outward from the figure, a whisper of lift
  vec2 cell = floor(uv * GRID);
  float pace = 0.5 + hash(cell + 11.3);
  vec2 rad = uv - CENTRE;
  vec2 outward = rad / max(length(rad), 0.05);
  vec2 wander = (vec2(hash(cell), hash(cell + 7.31)) - 0.5) * 2.0 * FLICKER;
  vec2 drift = spread * outward + vec2(0.0, lift) + wander;
  vec2 from = uv - drift * pace * dt;
  float fadeRate = (0.6 + 0.8 * hash(cell + 13.7)) / emberLife;
  float life = max(IMG_NORM_PIXEL(depthBuf, from).g - fadeRate * dt, 0.0);
  // a kick REPLACES the field with this instant's outline (v20)
  life = mix(life, edge, kick);
  life = max(life, edge * SOLID_UNTIL * 0.9 * step(hash(cell + floor(TIME * 30.0)), stream * 0.08));
  life = max(life, edge * SOLID_UNTIL * 0.9 * step(hash(cell + floor(TIME * 30.0) + 3.1), hatStamp * highsNow() * 0.3));
  return vec4(d, life, fract(TIME), 1.0);
}

// The ring: 4×4 tiles, each the camera at a quarter size; rgb + the figure in a.
vec4 ringPass(vec2 uv) {
  vec2 t = floor(uv * 4.0);
  float idx = t.x + 4.0 * t.y;
  float cur = mod(floor(TIME / SLOT), 16.0);
  if (abs(idx - cur) < 0.5 || FRAMEINDEX < 1.5) {
    vec2 q = fract(uv * 4.0);
    return vec4(IMG_NORM_PIXEL(inputImage, q).rgb, figureAt(q));
  }
  return IMG_NORM_PIXEL(ring, uv);
}
vec4 tileAt(float i, vec2 uv) {
  float k = mod(i, 16.0);
  vec2 t = vec2(mod(k, 4.0), floor(k / 4.0));
  return IMG_NORM_PIXEL(ring, (t + clamp(uv, 0.004, 0.996)) / 4.0);
}
// the camera `back` seconds ago (soft: a quarter size)
vec4 pastAt(vec2 uv, float back) {
  float now = TIME / SLOT;
  float s = clamp(now - back / SLOT, floor(now) - 14.0, floor(now));
  float s0 = floor(s);
  return mix(tileAt(s0, uv), tileAt(s0 + 1.0, uv), s - s0);
}

vec3 compAdd(vec3 pF, vec3 pG, float aF, float aG, float w, float sigma) {
  return (1.0 - (1.0 - w) * sigma * aG) * pF + (1.0 - w * sigma * aF) * pG;
}

// the bench: now and past, gelled, luma coverage, luma depth, soft z-test, summed as light
vec3 gelAt(vec2 uv, float back, float celInt) {
  vec3 rawF = IMG_NORM_PIXEL(inputImage, uv).rgb;
  vec4 past = pastAt(uv, back);
  vec3 rawG = past.rgb;
  float lF = lumaOf(rawF);
  float lG = lumaOf(rawG);
  float dF = 1.0 - lF;
  float dG = 1.0 - lG;
  float aF = lF * (1.0 - fade);
  float aG = lG * (1.0 - PRESSURE_FADE_G * fade);
  float figF = figureAt(uv);
  float figG = past.a;
  vec3 celF = rawF * ((floor(lF * CEL_LEVELS) + 0.5) / CEL_LEVELS) / max(lF, 1e-4);
  vec3 celG = rawG * ((floor(lG * CEL_LEVELS) + 0.5) / CEL_LEVELS) / max(lG, 1e-4);
  vec3 shadedF = mix(rawF, celF, celInt * figF);
  vec3 shadedG = mix(rawG, celG, celInt * figG);
  vec3 cF = mix(shadedF, shadedF * GEL_F, amount);
  vec3 cG = mix(shadedG, shadedG * GEL_G, amount);
  if (aF <= 0.0) { dF = 1.0; }
  if (aG <= 0.0) { dG = 1.0; }
  float t = clamp(0.5 + (dG - dF) / (2.0 * EPSILON), 0.0, 1.0);
  float w = smoothstep(0.0, 1.0, t);
  vec3 pOut = compAdd(cF * aF, cG * aG, aF, aG, w, SIGMA);
  vec3 shown = pOut * (EXPOSURE * (1.0 + BEAT_FLASH * exp(-5.0 * PHASE)));
  return 1.0 - exp(-shown);
}

void main() {
  vec2 uv = isf_FragNormCoord;
  if (PASSINDEX == 0.0) { gl_FragColor = sigPass(); return; }
  if (PASSINDEX == 1.0) { gl_FragColor = depthAndLifePass(uv); return; }
  if (PASSINDEX == 2.0) { gl_FragColor = ringPass(uv); return; }

  float highs = highsNow();
  float back = reach + midsReach * midsNow();
  float celInt = cel * clamp(CEL_BASE + (1.0 - CEL_BASE) * highs, 0.0, 1.0);

  // the sizzle: R and B pulled apart along the radius, G holds (v21's post)
  vec2 c = uv - 0.5;
  float r = length(c) * 2.0;
  vec2 dir = c * 2.0 * pow(max(r, 1e-4), HIGHS_CURVE - 1.0);
  float amt = sizzle * highs;
  vec3 shown;
  if (amt > 0.0005) {
    shown = vec3(gelAt(uv + dir * amt, back, celInt).r, gelAt(uv, back, celInt).g, gelAt(uv - dir * amt, back, celInt).b);
  } else {
    shown = gelAt(uv, back, celInt);
  }

  // the edge memory, drawn: frozen line while young, sparkles below SOLID_UNTIL
  float life = depthAt(uv) * 0.0 + IMG_NORM_PIXEL(depthBuf, uv).g;
  vec2 cell = floor(uv * GRID);
  float dying = clamp(life / SOLID_UNTIL, 0.0, 1.0);
  float survive = step(hash(cell + 3.7), dying);
  vec2 cuv = fract(uv * GRID) - 0.5;
  float dotMask = step(length(cuv), DOT * (0.35 + 0.65 * dying));
  float glint = step(1.0 - TWINKLE_DUTY, hash(cell + 0.173 * floor(TIME * TWINKLE_HZ)));
  float solid = smoothstep(SOLID_UNTIL, SOLID_UNTIL + 0.1, life);
  float sparkle = smoothstep(0.0, 0.12, life) * (1.0 - solid) * survive * dotMask * glint;

  // the live outline (v12–v16), the young snapshot as ink (v04–v10)
  vec2 o = 1.5 / IMG_SIZE(depthBuf);
  float gf = abs(figureAt(uv + vec2(o.x, 0.0)) - figureAt(uv - vec2(o.x, 0.0)))
           + abs(figureAt(uv + vec2(0.0, o.y)) - figureAt(uv - vec2(0.0, o.y)));
  float line = smoothstep(0.25, 0.7, gf);
  shown = mix(shown, LINE_COL, outline * line);
  shown *= 1.0 - ink * 0.85 * life * solid;

  // embers: yellow at birth, cooling to magenta (v19–v21), blended over
  float heat = clamp(life / SOLID_UNTIL, 0.0, 1.0);
  shown = mix(shown, mix(SPARK_COOL, SPARK_HOT, heat), embers * sparkle);
  gl_FragColor = vec4(clamp(shown, 0.0, 1.0), 1.0);
}
