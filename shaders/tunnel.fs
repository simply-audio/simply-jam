/*{
  "DESCRIPTION": "Feedback tunnels: the last frame folded back into this one. mode 0 tunnel (FX 50: the room recedes into rings), 1 zoom-rotate spiral (FX 131: classic video feedback, colour rolling per echo), 2 kaleido tunnel (FX 139: an 8-wedge kaleidoscope diving inward). figureOnTop keeps the figure live and sharp over its own tunnel (modes 0, 1; needs depth).",
  "CREDIT": "Chris Poucet, simply-creative FX 50, 131, 139, ported to tussen ISF",
  "CATEGORIES": ["time", "feedback", "depth", "sound", "beat", "modes", "pressure"],
  "INPUTS": [
    { "NAME": "inputImage", "TYPE": "image" },
    { "NAME": "depth", "TYPE": "depth" },
    { "NAME": "wave", "TYPE": "audio" },
    { "NAME": "spectrum", "TYPE": "audioFFT" },
    { "NAME": "amount", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.6 },
    { "NAME": "react", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5, "DESCRIPTION": "how much the music moves it: 0 holds it still, 0.5 as tuned, 1 twice as much" },
    { "NAME": "mode", "TYPE": "long", "MIN": 0, "MAX": 2, "DEFAULT": 2, "VALUES": [0, 1, 2], "LABELS": ["tunnel", "spiral", "kaleido tunnel"] },
    { "NAME": "figureOnTop", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 1.0 },
    { "NAME": "kickAmt", "TYPE": "float", "MIN": 0.0, "MAX": 2.0, "DEFAULT": 1.0 },
    { "NAME": "midsAmt", "TYPE": "float", "MIN": 0.0, "MAX": 2.0, "DEFAULT": 1.0 },
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
    { "TARGET": "fb", "PERSISTENT": true },
    {}
  ]
}*/

// mode 0 · tunnel           FX 50 Tunnel
// mode 1 · spiral           FX 131 Feedback zoom-rotate
// mode 2 · kaleido tunnel   FX 139 Kaleido tunnel
// (the "room" was the NDI picture; here it is the camera itself)

// ── grab these first ────────────────────────────────────────────────
// mode 0 (FX 50)
const float T_SHRINK_MAX = 0.06;  const float T_TWIST_MAX = 0.012; const float T_FEED_MAX = 0.93;
const float T_FADE = 0.985;       const float T_KICK_ENGAGE = 0.8; const float T_MIDS_ENGAGE = 0.25;
const float T_KICK_SHRINK = 1.0;  const float T_KICK_GLOW = 0.6;   const float T_MIDS_TWIST = 2.0;
// mode 1 (FX 131)
const float S_FEED = 0.94;        const float S_ZOOM = 1.025;      const float S_TURN = 0.012;
const float S_HUE_DRIFT = 0.015;  const float S_KICK_ENGAGE = 0.85; const float S_MIDS_ENGAGE = 0.25;
const float S_KICK_TURN = 2.0;    const float S_MIDS_TURN = 1.0;
// mode 2 (FX 139)
const float K_SEGMENTS = 8.0;     const float K_SPIN = 0.15;       const float K_SRC_ZOOM = 0.8;
const float K_SHRINK = 0.94;      const float K_FEEDBACK = 0.86;   const float K_TWIST = 0.01;
const float K_KICK_ENGAGE = 0.9;  const float K_MIDS_ENGAGE = 0.3; const float K_KICK_DIVE = 0.06;
const float K_MIDS_SPIN = 1.0;    const float K_CURVE = 0.7;       const float K_KICK_FLASH = 0.25;
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
vec4 depthPass(vec2 uv) {
  float d = fallbackDepth(uv);
  d = mix(IMG_NORM_PIXEL(depth, uv).r, d, max(depthFallback, 1.0 - depthReady()));
  float prev = IMG_NORM_PIXEL(depthBuf, uv).r;
  return vec4(mix(d, prev, depthSmooth * step(1.0, FRAMEINDEX)), 0.0, 0.0, 1.0);
}
float depthAt(vec2 uv) { return IMG_NORM_PIXEL(depthBuf, uv).r; }
float figureAt(vec2 uv) { return smoothstep(figure - soft, figure + soft, depthAt(uv)); }
// ═══════════════════════════════════════════════════════════════════════

vec2 rot(vec2 p, float a) { float c = cos(a); float s = sin(a); return vec2(c * p.x - s * p.y, s * p.x + c * p.y); }
vec3 roll(vec3 c, float a) {
  vec3 k = vec3(0.57735);
  return c * cos(a) + cross(k, c) * sin(a) + k * dot(k, c) * (1.0 - cos(a));
}
vec3 cam(vec2 uv) { return IMG_NORM_PIXEL(inputImage, uv).rgb; }
float inside(vec2 q) { return step(0.0, q.x) * step(q.x, 1.0) * step(0.0, q.y) * step(q.y, 1.0); }

// what the feedback buffer holds this frame: the tunnel itself
vec3 feedbackPass(vec2 uv, float m, float kick, float mids) {
  float aspect = RENDERSIZE.x / max(RENDERSIZE.y, 1.0);
  vec2 p = (uv - 0.5) * vec2(aspect, 1.0);
  if (m < 0.5) {
    float audio = clamp(kickAmt * T_KICK_ENGAGE * kick + midsAmt * T_MIDS_ENGAGE * mids, 0.0, 1.0);
    float amt = clamp(amount + audio, 0.0, 1.0);
    float feed = max(smoothstep(0.0, 0.15, amount), audio);
    float k = 1.0 + T_SHRINK_MAX * amt * (1.0 + T_KICK_SHRINK * kick);
    float a = T_TWIST_MAX * amt * (1.0 + T_MIDS_TWIST * mids);
    vec2 q = rot(p * k, -a);
    vec2 pu = q / vec2(aspect, 1.0) + 0.5;
    vec3 prev = mix(cam(uv), IMG_NORM_PIXEL(fb, pu).rgb * T_FADE, inside(pu) * step(1.5, FRAMEINDEX));
    return mix(cam(uv), prev, T_FEED_MAX * feed);
  }
  if (m < 1.5) {
    float amt = clamp(amount + kickAmt * S_KICK_ENGAGE * kick + midsAmt * S_MIDS_ENGAGE * mids, 0.0, 1.0);
    float ang = S_TURN * (1.0 + S_KICK_TURN * kick + S_MIDS_TURN * mids);
    vec2 q = rot(p, ang) / S_ZOOM;
    vec2 pu = q / vec2(aspect, 1.0) + 0.5;
    vec3 prev = roll(IMG_NORM_PIXEL(fb, pu).rgb, S_HUE_DRIFT * 6.2831853) * inside(pu) * step(1.5, FRAMEINDEX);
    float k = S_FEED * smoothstep(0.0, 1.0, amt);
    vec3 room = mix(cam(uv), prev, k);
    return max(room, cam(uv) * (1.0 - k));
  }
  float amt = clamp(amount + kickAmt * K_KICK_ENGAGE * kick + midsAmt * K_MIDS_ENGAGE * mids, 0.0, 1.0);
  float r = length(p);
  float a = atan(p.y, p.x) + TIME * (K_SPIN + K_MIDS_SPIN * mids);
  float w = 6.2831853 / K_SEGMENTS;
  a = mod(a, w);
  a = abs(a - w * 0.5);
  vec2 q = vec2(cos(a), sin(a)) * r * K_SRC_ZOOM;
  vec2 suv = q / vec2(aspect, 1.0) + 0.5;
  suv = 1.0 - abs(1.0 - mod(suv, 2.0));
  vec3 kal = cam(suv);
  float s = K_SHRINK - K_KICK_DIVE * kick;
  vec2 puv = rot(p, K_TWIST) / s / vec2(aspect, 1.0) + 0.5;
  vec3 prev = mix(kal, IMG_NORM_PIXEL(fb, puv).rgb, inside(puv) * step(1.5, FRAMEINDEX));
  float fbw = K_FEEDBACK * amt * smoothstep(0.0, 0.25, r + 0.02);
  return mix(kal, max(prev, kal * 0.6), fbw);
}

void main() {
  vec2 uv = isf_FragNormCoord;
  if (PASSINDEX == 0.0) { gl_FragColor = sigPass(); return; }
  if (PASSINDEX == 1.0) { gl_FragColor = depthPass(uv); return; }
  float m = floor(mode + 0.5);
  float kick = kickNow();
  float mids = midsNow();
  if (PASSINDEX == 2.0) { gl_FragColor = vec4(feedbackPass(uv, m, kick, mids), 1.0); return; }

  vec3 room = IMG_NORM_PIXEL(fb, uv).rgb;
  vec3 col;
  if (m < 0.5) {
    float audio = clamp(kickAmt * T_KICK_ENGAGE * kick + midsAmt * T_MIDS_ENGAGE * mids, 0.0, 1.0);
    float feed = max(smoothstep(0.0, 0.15, amount), audio);
    vec3 fig = cam(uv) * (1.0 + T_KICK_GLOW * kick * feed);
    col = mix(room, fig, figureAt(uv) * figureOnTop);
  } else if (m < 1.5) {
    col = mix(room, cam(uv), figureAt(uv) * figureOnTop);
  } else {
    float amt = clamp(amount + kickAmt * K_KICK_ENGAGE * kick + midsAmt * K_MIDS_ENGAGE * mids, 0.0, 1.0);
    vec3 k = room * (1.0 + K_KICK_FLASH * kick);
    col = mix(cam(uv), k, pow(amt, K_CURVE));
  }
  gl_FragColor = vec4(clamp(col, 0.0, 1.0), 1.0);
}
