/*{
  "DESCRIPTION": "beams: a dark stage cut by light. Gel-coloured beams fall from above through haze and land on the figure, which stands lit in them and throws its shadow down the shafts; the room sinks into the dark. Each beat the beams pan to new places (swing), each bar the gels move one place along (cycle); kicks flare and widen them, the mids thicken and stir the haze, the hats glint dust in the light.",
  "CREDIT": "Chris Poucet, simply-jam",
  "CATEGORIES": ["whole-look", "figure", "depth", "sound", "beat", "pressure"],
  "INPUTS": [
    { "NAME": "inputImage", "TYPE": "image" },
    { "NAME": "depth", "TYPE": "depth" },
    { "NAME": "wave", "TYPE": "audio" },
    { "NAME": "spectrum", "TYPE": "audioFFT" },
    { "NAME": "amount", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 1.0, "DESCRIPTION": "pressure: how far the stage takes over from the camera" },
    { "NAME": "count", "TYPE": "float", "MIN": 1.0, "MAX": 6.0, "DEFAULT": 4.0, "DESCRIPTION": "how many beams (1-6)" },
    { "NAME": "spread", "TYPE": "float", "MIN": 0.02, "MAX": 0.4, "DEFAULT": 0.12, "DESCRIPTION": "how fast a beam widens on its way down" },
    { "NAME": "swing", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.6, "DESCRIPTION": "how far the beams pan to a new place on each beat (0: they hang still and only sway)" },
    { "NAME": "haze", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.55, "DESCRIPTION": "smoke in the air: how visible the shafts are" },
    { "NAME": "room", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.85, "DESCRIPTION": "how dark the room goes outside the beams" },
    { "NAME": "key", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.3, "DESCRIPTION": "light on the figure outside the beams" },
    { "NAME": "white", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0, "DESCRIPTION": "0 coloured gels · 1 open white beams" },
    { "NAME": "cycle", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 1.0, "DESCRIPTION": "1: the gels move one beam along on every bar" },
    { "NAME": "kickAmt", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.8, "DESCRIPTION": "kicks flare and widen the beams" },
    { "NAME": "midsAmt", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.6, "DESCRIPTION": "mids thicken and stir the haze" },
    { "NAME": "highsAmt", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.6, "DESCRIPTION": "hats glint dust in the beams" },
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

// Beams live in an aspect-correct space p = (x · aspect, y), y up. Each beam
// starts above the frame and points down at an angle; the figure (near in
// depth) is lit where a beam crosses it, the room only takes a dim wash, and
// a room pixel whose path up to the beam's source crosses the figure is in
// the figure's shadow.

// ════ shared: signals ══════════════════════════════════════════════════
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
float midsNow() { return sigB().g; }

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

vec4 depthTimePass(vec2 uv) {
  vec4 prev = IMG_NORM_PIXEL(depthBuf, uv);
  float d = fallbackDepth(uv);
  d = mix(IMG_NORM_PIXEL(depth, uv).r, d, max(depthFallback, 1.0 - depthReady()));
  d = mix(d, prev.r, depthSmooth * step(1.0, FRAMEINDEX));
  return vec4(d, mix(clamp(fract(fract(TIME) - prev.b + 1.0), 1.0 / 240.0, 0.1), 1.0 / 60.0, 1.0 - step(1.0, FRAMEINDEX)), fract(TIME), 1.0);
}
float dtNow() { return IMG_NORM_PIXEL(depthBuf, vec2(0.5)).g; }
float highsNow() { return sigB().b; }

const float SRC_Y      = 1.12;   // the lamps hang just above the frame
const float MAX_TILT   = 0.55;   // rad, a beam's furthest pan from straight down
const float SHADOW_LEN = 0.45;   // how far down a shaft the figure's shadow reaches
const float SHADOW_K   = 0.85;   // how dark that shadow is

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vnoise(vec2 p) {
  vec2 i = floor(p); vec2 f = fract(p); f = f * f * (3.0 - 2.0 * f);
  return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
float fbm(vec2 p) { return 0.55 * vnoise(p) + 0.3 * vnoise(p * 2.3) + 0.15 * vnoise(p * 5.1); }
vec3 cam(vec2 uv) { return IMG_NORM_PIXEL(inputImage, uv).rgb; }

// four theatre gels: magenta, cyan, amber, steel blue
vec3 gel(float k) {
  float i = mod(floor(k + 0.5), 4.0);
  vec3 c = vec3(1.0, 0.22, 0.72);
  if (i > 0.5) { c = vec3(0.2, 0.8, 1.0); }
  if (i > 1.5) { c = vec3(1.0, 0.62, 0.18); }
  if (i > 2.5) { c = vec3(0.55, 0.65, 1.0); }
  return mix(c, vec3(0.95, 0.95, 1.0), white);
}

// where beam i points: a new place per beat (eased in over the first part
// of the beat), plus a slow sway so it never stands dead still
float tiltOf(float i) {
  float b = floor(BEAT);
  float from = hash(vec2(i * 7.31, b - 1.0));
  float to = hash(vec2(i * 7.31, b));
  float pos = mix(from, to, smoothstep(0.0, 0.45, PHASE)) - 0.5;
  float sway = 0.18 * sin(TIME * 0.37 + i * 1.7);
  return clamp(swing * pos * 2.0 * MAX_TILT + sway * (0.4 + 0.6 * swing), -MAX_TILT, MAX_TILT);
}

void main() {
  vec2 uv = isf_FragNormCoord;
  if (PASSINDEX == 0.0) { gl_FragColor = sigPass(); return; }
  if (PASSINDEX == 1.0) { gl_FragColor = depthTimePass(uv); return; }

  float kick = kickNow();
  float mids = midsNow();
  float hats = highsNow();
  float aspect = RENDERSIZE.x / max(RENDERSIZE.y, 1.0);
  vec2 p = vec2(uv.x * aspect, uv.y);
  vec3 c = cam(uv);
  float fig = figureAt(uv);

  float flare = 1.0 + 1.3 * kickAmt * kick;
  float widen = 1.0 + 0.5 * kickAmt * kick;
  float n = fbm(p * vec2(2.2, 3.4) + vec2(TIME * 0.05, -TIME * 0.03) * (1.0 + 3.0 * midsAmt * mids));
  float air = clamp(haze * (0.3 + 0.7 * n) * (0.6 + 0.9 * midsAmt * mids), 0.0, 1.0);
  float barShift = cycle * floor(BAR);
  float nBeams = clamp(floor(count + 0.5), 1.0, 6.0);

  vec3 onFig = vec3(0.0);    // light landing on the figure
  vec3 onRoom = vec3(0.0);   // light landing on the room (shadowed)
  vec3 shaft = vec3(0.0);    // light scattered by the haze
  float inBeam = 0.0;
  for (int i = 0; i < 6; i++) {
    float fi = float(i);
    if (fi < nBeams) {
      vec2 s = vec2((fi + 0.5) / nBeams * aspect, SRC_Y);
      float a = tiltOf(fi);
      vec2 dir = vec2(sin(a), -cos(a));
      vec2 v = p - s;
      float along = max(dot(v, dir), 0.0);
      float across = abs(v.x * dir.y - v.y * dir.x);
      float w = (0.012 + along * spread) * widen;
      float e = across / w;
      float lit = smoothstep(1.0, 0.6, e) * (0.65 + 0.35 * exp(-e * e * 3.0)) / (1.0 + 0.7 * along);
      if (lit > 0.001) {
        // the figure's shadow: walk back up the beam towards its lamp
        float occ = 0.0;
        for (int k = 1; k <= 8; k++) {
          float d = float(k) / 8.0 * SHADOW_LEN;
          vec2 q = p - dir * d;
          vec2 quv = vec2(q.x / aspect, q.y);
          float inside = step(0.0, quv.x) * step(quv.x, 1.0) * step(0.0, quv.y) * step(quv.y, 1.0);
          occ = max(occ, figureAt(clamp(quv, 0.0, 1.0)) * inside * (1.0 - d / (SHADOW_LEN * 1.15)));
        }
        float shade = 1.0 - SHADOW_K * occ * (1.0 - fig);
        vec3 g = gel(fi + barShift) * lit * flare;
        onFig += g;
        onRoom += g * shade;
        shaft += g * shade * air;
        inBeam = max(inBeam, lit * shade);
      }
    }
  }

  // dust glinting in the light on the hats
  vec2 cell = floor(isf_FragNormCoord * RENDERSIZE / 3.0);
  float glint = step(1.0 - 0.06 * highsAmt * hats, hash(cell + floor(TIME * 15.0) * 13.1)) * inBeam;

  vec3 roomLit = c * (1.0 - room) + c * onRoom * 0.55;
  vec3 figLit = c * (key + 1.6 * onFig);
  vec3 stage = mix(roomLit, figLit, fig);
  stage += shaft * mix(0.55, 0.25, fig) + glint * vec3(1.0, 0.97, 0.9) * 0.9;
  stage = stage / (1.0 + 0.35 * max(stage - 0.8, 0.0));   // soft shoulder on the hot spots
  gl_FragColor = vec4(clamp(mix(c, stage, amount), 0.0, 1.0), 1.0);
}
