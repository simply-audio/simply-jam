/*{
  "DESCRIPTION": "paint: the camera as a wet oil painting. Strokes follow the picture's edges (a Kuwahara filter stretched along them), finer on the figure and looser in the room, with the canvas showing through and the paint standing up in relief. Kicks swell the brush and the colour (kickAmt), the mids make the paint run and drift (midsAmt), the hats make the wet strokes catch the light (highsAmt).",
  "CREDIT": "Chris Poucet, simply-jam",
  "CATEGORIES": ["texture", "depth", "sound", "beat", "pressure"],
  "INPUTS": [
    { "NAME": "inputImage", "TYPE": "image" },
    { "NAME": "depth", "TYPE": "depth" },
    { "NAME": "wave", "TYPE": "audio" },
    { "NAME": "spectrum", "TYPE": "audioFFT" },
    { "NAME": "amount", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 1.0, "DESCRIPTION": "pressure: how far the painting takes over from the camera" },
    { "NAME": "react", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5, "DESCRIPTION": "how much the music moves it: 0 holds it still, 0.5 as tuned, 1 twice as much" },
    { "NAME": "brush", "TYPE": "float", "MIN": 3.0, "MAX": 30.0, "DEFAULT": 12.0, "DESCRIPTION": "stroke size in pixels (the room's; the figure's is finer by `detail`)" },
    { "NAME": "detail", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.6, "DESCRIPTION": "how much finer the figure is painted than the room" },
    { "NAME": "stretch", "TYPE": "float", "MIN": 1.0, "MAX": 4.0, "DEFAULT": 2.0, "DESCRIPTION": "how long the strokes run along the edges" },
    { "NAME": "saturation", "TYPE": "float", "MIN": 0.0, "MAX": 2.0, "DEFAULT": 1.3, "DESCRIPTION": "how rich the paint's colour is" },
    { "NAME": "relief", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5, "DESCRIPTION": "how much the paint stands up off the canvas" },
    { "NAME": "canvas", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.4, "DESCRIPTION": "the canvas weave showing through" },
    { "NAME": "kickAmt", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.7, "DESCRIPTION": "kicks swell the brush and the colour" },
    { "NAME": "midsAmt", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5, "DESCRIPTION": "mids make the paint run and drift" },
    { "NAME": "highsAmt", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.6, "DESCRIPTION": "hats make the wet strokes catch the light" },
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
    { "TARGET": "strokes", "WIDTH": "$WIDTH / 2", "HEIGHT": "$HEIGHT / 2" },
    {}
  ]
}*/

// `strokes` (half size) is the painting: a 9×9 Kuwahara kernel laid along
// the local edge (long axis = the edge's direction), each pixel taking the
// mean of whichever of the four quadrants is most even. The last pass puts
// the relief, the shine and the canvas on it.

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
float highsNow() { return reactK() * sigB().b; }

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vnoise(vec2 p) {
  vec2 i = floor(p); vec2 f = fract(p); f = f * f * (3.0 - 2.0 * f);
  return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
vec3 cam(vec2 uv) { return IMG_NORM_PIXEL(inputImage, clamp(uv, 0.0, 1.0)).rgb; }

vec4 strokesPass(vec2 uv) {
  float kick = kickNow();
  float mids = midsNow();
  float fig = figureAt(uv);
  vec2 px = 1.0 / RENDERSIZE;
  float size = brush * mix(1.0, 1.0 - 0.7 * detail, fig) * (1.0 + 0.6 * kickAmt * kick);

  // the paint runs: a slow drift field, stronger on the mids
  vec2 drift = vec2(vnoise(uv * 4.0 + vec2(TIME * 0.3, 0.0)), vnoise(uv * 4.0 + vec2(7.3, -TIME * 0.25))) - 0.5;
  vec2 q = uv + drift * px * size * (0.4 + 2.5 * midsAmt * mids);

  // the edge's direction from the brightness gradient
  vec2 e = px * size * 0.5;
  float gx = lumaOf(cam(q + vec2(e.x, 0.0))) - lumaOf(cam(q - vec2(e.x, 0.0)));
  float gy = lumaOf(cam(q + vec2(0.0, e.y))) - lumaOf(cam(q - vec2(0.0, e.y)));
  vec2 along = normalize(vec2(-gy, gx) + vec2(1e-4, 0.0));
  vec2 au = along * (size / 4.0) * sqrt(stretch) * px;
  vec2 av = vec2(-along.y, along.x) * (size / 4.0) / sqrt(stretch) * px;

  vec4 s0 = vec4(0.0); vec4 s1 = vec4(0.0); vec4 s2 = vec4(0.0); vec4 s3 = vec4(0.0);
  float l0 = 0.0; float l1 = 0.0; float l2 = 0.0; float l3 = 0.0;
  for (int j = -4; j <= 4; j++) {
    for (int i = -4; i <= 4; i++) {
      vec3 c = cam(q + float(i) * au + float(j) * av);
      float l = lumaOf(c);
      vec4 v = vec4(c, l * l);
      if (i <= 0 && j <= 0) { s0 += v; l0 += l; }
      if (i >= 0 && j <= 0) { s1 += v; l1 += l; }
      if (i <= 0 && j >= 0) { s2 += v; l2 += l; }
      if (i >= 0 && j >= 0) { s3 += v; l3 += l; }
    }
  }
  float n = 25.0;
  float v0 = s0.a / n - (l0 / n) * (l0 / n);
  float v1 = s1.a / n - (l1 / n) * (l1 / n);
  float v2 = s2.a / n - (l2 / n) * (l2 / n);
  float v3 = s3.a / n - (l3 / n) * (l3 / n);
  vec3 col = s0.rgb / n; float best = v0;
  if (v1 < best) { best = v1; col = s1.rgb / n; }
  if (v2 < best) { best = v2; col = s2.rgb / n; }
  if (v3 < best) { best = v3; col = s3.rgb / n; }

  float sat = saturation * (1.0 + 0.45 * kickAmt * kick);
  col = mix(vec3(lumaOf(col)), col, sat);
  return vec4(clamp(col, 0.0, 1.0), 1.0);
}

void main() {
  vec2 uv = isf_FragNormCoord;
  if (PASSINDEX == 0.0) { gl_FragColor = sigPass(); return; }
  if (PASSINDEX == 1.0) { gl_FragColor = depthTimePass(uv); return; }
  if (PASSINDEX == 2.0) { gl_FragColor = strokesPass(uv); return; }

  float hats = highsNow();
  vec3 c = cam(uv);
  vec2 tx = 1.5 / IMG_SIZE(strokes);
  vec3 p = IMG_NORM_PIXEL(strokes, uv).rgb;
  // relief: light from the top left across the paint's height (its brightness)
  float hUp = lumaOf(IMG_NORM_PIXEL(strokes, clamp(uv + vec2(-tx.x, tx.y), 0.0, 1.0)).rgb);
  float hDn = lumaOf(IMG_NORM_PIXEL(strokes, clamp(uv + vec2(tx.x, -tx.y), 0.0, 1.0)).rgb);
  float ridge = hUp - hDn;
  vec3 col = p * (1.0 + 0.9 * relief * ridge);
  // shine: the wet ridges catch the light on the hats
  col += vec3(1.0, 0.97, 0.92) * smoothstep(0.02, 0.12, ridge) * relief * (0.15 + 0.85 * highsAmt * hats) * 0.5;
  // canvas weave
  vec2 f = isf_FragNormCoord * RENDERSIZE;
  float weave = 0.5 + 0.25 * (sin(f.x * 1.9) + sin(f.y * 1.9)) + 0.15 * (hash(floor(f / 2.0)) - 0.5);
  col *= 1.0 - 0.12 * canvas * weave;
  gl_FragColor = vec4(clamp(mix(c, col, amount), 0.0, 1.0), 1.0);
}
