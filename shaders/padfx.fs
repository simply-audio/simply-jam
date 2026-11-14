/*{
  "DESCRIPTION": "Pad FX: 24 performance effects, each an fx_* float 0..1 (pad pressure: harder = stronger), all 0 = the picture untouched. Sits last on every chain, never swapped, so the pads work on whichever look is live.",
  "CREDIT": "audiodev set; maths from the simply-creative FX library (12, 13, 23, 26, 27, 28, 41, 42, 50, 52, 71, 95, 131 …)",
  "CATEGORIES": ["audiodev"],
  "INPUTS": [
    { "NAME": "inputImage", "TYPE": "image" },
    { "NAME": "wave", "TYPE": "audio" },
    { "NAME": "spectrum", "TYPE": "audioFFT" },
    { "NAME": "fx_zoom", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_shake", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_spin", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_mirror", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_kaleido", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_tile", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_ripple", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_twirl", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_pixel", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_slices", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_glitch", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_rgb", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_blur", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_trails", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_tunnel", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_invert", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_mono", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_hue", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_posterize", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_thermal", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_strobe", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_blackout", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_flash", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_scan", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "gain", "TYPE": "float", "MIN": 0.25, "MAX": 8.0, "DEFAULT": 1.0 }
  ],
  "PASSES": [
    { "TARGET": "sig", "WIDTH": "2", "HEIGHT": "1", "FLOAT": true, "PERSISTENT": true },
    { "TARGET": "fb", "PERSISTENT": true },
    {}
  ]
}*/

// ── grab these first ────────────────────────────────────────────────
const float ZOOM_MAX    = 0.45;  // fraction of the frame cut away at full press
const float SHAKE_PX    = 0.035; // uv, at full press, kicked harder on a kick
const float SPIN_TURNS  = 0.5;   // half a turn at full press
const float KALEIDO_N   = 6.0;   // segments
const float RIPPLE_AMP  = 0.03;
const float TWIRL_TURNS = 1.2;
const float PIXEL_MAX   = 64.0;  // cells across at full press (fewer = bigger)
const float RGB_MAX     = 0.03;
const float BLUR_MAX    = 0.02;
const vec3  THERMAL_LO  = vec3(0.0, 0.0, 0.05);
// ──────────────────────────────────────────────────────────────────

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
float kickNow() { return sigA().r; }
// ═══════════════════════════════════════════════════════════════════════

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float lumaOf(vec3 c) { return dot(c, vec3(0.2126, 0.7152, 0.0722)); }
vec2 rot(vec2 p, float a) { float c = cos(a); float s = sin(a); return vec2(c * p.x - s * p.y, s * p.x + c * p.y); }
vec3 hueRotate(vec3 c, float turns) {
  float a = 6.2831853 * turns;
  vec3 k = vec3(0.57735);
  return c * cos(a) + cross(k, c) * sin(a) + k * dot(k, c) * (1.0 - cos(a));
}
vec3 ironbow(float t) {
  t = clamp(t, 0.0, 1.0) * 5.0;
  vec3 c0 = THERMAL_LO; vec3 c1 = vec3(0.20, 0.02, 0.45); vec3 c2 = vec3(0.75, 0.05, 0.50);
  vec3 c3 = vec3(1.00, 0.45, 0.05); vec3 c4 = vec3(1.00, 0.85, 0.20); vec3 c5 = vec3(1.0, 1.0, 0.92);
  if (t < 1.0) { return mix(c0, c1, t); }
  if (t < 2.0) { return mix(c1, c2, t - 1.0); }
  if (t < 3.0) { return mix(c2, c3, t - 2.0); }
  if (t < 4.0) { return mix(c3, c4, t - 3.0); }
  return mix(c4, c5, t - 4.0);
}

// Where to read the picture: every geometric pad, in order.
vec2 warpUV(vec2 uv, float kick) {
  float aspect = RENDERSIZE.x / max(RENDERSIZE.y, 1.0);
  vec2 p = (uv - 0.5) * vec2(aspect, 1.0);
  if (fx_shake > 0.001) {
    float t = floor(TIME * 24.0);
    p += (vec2(hash(vec2(t, 1.0)), hash(vec2(t, 2.0))) - 0.5) * 2.0 * SHAKE_PX * fx_shake * (0.4 + kick);
  }
  if (fx_zoom > 0.001) { p *= 1.0 - ZOOM_MAX * fx_zoom * (0.8 + 0.2 * kick); }
  if (fx_spin > 0.001) { p = rot(p, 6.2831853 * SPIN_TURNS * fx_spin * fx_spin); }
  if (fx_twirl > 0.001) {
    float r = length(p);
    p = rot(p, 6.2831853 * TWIRL_TURNS * fx_twirl * smoothstep(0.7, 0.0, r));
  }
  if (fx_ripple > 0.001) {
    float r = length(p);
    p += normalize(p + 1e-5) * RIPPLE_AMP * fx_ripple * sin(40.0 * r - TIME * 10.0) * (0.5 + kick);
  }
  if (fx_kaleido > 0.001) {
    float r = length(p);
    float a = atan(p.y, p.x) + TIME * 0.2;
    float seg = 6.2831853 / KALEIDO_N;
    float m = mod(a, seg);
    m = min(m, seg - m);
    vec2 k = vec2(cos(m), sin(m)) * r;
    p = mix(p, k, smoothstep(0.0, 0.25, fx_kaleido));
  }
  vec2 q = p / vec2(aspect, 1.0) + 0.5;
  if (fx_mirror > 0.001) { q.x = mix(q.x, 0.5 - abs(q.x - 0.5), step(0.15, fx_mirror)); }
  if (fx_tile > 0.001) { q = fract(q * floor(1.0 + fx_tile * 3.99)); }
  if (fx_pixel > 0.001) {
    float cells = mix(320.0, PIXEL_MAX * 0.25, fx_pixel);
    vec2 cs = vec2(cells, cells / aspect);
    q = (floor(q * cs) + 0.5) / cs;
  }
  if (fx_slices > 0.001) {
    float row = floor(q.y * 18.0);
    q.x += (hash(vec2(row, floor(TIME * 8.0))) - 0.5) * 0.25 * fx_slices * step(0.4, hash(vec2(row, 7.0)));
  }
  if (fx_glitch > 0.001) {
    vec2 b = floor(q * vec2(12.0, 8.0));
    float h = hash(b + floor(TIME * 10.0));
    float on = step(1.0 - 0.35 * fx_glitch * (0.5 + kick), h);
    q += on * (vec2(hash(b + 3.1), hash(b + 5.7)) - 0.5) * 0.2;
  }
  return q;
}

vec3 readAt(vec2 q) {
  vec3 c = IMG_NORM_PIXEL(inputImage, q).rgb;
  if (fx_rgb > 0.001) {
    vec2 d = (q - 0.5) * RGB_MAX * fx_rgb * 4.0;
    c = vec3(IMG_NORM_PIXEL(inputImage, q + d).r, c.g, IMG_NORM_PIXEL(inputImage, q - d).b);
  }
  if (fx_blur > 0.001) {
    vec3 acc = c;
    for (int i = 0; i < 12; i++) {
      float a = float(i) * 2.39996;
      float r = sqrt((float(i) + 0.5) / 12.0) * BLUR_MAX * fx_blur;
      acc += IMG_NORM_PIXEL(inputImage, q + vec2(cos(a), sin(a) * RENDERSIZE.x / max(RENDERSIZE.y, 1.0)) * r).rgb;
    }
    c = acc / 13.0;
  }
  return c;
}

vec3 grade(vec3 c, float kick) {
  if (fx_hue > 0.001) { c = clamp(hueRotate(c, fx_hue + 0.1 * kick * fx_hue), 0.0, 1.0); }
  if (fx_posterize > 0.001) {
    float steps = mix(12.0, 2.0, fx_posterize);
    c = mix(c, floor(c * steps + 0.5) / steps, smoothstep(0.0, 0.1, fx_posterize));
  }
  if (fx_thermal > 0.001) { c = mix(c, ironbow(lumaOf(c) * 1.2 + 0.2 * kick), fx_thermal); }
  if (fx_mono > 0.001) {
    float l = lumaOf(c);
    c = mix(c, vec3(clamp((l - 0.5) * (1.0 + fx_mono) + 0.5, 0.0, 1.0)), fx_mono);
  }
  if (fx_invert > 0.001) { c = mix(c, 1.0 - c, fx_invert); }
  return c;
}

void main() {
  vec2 uv = isf_FragNormCoord;
  if (PASSINDEX == 0.0) { gl_FragColor = sigPass(); return; }
  float kick = kickNow();

  if (PASSINDEX == 1.0) {
    vec2 q = warpUV(uv, kick);
    vec3 c = grade(readAt(q), kick);
    // feedback: trails keep the brighter past, the tunnel pulls it inward and turns it
    if (fx_trails > 0.001) {
      vec3 prev = IMG_NORM_PIXEL(fb, uv).rgb;
      c = max(c, prev * mix(0.6, 0.97, fx_trails));
    }
    if (fx_tunnel > 0.001) {
      vec2 p = rot(uv - 0.5, 0.03 * fx_tunnel) * (1.0 - 0.06 * fx_tunnel) + 0.5;
      vec3 prev = IMG_NORM_PIXEL(fb, p).rgb;
      float keep = mix(0.0, 0.92, fx_tunnel) * (1.0 - 0.6 * smoothstep(0.15, 0.6, lumaOf(c)));
      c = mix(c, prev, keep);
    }
    gl_FragColor = vec4(c, 1.0);
    return;
  }

  vec3 c = IMG_NORM_PIXEL(fb, uv).rgb;
  if (fx_scan > 0.001) {
    float line = 0.5 + 0.5 * sin((isf_FragNormCoord.y * RENDERSIZE.y) * 3.14159 * 0.5);
    c *= 1.0 - 0.6 * fx_scan * line;
    c *= 1.0 + 0.3 * fx_scan * (hash(vec2(floor(TIME * 30.0), 1.0)) - 0.5);
  }
  if (fx_strobe > 0.001) {
    // 16ths of the Link beat: on for the first third of each
    float on = step(fract(PHASE * 4.0), 0.33);
    c = mix(c, vec3(1.0), on * fx_strobe);
  }
  if (fx_flash > 0.001) { c = mix(c, vec3(1.0), fx_flash * (0.4 + 0.6 * max(exp(-5.0 * PHASE), kick))); }
  if (fx_blackout > 0.001) { c *= 1.0 - fx_blackout; }
  gl_FragColor = vec4(clamp(c, 0.0, 1.0), 1.0);
}
