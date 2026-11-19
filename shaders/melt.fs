/*{
  "DESCRIPTION": "Things that flow: one feedback buffer advected. 0 the figure dissolves upward into smoke (FX 80), 1 pixel sort, bright streaks falling (81), 2 the room melts and drips (112), 3 the room flows like liquid on the mids (183), 4 pixel rain on the hats (228), 5 the figure's colours bleed into the room like ink (237).",
  "CREDIT": "Chris Poucet, simply-creative FX 80 81 112 183 228 237, ported to tussen ISF",
  "CATEGORIES": ["time", "feedback", "depth", "sound", "beat", "modes", "pressure"],
  "INPUTS": [
    { "NAME": "inputImage", "TYPE": "image" },
    { "NAME": "depth", "TYPE": "depth" },
    { "NAME": "wave", "TYPE": "audio" },
    { "NAME": "spectrum", "TYPE": "audioFFT" },
    { "NAME": "amount", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.4 },
    { "NAME": "react", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5, "DESCRIPTION": "how much the music moves it: 0 holds it still, 0.5 as tuned, 1 twice as much" },
    { "NAME": "mode", "TYPE": "long", "MIN": 0, "MAX": 5, "DEFAULT": 0, "VALUES": [0,1,2,3,4,5], "LABELS": ["smoke figure", "pixel sort", "room melt", "liquid room", "pixel rain", "colour bleed"] },
    { "NAME": "figureOnTop", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 1.0 },
    { "NAME": "roomDim", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 1.0, "DESCRIPTION": "smoke: what shows where the figure dissolved (the room, dimmed)" },
    { "NAME": "kickAmt", "TYPE": "float", "MIN": 0.0, "MAX": 2.0, "DEFAULT": 1.0 },
    { "NAME": "midsAmt", "TYPE": "float", "MIN": 0.0, "MAX": 2.0, "DEFAULT": 1.0 },
    { "NAME": "highsAmt", "TYPE": "float", "MIN": 0.0, "MAX": 2.0, "DEFAULT": 1.0 },
    { "NAME": "beatSync", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "gain", "TYPE": "float", "MIN": 0.25, "MAX": 8.0, "DEFAULT": 1.0 },
    { "NAME": "figure", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5 },
    { "NAME": "soft", "TYPE": "float", "MIN": 0.01, "MAX": 0.3, "DEFAULT": 0.08 },
    { "NAME": "depthFallback", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "depthSmooth", "TYPE": "float", "MIN": 0.0, "MAX": 0.95, "DEFAULT": 0.5 }
  ],
  "PASSES": [
    { "TARGET": "sig", "WIDTH": "2", "HEIGHT": "1", "FLOAT": true, "PERSISTENT": true },
    { "TARGET": "depthBuf", "WIDTH": "$WIDTH / 4", "HEIGHT": "$HEIGHT / 4", "FLOAT": true, "PERSISTENT": true },
    { "TARGET": "buf", "FLOAT": true, "PERSISTENT": true },
    {}
  ]
}*/

// Chris's consts are inlined per mode (the FX number on each branch). The
// NDI "room" of the originals is the camera itself. y runs up here: smoke
// rises, drips and rain fall, as in the originals.

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
float hash1(float x) { return fract(sin(x * 91.3458) * 47453.5453); }
float h31(vec3 p) { p = fract(p * 0.1031); p += dot(p, p.zyx + 31.32); return fract((p.x + p.y) * p.z); }
float vnoise(vec2 p) {
  vec2 i = floor(p); vec2 f = fract(p); f = f * f * (3.0 - 2.0 * f);
  return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
float fbm(vec2 p) { return 0.55 * vnoise(p) + 0.3 * vnoise(p * 2.3) + 0.15 * vnoise(p * 5.1); }
float field(vec2 p, float t) { return vnoise(p + vec2(0.0, t * 0.13)) + 0.5 * vnoise(p * 2.03 - vec2(t * 0.21, 0.0)); }
vec2 curl(vec2 p, float t) {
  float e = 0.05;
  float dx = field(p + vec2(e, 0.0), t) - field(p - vec2(e, 0.0), t);
  float dy = field(p + vec2(0.0, e), t) - field(p - vec2(0.0, e), t);
  return vec2(dy, -dx) / (2.0 * e);
}
vec3 cam(vec2 uv) { return IMG_NORM_PIXEL(inputImage, uv).rgb; }
float amtOf(float ke, float me, float kick, float mids) {
  return clamp(amount + kickAmt * ke * kick + midsAmt * me * mids, 0.0, 1.0);
}
// FX 80: 1 where the figure has dissolved (tops go first: y up, so 1 - uv.y is the bottom… the original's f.uv was y down)
float gone(vec2 uv, float k) {
  float n = fbm(uv * 7.0 + vec2(0.0, -TIME * 0.4));
  n = mix(n, uv.y, 0.35);
  return smoothstep(n - 0.04, n + 0.04, k * 1.15 - 0.05);
}

vec4 bufPass(vec2 uv, float m, float kick, float mids) {
  float dt = dtNow();
  float fresh = step(FRAMEINDEX, 1.5);
  vec3 c = cam(uv);
  float fig = figureAt(uv);
  float aspect = RENDERSIZE.x / max(RENDERSIZE.y, 1.0);
  if (m < 0.5) {                                   // FX 80 smoke: rises (y up)
    vec2 p = uv * 5.0;
    vec2 sw = vec2(sin(p.y * 1.9 + TIME * 1.1) + 0.5 * sin(p.y * 4.3 - TIME * 1.7), 0.5 * cos(p.x * 2.1 - TIME * 0.8));
    vec2 vel = vec2(0.0, 0.16 * (1.0 + 2.0 * kick)) + 0.10 * (1.0 + 1.5 * mids) * sw;
    vec4 prev = IMG_NORM_PIXEL(buf, uv - vel * dt) * (1.0 - fresh);
    prev.a = max(prev.a - dt / 2.2, 0.0);
    float k = amtOf(0.7, 0.3, kick, mids) * (1.0 + 0.35 * kick);
    float feed = fig * gone(uv, k) * 0.9;
    feed *= step(hash(floor((isf_FragNormCoord * RENDERSIZE) * 0.5) + fract(TIME * 3.7) * 91.0), 0.25 * (1.0 + 2.0 * kick));
    if (feed > prev.a) { prev = vec4(c * 1.1 + 0.05, feed); }
    return prev;
  }
  if (m < 1.5) {                                   // FX 81 pixel sort: bright streaks fall
    float fx = amtOf(0.8, 0.25, kick, mids);
    if (fx <= 0.001) { return vec4(c, 1.0); }
    float thr = max(mix(1.0, 0.25, fx) - 0.3 * kick, 0.05);
    float col = floor((isf_FragNormCoord.x * RENDERSIZE.x) / 2.0);
    float dec = 0.975 - 0.02 * hash1(col);
    vec3 above = IMG_NORM_PIXEL(buf, uv + vec2(0.0, 5.0 * (1.0 + mids) / RENDERSIZE.y)).rgb * dec * (1.0 - fresh);
    float la = lumaOf(above);
    float k = smoothstep(thr * 0.8, thr, la) * step(lumaOf(c), la);
    return vec4(mix(c, above, k), 1.0);
  }
  if (m < 2.5) {                                   // FX 112 room melt: drips down
    float amt = amtOf(0.9, 0.25, kick, mids);
    if (amt <= 0.001 || fresh > 0.5) { return vec4(c, 1.0); }
    float cx = uv.x * 38.0;
    float s = mix(hash1(floor(cx)), hash1(floor(cx) + 1.0), smoothstep(0.0, 1.0, fract(cx)));
    s = 0.25 + 0.75 * s * s;
    float speed = 0.45 * s * (1.0 + 0.8 * mids + 2.0 * kick) * (0.4 + 0.6 * amt);
    vec2 from = uv + vec2(0.004 * sin(uv.y * 40.0 + TIME * 3.0), speed) * dt;
    vec3 prev = IMG_NORM_PIXEL(buf, from).rgb;
    float hold = exp(-dt / (3.0 * amt * amt + 1e-4));
    vec3 r = mix(c, prev, hold);
    r += clamp(c - r, -1.0 / 255.0, 1.0 / 255.0) * (1.0 - amt);
    return vec4(r, 1.0);
  }
  if (m < 3.5) {                                   // FX 183 liquid room
    float amt = amtOf(0.6, 0.55, kick, mids);
    vec2 p = (uv - 0.5) * vec2(aspect, 1.0);
    vec2 v = curl(p * 2.2, TIME) * 0.006 * (0.15 + 0.85 * mids);
    float r = length(p);
    float ringR = (1.0 - kick) * 1.4 * 0.15 + 0.02;
    float ring = exp(-pow((r - ringR) / 0.06, 2.0)) * kick;
    v += normalize(p + 1e-5) * ring * 0.012;
    v /= vec2(aspect, 1.0);
    vec3 prev = IMG_NORM_PIXEL(buf, uv - v).rgb;
    float k = amt > 0.002 ? mix(0.80, 0.975, amount) * smoothstep(0.0, 0.15, amt) : 0.0;
    return vec4(mix(c, prev, k * (1.0 - fresh)), 1.0);
  }
  if (m < 4.5) {                                   // FX 228 pixel rain: drops fall from hats
    vec2 texel = 1.0 / RENDERSIZE;
    float amt = clamp(0.5 * amount + highsAmt * highsNow() + kickAmt * 0.5 * kick, 0.0, 1.0);
    float col = floor((isf_FragNormCoord.x * RENDERSIZE.x) / 2.0);
    float speed = mix(3.0, 11.0, h31(vec3(col, 7.0, 1.0))) * dt * 60.0;
    vec3 cA = vec3(0.0); float iA = 0.0;
    for (int k = 1; k <= 6; k++) {
      vec2 above = uv + vec2(0.0, speed * float(k) / 6.0 * texel.y);
      vec4 s = IMG_NORM_PIXEL(buf, above);
      float ik = above.y <= 1.0 ? s.a : 0.0;
      if (ik > iA) { iA = ik; cA = s.rgb; }
    }
    vec4 h = IMG_NORM_PIXEL(buf, uv);
    float iH = h.a - dt / 0.35;
    vec3 rc = cA; float i = iA - dt / 0.35 * 0.3;
    if (iH > i) { rc = h.rgb; i = iH; }
    vec2 cell = floor((isf_FragNormCoord * RENDERSIZE) / 2.0);
    if (h31(vec3(cell, mod(FRAMEINDEX, 4096.0))) < 0.002 * amt) { rc = c * 1.25; i = 1.0; }
    return vec4(rc, clamp(i, 0.0, 1.0) * (1.0 - fresh));
  }
  // FX 237 colour bleed: the figure's colours creep into the room
  float amt = amtOf(0.7, 0.45, kick, mids);
  if (fig > 0.5) { return vec4(clamp(mix(vec3(lumaOf(c)), c, 1.35), 0.0, 1.0), 1.0); }
  if (amt < 0.002) { return vec4(0.0); }
  float r = 0.006 * (0.15 + 0.85 * mids) * (1.0 + 2.5 * kick);
  float wob = (vnoise(uv * 7.0 + TIME * 0.3) - 0.5) * 6.2831853 * 0.6;
  float best = 0.0; vec3 acc = vec3(0.0); float w = 0.0;
  for (int i = 0; i < 6; i++) {
    float a = float(i) * 1.0471976 + wob + TIME * 0.7;
    vec4 s = IMG_NORM_PIXEL(buf, uv + vec2(cos(a) / aspect, sin(a)) * r);
    acc += s.rgb * s.a; w += s.a;
    best = max(best, s.a);
  }
  best = max(best, IMG_NORM_PIXEL(buf, uv).a);
  float life = mix(0.4, 3.0, amount) + 1e-3;
  float dens = max(best - dt / life, 0.0) * (1.0 - fresh);
  return vec4(w > 1e-3 ? acc / w : vec3(0.0), dens);
}

void main() {
  vec2 uv = isf_FragNormCoord;
  if (PASSINDEX == 0.0) { gl_FragColor = sigPass(); return; }
  if (PASSINDEX == 1.0) { gl_FragColor = depthTimePass(uv); return; }
  float m = floor(mode + 0.5);
  float kick = kickNow();
  float mids = midsNow();
  if (PASSINDEX == 2.0) { gl_FragColor = bufPass(uv, m, kick, mids); return; }

  vec3 c = cam(uv);
  float fig = figureAt(uv);
  float top = fig * figureOnTop;
  vec4 b = IMG_NORM_PIXEL(buf, uv);
  vec3 col = c;
  if (m < 0.5) {
    float k = amtOf(0.7, 0.3, kick, mids);
    float n = fbm(uv * 7.0 + vec2(0.0, -TIME * 0.4));
    n = mix(n, uv.y, 0.35);
    float thr = k * (1.0 + 0.35 * kick) * 1.15 - 0.05;
    float keep = 1.0 - smoothstep(n - 0.04, n + 0.04, thr);
    float front = (1.0 - smoothstep(0.0, 0.06, abs(thr - n))) * step(0.001, k);
    col = mix(c * roomDim, c, fig * keep);
    col += 1.2 * front * fig * keep * vec3(1.0, 0.9, 0.75) * 0.5;
    vec2 tx = 2.0 / RENDERSIZE;
    vec4 s = b * 2.0 + IMG_NORM_PIXEL(buf, uv + vec2(tx.x, 0.0)) + IMG_NORM_PIXEL(buf, uv - vec2(tx.x, 0.0))
           + IMG_NORM_PIXEL(buf, uv + vec2(0.0, tx.y)) + IMG_NORM_PIXEL(buf, uv - vec2(0.0, tx.y));
    s /= 6.0;
    float a = clamp(s.a * 1.3, 0.0, 1.0);
    col = mix(col, s.rgb, a * (1.0 - fig * keep * 0.7));
  } else if (m < 1.5) {
    col = b.rgb;
  } else if (m < 2.5) {
    float amt = amtOf(0.9, 0.25, kick, mids);
    float diff = clamp(length(b.rgb - c) * 2.0, 0.0, 1.0);
    vec3 room = mix(c, b.rgb * (1.0 - 0.15 * diff), smoothstep(0.0, 0.05, amt));
    col = mix(room, c, top);
  } else if (m < 3.5) {
    float amt = amtOf(0.6, 0.55, kick, mids);
    vec3 room = mix(c, b.rgb, smoothstep(0.0, 0.05, amt));
    float drift = length(b.rgb - c);
    room += 0.25 * amt * smoothstep(0.08, 0.5, drift) * vec3(0.8, 0.9, 1.0) * lumaOf(b.rgb);
    col = mix(room, c, top);
  } else if (m < 4.5) {
    float a = 0.95 * smoothstep(0.0, 0.6, b.a);
    col = mix(c, b.rgb * (1.0 + 0.5 * smoothstep(0.85, 1.0, b.a)), a);
  } else {
    float amt = amtOf(0.7, 0.45, kick, mids);
    vec3 inked = mix(b.rgb, c * b.rgb * 1.8, 0.5);
    vec3 room = mix(c, inked, smoothstep(0.0, 1.0, b.a) * 0.85 * smoothstep(0.0, 0.08, amt));
    col = mix(room, c, fig);
  }
  gl_FragColor = vec4(clamp(col, 0.0, 1.0), 1.0);
}
