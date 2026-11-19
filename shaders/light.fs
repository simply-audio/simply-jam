/*{
  "DESCRIPTION": "light: the figure relit, as if the room had a lighting rig. soft key: a soft box lights the figure from the side, shaped by the depth map, and moves to a new place on each beat, with a cool rim from the other side that flares on the kicks · venetian blinds: film-noir slats of window light that bend over the figure and slide along on the beat, opening on the kicks · pool caustics: water light rippling over the figure and the wall, faster on the mids, surging on the kicks · leaf dapple: sun through leaves, swaying on the mids, a gust on each kick, sharper on the hats. The room sinks into the dark (room).",
  "CREDIT": "Chris Poucet, simply-jam",
  "CATEGORIES": ["figure", "colour", "depth", "sound", "beat", "modes", "pressure"],
  "INPUTS": [
    { "NAME": "inputImage", "TYPE": "image" },
    { "NAME": "depth", "TYPE": "depth" },
    { "NAME": "wave", "TYPE": "audio" },
    { "NAME": "spectrum", "TYPE": "audioFFT" },
    { "NAME": "amount", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 1.0, "DESCRIPTION": "how far the light takes over from the camera (1 as tuned; 0 the picture untouched)" },
    { "NAME": "react", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5, "DESCRIPTION": "how much the music moves it: 0 holds it still, 0.5 as tuned, 1 twice as much" },
    { "NAME": "mode", "TYPE": "long", "MIN": 0, "MAX": 3, "DEFAULT": 0, "VALUES": [0, 1, 2, 3], "LABELS": ["soft key", "venetian blinds", "pool caustics", "leaf dapple"] },
    { "NAME": "room", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.6, "DESCRIPTION": "how far the room behind the figure sinks into the dark" },
    { "NAME": "move", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.6, "DESCRIPTION": "how far the light moves on each beat (key, blinds) or how fast it flows (caustics, dapple)" },
    { "NAME": "warmth", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.6, "DESCRIPTION": "the light's colour: 0 cold, 0.5 white, 1 tungsten" },
    { "NAME": "slats", "TYPE": "float", "MIN": 3.0, "MAX": 24.0, "DEFAULT": 9.0, "DESCRIPTION": "blinds: how many slats; caustics and dapple: their scale" },
    { "NAME": "kickAmt", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.7, "DESCRIPTION": "kicks flare the rim, open the blinds, surge the caustics, gust the leaves" },
    { "NAME": "midsAmt", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5, "DESCRIPTION": "mids soften the light and stir the water and the leaves" },
    { "NAME": "highsAmt", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5, "DESCRIPTION": "hats put a sheen on the figure and a glint in the light" },
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
    {}
  ]
}*/

// The depth map (smoothed, quarter size) gives each pixel a surface normal;
// every mode lights that surface, so the light wraps round the figure and
// lands flatter and softer on the wall. The camera's own colour is the
// surface's colour: the light multiplies it, never paints over it.

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
// 1 while music plays, 0 in silence (the lows or the highs heard): a pulse
// on the beat clock alone is gated on it, so a look sits still in silence.
float musicOn() { return smoothstep(0.05, 0.2, max(sigB().r, sigB().b)); }
float kickAge() { return sigA().a; }   // seconds since the last kick (60 = none for a minute)
float depthReady() { return DEPTH_READY; }   // tussen's own: 1 while the depth model's map is bound
// react 0.5 is the music as the shader was tuned; 0 holds it still, 1 doubles it.
float reactK() { return 2.0 * react; }
// amount 1 is the look as tuned (REST of the old scale); 0 the picture as it came in.
const float REST = 0.9;
float press() { return REST * amount; }
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
vec3 cam(vec2 uv) { return IMG_NORM_PIXEL(inputImage, uv).rgb; }
float hash1(float x) { return fract(sin(x * 91.3458) * 47453.5453); }
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vnoise(vec2 p) {
  vec2 i = floor(p); vec2 f = fract(p); f = f * f * (3.0 - 2.0 * f);
  return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
float fbm(vec2 p) { return 0.5 * vnoise(p) + 0.3 * vnoise(p * 2.1 + 3.7) + 0.2 * vnoise(p * 4.3 - 1.3); }

// the light's colour: cold blue through white to tungsten
vec3 lightColour(float w) {
  vec3 cold = vec3(0.65, 0.8, 1.15); vec3 white = vec3(1.0); vec3 warm = vec3(1.2, 0.85, 0.55);
  return w < 0.5 ? mix(cold, white, w * 2.0) : mix(white, warm, w * 2.0 - 1.0);
}

// the surface's normal from the depth map, with a little detail from the picture's brightness
vec3 normalAt(vec2 uv) {
  vec2 e = 4.0 / RENDERSIZE * 1.5;
  float dx = depthAt(uv + vec2(e.x, 0.0)) - depthAt(uv - vec2(e.x, 0.0));
  float dy = depthAt(uv + vec2(0.0, e.y)) - depthAt(uv - vec2(0.0, e.y));
  vec2 f = 2.0 / RENDERSIZE;
  float lx = lumaOf(cam(uv + vec2(f.x, 0.0))) - lumaOf(cam(uv - vec2(f.x, 0.0)));
  float ly = lumaOf(cam(uv + vec2(0.0, f.y))) - lumaOf(cam(uv - vec2(0.0, f.y)));
  vec2 detail = clamp(vec2(lx, ly), -0.08, 0.08) * 0.5;
  return normalize(vec3(-dx * 9.0 - detail.x, -dy * 9.0 - detail.y, 1.0));
}

// a beat-stepped glide: a new value each beat, reached in the first third of it
float glide(float salt) {
  float i = floor(BEAT);
  float t = smoothstep(0.0, 0.35, fract(BEAT));
  return mix(hash1(i - 1.0 + salt), hash1(i + salt), t) * 2.0 - 1.0;
}

// water light: the edges of drifting cells, warped, so bright lines knot
// and part like sunlight through a moving pool's surface
vec2 h22(vec2 p) { return fract(sin(vec2(dot(p, vec2(127.1, 311.7)), dot(p, vec2(269.5, 183.3)))) * 43758.5453); }
float cells(vec2 p, float t) {
  vec2 i = floor(p); vec2 f = fract(p);
  float f1 = 8.0; float f2 = 8.0;
  for (int y = -1; y <= 1; y++) {
    for (int x = -1; x <= 1; x++) {
      vec2 g = vec2(float(x), float(y));
      vec2 o = h22(i + g);
      o = 0.5 + 0.42 * sin(t * (0.6 + 0.6 * o) + 6.2831 * o);
      float d = length(g + o - f);
      if (d < f1) { f2 = f1; f1 = d; } else if (d < f2) { f2 = d; }
    }
  }
  return f2 - f1;
}
float caustic(vec2 p, float t) {
  p += 0.35 * vec2(fbm(p * 0.7 + t * 0.15), fbm(p * 0.7 + 4.0 - t * 0.12));
  float e1 = cells(p, t);
  float e2 = cells(p * 1.7 + 3.1, t * 1.3);
  return pow(1.0 - smoothstep(0.0, 0.22, e1), 3.0) * 0.9 + pow(1.0 - smoothstep(0.0, 0.16, e2), 3.0) * 0.5;
}

void main() {
  vec2 uv = isf_FragNormCoord;
  if (PASSINDEX == 0.0) { gl_FragColor = sigPass(); return; }
  if (PASSINDEX == 1.0) { gl_FragColor = depthTimePass(uv); return; }

  float kick = kickNow() * kickAmt;
  float mids = midsNow() * midsAmt;
  float hats = highsNow() * highsAmt;
  float m = floor(mode + 0.5);
  float aspect = RENDERSIZE.x / max(RENDERSIZE.y, 1.0);
  vec3 c = cam(uv);
  float d = depthAt(uv);
  float fig = figureAt(uv);
  vec3 n = normalAt(uv);
  vec3 lc = lightColour(warmth);
  float roomLit = mix(1.0, 0.18, room);
  vec3 outc;

  if (m < 0.5) {
    // soft key: a soft box beside the camera, a new place each beat
    float lx = 0.25 + 0.75 * move;
    float side = glide(0.0);
    vec3 L = normalize(vec3(sign(side) * lx * (0.6 + 0.4 * abs(side)), 0.35 + 0.25 * glide(7.0) * move, 0.55));
    float wrap = 0.35 + 0.5 * mids;
    float key = clamp((dot(n, L) + wrap) / (1.0 + wrap), 0.0, 1.0);
    // a cool rim from the other side, flaring on the kick
    vec2 back = -normalize(L.xy + 1e-4);
    float rim = pow(1.0 - n.z, 1.5) * max(dot(normalize(n.xy + 1e-4), back), 0.0);
    vec3 rimCol = lightColour(1.0 - warmth);
    vec3 H = normalize(L + vec3(0.0, 0.0, 1.0));
    float spec = pow(max(dot(n, H), 0.0), 20.0) * (0.15 + 0.8 * hats);
    vec3 figLit = c * (0.22 + 1.25 * key * lc) + rimCol * rim * (0.35 + 1.4 * kick) * (0.4 + lumaOf(c)) + lc * spec * 0.35;
    // the key spills faintly onto the room on its side
    float spill = smoothstep(-0.6, 1.0, (uv.x - 0.5) * sign(L.x) * 2.0) * 0.25;
    vec3 roomC = c * (roomLit + spill * lc * (1.0 - room * 0.5));
    outc = mix(roomC, figLit, fig);
  } else if (m < 1.5) {
    // venetian blinds: slanted slats from a window off to the upper right
    vec2 p = (uv - 0.5) * vec2(aspect, 1.0);
    vec2 dir = normalize(vec2(0.32, 1.0));
    // the slats jump where the figure stands in front of the wall, as a
    // projected pattern does, and curve a little round its shape
    float t = dot(p, dir) + fig * (0.05 + 0.04 * n.x) + (1.0 - fig) * d * 0.05;
    float step4 = floor(BEAT) + smoothstep(0.0, 0.3, fract(BEAT));
    float s = fract(t * slats + step4 * 0.25 * move);
    float shut = mix(0.55, 0.22, kick);
    float edge = 0.03 + 0.12 * (1.0 - d) + 0.1 * mids;
    float lit = smoothstep(shut - edge, shut + edge, s) * (1.0 - smoothstep(1.0 - edge * 0.5, 1.0, s));
    float window = smoothstep(-0.9, 0.7, p.x * 0.6 + p.y * 0.8);   // brighter toward the window
    float facing = 0.75 + 0.25 * clamp(dot(n, normalize(vec3(0.5, 0.6, 0.6))), 0.0, 1.0);
    vec3 grey = vec3(lumaOf(c));
    vec3 surf = mix(c, grey * 1.05, 0.35);
    vec3 shade = surf * mix(0.25, 0.08, room);
    vec3 sun = surf * lc * (1.6 + 0.5 * kick) * window * facing + lc * 0.05 * hats * window;
    vec3 litC = mix(shade, sun, lit);
    outc = mix(litC * mix(1.0, roomLit * 1.6, 1.0 - fig), litC, fig);
  } else if (m < 2.5) {
    // pool caustics: light through moving water, refracted by the surface
    vec2 p = (uv - 0.5) * vec2(aspect, 1.0) * (slats / 9.0) * 0.9;
    p += n.xy * 0.03 * fig + vec2(0.0, d * 0.15);
    float t = TIME * (0.25 + 0.6 * move) + mids * 0.6;
    float cau = caustic(p * 3.0 * (1.0 - 0.05 * kick), t * 1.5);
    cau = clamp(cau, 0.0, 1.5);
    vec3 water = mix(vec3(0.3, 0.85, 1.0), lc, 0.5);
    float glint = smoothstep(0.7, 1.2, cau) * hats;
    vec3 surf = c * (0.42 + 0.15 * water);
    vec3 litC = surf + c * water * cau * (1.8 + 1.6 * kick) + vec3(1.0) * glint * 0.4;
    outc = mix(litC * mix(1.0, roomLit * 1.4, 1.0 - fig), litC, fig);
  } else {
    // leaf dapple: sun through leaves; nearer surfaces show sharper shadows
    vec2 p = (uv - 0.5) * vec2(aspect, 1.0) * (slats / 9.0) * 2.4;
    p += vec2(d * 0.25, 0.0);
    float tt = TIME * (0.15 + 0.5 * move);
    p += vec2(sin(tt * 1.3) + 0.4 * sin(tt * 2.9), 0.6 * cos(tt * 1.1)) * (0.05 + 0.25 * mids);
    p.x += 0.35 * kick;
    float leaf = fbm(p + vec2(tt * 0.2, 0.0));
    float edge = (0.03 + 0.14 * (1.0 - d)) * (1.0 - 0.6 * hats);
    float th = 0.54 - 0.16 * fig;   // the figure stands a little more in the sun
    float sun = smoothstep(th - edge, th + edge, leaf);
    vec3 sunC = lc * mix(1.0, 1.15, warmth) * vec3(1.15, 1.0, 0.8);
    vec3 shadeC = mix(vec3(0.24, 0.32, 0.3), vec3(0.4), fig * 0.5) * mix(1.0, 0.55, room);
    float facing = 0.7 + 0.3 * clamp(dot(n, normalize(vec3(-0.4, 0.7, 0.6))), 0.0, 1.0);
    vec3 litC = c * mix(shadeC, sunC * 1.5 * facing, sun);
    outc = mix(litC * mix(1.0, roomLit * 1.5, 1.0 - fig), litC, fig);
  }
  gl_FragColor = vec4(clamp(mix(c, outc, press()), 0.0, 1.0), 1.0);
}
