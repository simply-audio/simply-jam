/*{
  "DESCRIPTION": "shadows: three coloured lamps throw three shadows of the figure on the wall behind. Where one lamp is blocked the wall takes the other two's colour, where shadows overlap it darkens, and the figure stands lit white in front. Kicks throw the shadows apart (kickAmt), the mids sway them (midsAmt), the outer two trail the figure's moves (lag), and on each bar the lamps can trade colours (cycle).",
  "CREDIT": "Chris Poucet, simply-jam",
  "CATEGORIES": ["whole-look", "figure", "depth", "sound", "beat", "pressure"],
  "INPUTS": [
    { "NAME": "inputImage", "TYPE": "image" },
    { "NAME": "depth", "TYPE": "depth" },
    { "NAME": "wave", "TYPE": "audio" },
    { "NAME": "spectrum", "TYPE": "audioFFT" },
    { "NAME": "amount", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 1.0, "DESCRIPTION": "pressure: how far the lit wall takes over from the camera" },
    { "NAME": "react", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5, "DESCRIPTION": "how much the music moves it: 0 holds it still, 0.5 as tuned, 1 twice as much" },
    { "NAME": "offset", "TYPE": "float", "MIN": 0.0, "MAX": 0.3, "DEFAULT": 0.07, "DESCRIPTION": "how far apart the three shadows stand at rest" },
    { "NAME": "lag", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.45, "DESCRIPTION": "how far the outer shadows trail behind the figure's moves" },
    { "NAME": "wall", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.6, "DESCRIPTION": "0: the lamps light the room as the camera sees it · 1: a plain white wall" },
    { "NAME": "blur", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.35, "DESCRIPTION": "how soft the shadows' edges are" },
    { "NAME": "lamps", "TYPE": "long", "MIN": 0, "MAX": 2, "DEFAULT": 0, "VALUES": [0, 1, 2], "LABELS": ["red green blue", "cyan magenta yellow", "warm and cold"], "DESCRIPTION": "the three lamps' colours" },
    { "NAME": "cycle", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 1.0, "DESCRIPTION": "1: the lamps trade colours on every bar" },
    { "NAME": "kickAmt", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.8, "DESCRIPTION": "kicks throw the shadows apart" },
    { "NAME": "midsAmt", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5, "DESCRIPTION": "mids sway the shadows up and sideways" },
    { "NAME": "highsAmt", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.4, "DESCRIPTION": "hats brighten the shadows' coloured edges" },
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
    { "TARGET": "masks", "WIDTH": "$WIDTH / 4", "HEIGHT": "$HEIGHT / 4", "FLOAT": true, "PERSISTENT": true },
    {}
  ]
}*/

// `masks` holds the figure three times: r now (the middle lamp), g and b
// following it more slowly, so the left and right lamps' shadows trail the
// figure's moves. Each lamp's shadow is its mask read at an offset; the wall
// is lit by every lamp whose shadow does not cover it.

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

vec4 masksPass(vec2 uv) {
  float f = figureAt(uv);
  vec4 prev = IMG_NORM_PIXEL(masks, uv);
  float dt = dtNow();
  float first = 1.0 - step(1.0, FRAMEINDEX);
  float tg = 0.02 + 0.35 * lag;
  float tb = 0.02 + 0.6 * lag;
  float g = mix(prev.g, f, max(1.0 - exp(-dt / tg), first));
  float b = mix(prev.b, f, max(1.0 - exp(-dt / tb), first));
  return vec4(f, g, b, 1.0);
}

// lamp k's colour (k = 0 left, 1 middle, 2 right), rotated along on each bar
vec3 lampColour(float k) {
  float i = mod(k + cycle * floor(BAR), 3.0);
  float set = floor(lamps + 0.5);
  vec3 a = vec3(1.0, 0.12, 0.1); vec3 b = vec3(0.1, 1.0, 0.2); vec3 c = vec3(0.15, 0.3, 1.0);
  if (set > 0.5) { a = vec3(0.1, 0.9, 1.0); b = vec3(1.0, 0.15, 0.85); c = vec3(1.0, 0.9, 0.1); }
  if (set > 1.5) { a = vec3(1.0, 0.55, 0.15); b = vec3(0.9, 0.85, 0.8); c = vec3(0.2, 0.5, 1.0); }
  return i < 0.5 ? a : (i < 1.5 ? b : c);
}

// lamp k's shadow at uv: its mask channel, shifted and softened
float shadowOf(vec2 uv, vec2 off, float ch) {
  vec2 r = vec2(0.012, 0.02) * blur;
  vec2 q = uv - off;
  vec4 s = IMG_NORM_PIXEL(masks, clamp(q, 0.0, 1.0)) * 2.0
         + IMG_NORM_PIXEL(masks, clamp(q + vec2(r.x, 0.0), 0.0, 1.0))
         + IMG_NORM_PIXEL(masks, clamp(q - vec2(r.x, 0.0), 0.0, 1.0))
         + IMG_NORM_PIXEL(masks, clamp(q + vec2(0.0, r.y), 0.0, 1.0))
         + IMG_NORM_PIXEL(masks, clamp(q - vec2(0.0, r.y), 0.0, 1.0));
  s /= 6.0;
  float v = ch < 0.5 ? s.r : (ch < 1.5 ? s.g : s.b);
  float inside = step(0.0, q.x) * step(q.x, 1.0) * step(0.0, q.y) * step(q.y, 1.0);
  return smoothstep(0.3, 0.7, v) * inside;
}

void main() {
  vec2 uv = isf_FragNormCoord;
  if (PASSINDEX == 0.0) { gl_FragColor = sigPass(); return; }
  if (PASSINDEX == 1.0) { gl_FragColor = depthTimePass(uv); return; }
  if (PASSINDEX == 2.0) { gl_FragColor = masksPass(uv); return; }

  float kick = kickNow();
  float mids = midsNow();
  float hats = highsNow();
  vec3 c = cam(uv);
  float fig = figureAt(uv);

  // the lamps stand left, middle-low and right: shadows fall the other way
  float apart = offset + 0.12 * kickAmt * kick;
  float sway = 0.03 * midsAmt * mids;
  vec2 offL = vec2(apart, 0.015) + sway * vec2(sin(TIME * 0.9), cos(TIME * 0.7));
  vec2 offM = vec2(0.0, 0.03 + 0.5 * apart) + sway * vec2(sin(TIME * 0.6 + 2.0), 1.0);
  vec2 offR = vec2(-apart, 0.015) + sway * vec2(sin(TIME * 0.8 + 4.0), cos(TIME * 1.1 + 1.0));
  float sL = shadowOf(uv, offL, 1.0);
  float sM = shadowOf(uv, offM, 0.0);
  float sR = shadowOf(uv, offR, 2.0);
  vec3 cL = lampColour(0.0);
  vec3 cM = lampColour(1.0);
  vec3 cR = lampColour(2.0);

  float l = lumaOf(c);
  vec3 surface = mix(c * 1.25, vec3(0.75 + 0.25 * l), wall);
  vec3 light = cL * (1.0 - sL) + cM * (1.0 - sM) + cR * (1.0 - sR);
  vec3 lit = surface * light * 0.62;
  // a coloured fringe where a shadow's edge lies, brighter on the hats
  float edges = sL * (1.0 - sL) + sM * (1.0 - sM) + sR * (1.0 - sR);
  lit += light * edges * 0.6 * highsAmt * hats;
  // the figure stands in front, lit by all three (near white), its edges tinted
  float nearL = figureAt(clamp(uv + vec2(0.01, 0.0), 0.0, 1.0));
  float nearR = figureAt(clamp(uv - vec2(0.01, 0.0), 0.0, 1.0));
  vec3 figLit = c * 1.15 + 0.25 * (cL * (1.0 - nearR) + cR * (1.0 - nearL));
  vec3 outc = mix(lit, figLit, fig);
  gl_FragColor = vec4(clamp(mix(c, outc, amount), 0.0, 1.0), 1.0);
}
