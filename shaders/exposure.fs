/*{
  "DESCRIPTION": "exposure: what a camera sees with its shutter held open, kept in one buffer. light painting: whatever is bright (a lamp, the pads, a lit face) leaves trails of light behind it, drifting up on the mids, flaring on the kicks, more of the picture painting on the hats · motion glow: only what moves lights up, in a gel that changes each bar, the still room sinking away; kicks make it more sensitive · afterimage: the eye's own after-image, a ghost in the opposite colour wherever something just was, deeper on the kicks, longer on the mids · second curtain: a long exposure that smears every move, re-exposed sharp on each kick, like a slow shutter with a flash at its end.",
  "CREDIT": "Chris Poucet, simply-jam",
  "CATEGORIES": ["time", "feedback", "depth", "sound", "beat", "modes", "pressure"],
  "INPUTS": [
    { "NAME": "inputImage", "TYPE": "image" },
    { "NAME": "depth", "TYPE": "depth" },
    { "NAME": "wave", "TYPE": "audio" },
    { "NAME": "spectrum", "TYPE": "audioFFT" },
    { "NAME": "amount", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.85, "DESCRIPTION": "pressure: how strong the exposure is over the live picture" },
    { "NAME": "react", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5, "DESCRIPTION": "how much the music moves it: 0 holds it still, 0.5 as tuned, 1 twice as much" },
    { "NAME": "mode", "TYPE": "long", "MIN": 0, "MAX": 3, "DEFAULT": 0, "VALUES": [0, 1, 2, 3], "LABELS": ["light painting", "motion glow", "afterimage", "second curtain"] },
    { "NAME": "trail", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5, "DESCRIPTION": "how long the shutter stays open: how long trails, glows and ghosts last" },
    { "NAME": "dark", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5, "DESCRIPTION": "how far the live picture dims under the light (light painting, motion glow)" },
    { "NAME": "tint", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.35, "DESCRIPTION": "light painting: 0 the trails keep the picture's colour, 1 they take a gel that changes on each bar" },
    { "NAME": "figureSharp", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0, "DESCRIPTION": "second curtain: 1 keeps the figure sharp, only the room smears" },
    { "NAME": "kickAmt", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.7, "DESCRIPTION": "kicks flare the trails, sharpen the motion, deepen the ghost, re-expose the long exposure" },
    { "NAME": "midsAmt", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5, "DESCRIPTION": "mids lift the trails upward and lengthen the ghosts" },
    { "NAME": "highsAmt", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5, "DESCRIPTION": "hats let dimmer things paint and sparkle the glow" },
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
    { "TARGET": "acc", "FLOAT": true, "PERSISTENT": true },
    { "TARGET": "last", "WIDTH": "$WIDTH / 2", "HEIGHT": "$HEIGHT / 2", "FLOAT": true, "PERSISTENT": true },
    {}
  ]
}*/

// `acc` is the exposure (rgb), written each frame from itself and the
// camera; `last` is the camera's previous frame at half size, for what
// moved. Every decay is per second (dtNow), so trails last as long at 30
// frames a second as at 120.

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
vec3 cam(vec2 uv) { return IMG_NORM_PIXEL(inputImage, uv).rgb; }
vec3 hsv(float h, float s, float v) {
  vec3 k = clamp(abs(fract(h + vec3(0.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0) - 1.0, 0.0, 1.0);
  return v * mix(vec3(1.0), k, s);
}
float hash1(float x) { return fract(sin(x * 91.3458) * 47453.5453); }
// a gel for bar b: hues a third apart or so, never two alike in a row
vec3 gel(float b) { return hsv(fract(0.07 + 0.37 * b + 0.15 * hash1(b)), 0.8, 1.0); }
// a kick landed this frame (from the sound, or the Link beat with beatSync)
float kickEdge() {
  float snd = step(kickAge(), 0.5 * dtNow());
  float link = step(fract(BEAT), dtNow() * TEMPO / 60.0) * step(0.5, beatSync);
  return max(snd, link);
}
float holdOf(float tau) { return exp(-dtNow() / max(tau, 1e-3)); }

vec4 accPass(vec2 uv, float m) {
  float fresh = 1.0 - step(1.0, FRAMEINDEX);
  vec3 c = cam(uv);
  float kick = kickNow() * kickAmt;
  float mids = midsNow() * midsAmt;
  float hats = highsNow() * highsAmt;
  if (m < 0.5) {
    // light painting: the brightest things, kept at their brightest, fading slowly
    float rise = dtNow() * (0.02 + 0.2 * mids);
    vec3 prev = IMG_NORM_PIXEL(acc, uv - vec2(0.0, rise)).rgb * (1.0 - fresh);
    float l = lumaOf(c);
    float thr = mix(0.78, 0.5, hats);
    vec3 hl = c * smoothstep(thr, thr + 0.18, l);
    hl = mix(hl, gel(floor(BAR)) * lumaOf(hl) * 1.4, tint);
    return vec4(max(prev * holdOf(mix(0.25, 3.0, trail)), hl), 1.0);
  }
  if (m < 1.5) {
    // motion glow: how far each pixel moved since the last frame, as light
    vec3 prev = IMG_NORM_PIXEL(acc, uv).rgb * (1.0 - fresh);
    vec3 was = IMG_NORM_PIXEL(last, uv).rgb;
    float mo = length(c - was) * (1.0 - fresh);
    float sens = 0.02 + 0.08 * (1.0 - kick);
    float glow = smoothstep(sens, sens + 0.2, mo);
    vec3 col = mix(gel(floor(BAR)), c * 1.6 + 0.1, 0.35) * glow;
    return vec4(max(prev * holdOf(mix(0.15, 1.6, trail)), col), 1.0);
  }
  if (m < 2.5) {
    // afterimage: the eye's adaptation, a slow average of what it has seen
    vec3 prev = IMG_NORM_PIXEL(acc, uv).rgb;
    float tau = mix(0.25, 1.6, trail) * (1.0 + 1.5 * mids);
    return vec4(mix(c, prev, holdOf(tau) * (1.0 - fresh)), 1.0);
  }
  // second curtain: a long exposure, re-exposed (sharp) on the kick
  vec3 prev = IMG_NORM_PIXEL(acc, uv).rgb;
  float tau = mix(0.08, 1.0, trail);
  vec3 e = mix(c, prev, holdOf(tau) * (1.0 - fresh));
  return vec4(mix(e, c, kickEdge() * kickAmt), 1.0);
}

void main() {
  vec2 uv = isf_FragNormCoord;
  if (PASSINDEX == 0.0) { gl_FragColor = sigPass(); return; }
  if (PASSINDEX == 1.0) { gl_FragColor = depthTimePass(uv); return; }
  float m = floor(mode + 0.5);
  if (PASSINDEX == 2.0) { gl_FragColor = accPass(uv, m); return; }
  if (PASSINDEX == 3.0) { gl_FragColor = vec4(cam(uv), 1.0); return; }

  float kick = kickNow() * kickAmt;
  float mids = midsNow() * midsAmt;
  float hats = highsNow() * highsAmt;
  vec3 c = cam(uv);
  vec3 a = IMG_NORM_PIXEL(acc, uv).rgb;
  vec3 outc;
  if (m < 0.5) {
    vec3 base = c * mix(1.0, 0.3, dark);
    vec3 light = a * (0.9 + 0.9 * kick);
    outc = 1.0 - (1.0 - base) * (1.0 - clamp(light, 0.0, 1.0));   // screen: light adds, never clips harshly
  } else if (m < 1.5) {
    vec3 base = c * mix(1.0, 0.18, dark);
    float sparkle = 1.0 + 0.6 * hats * step(0.6, fract(sin(dot(floor(uv * RENDERSIZE / 2.0), vec2(12.9898, 78.233)) + floor(TIME * 20.0)) * 43758.5453));
    outc = base + a * (1.0 + 0.5 * kick) * sparkle;
  } else if (m < 2.5) {
    // where the picture is darker than what the eye got used to, its
    // opposite colour shows; where it's brighter, it glares a little
    vec3 ghost = c - a;
    float g = lumaOf(ghost);
    vec3 chroma = ghost - g;
    outc = c + (1.2 + 1.5 * kick) * (chroma * 1.6 + g * 0.6);
  } else {
    float fig = figureAt(uv) * figureSharp;
    outc = mix(a, c, max(fig, 0.15 + 0.35 * kick));
    outc *= 1.0 + 0.15 * kick;
  }
  gl_FragColor = vec4(clamp(mix(c, outc, amount), 0.0, 1.0), 1.0);
}
