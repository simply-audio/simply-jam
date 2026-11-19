/*{
  "DESCRIPTION": "Wash: the room washed in one colour that turns with the mids, the figure left clear; each kick lifts the wash. (The template: rename it, say what yours does and what it reacts to.)",
  "CREDIT": "Your Name, for simply-jam (MIT); the music and depth blocks are tussen's standard shaders'",
  "CATEGORIES": ["colour", "figure", "depth", "sound", "beat", "pressure"],
  "INPUTS": [
    { "NAME": "inputImage", "TYPE": "image" },
    { "NAME": "spectrum", "TYPE": "audioFFT" },
    { "NAME": "depth", "TYPE": "depth" },
    { "NAME": "amount", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 1.0 },
    { "NAME": "react", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5 },
    { "NAME": "colour", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.85 },
    { "NAME": "cut", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.55 }
  ],
  "PASSES": [
    { "TARGET": "sig", "WIDTH": "2", "HEIGHT": "1", "FLOAT": true, "PERSISTENT": true },
    { "TARGET": "near", "WIDTH": "$WIDTH / 4", "HEIGHT": "$HEIGHT / 4", "FLOAT": true },
    {}
  ]
}*/

// ── shared: the music (the same in every standard shader) ─────────────
// Pass 0 listens once a frame and keeps what it heard in `sig` (2 × 1,
// kept from frame to frame, half floats: never TIME itself, only its
// fraction). Pixel 0: bass, mids, highs (0..1, smoothed), bass's slow
// average. Pixel 1: hit (1 on a kick, then decaying), fract(TIME), level,
// 1 once written.
// A band of the spectrum: the mean of 12 of its bins, both channels at
// once (read between the left's row and the right's).
float band(float lo, float hi) {
  float s = 0.0;
  for (int i = 0; i < 12; i++) {
    float x = (floor(mix(lo, hi + 1.0, (float(i) + 0.5) / 12.0)) + 0.5) / 256.0;
    s += IMG_NORM_PIXEL(spectrum, vec2(x, 0.5)).r;
  }
  return s / 12.0;
}
vec4 heard(float i) { return IMG_NORM_PIXEL(sig, vec2((i + 0.5) / 2.0, 0.5)); }
vec4 listen() {
  vec4 a = heard(0.0);
  vec4 b = heard(1.0);
  float seen = step(0.5, b.a);
  float dt = mix(1.0 / 60.0, clamp(fract(fract(TIME) - b.g + 1.0), 1.0 / 240.0, 0.1), seen);
  float lo = 1.0 - exp(-8.0 * band(0.0, 2.0));
  float mid = 1.0 - exp(-40.0 * band(4.0, 40.0));
  float hi = 1.0 - exp(-120.0 * band(40.0, 160.0));
  float avg = mix(lo, a.a + (1.0 - exp(-dt / 0.6)) * (lo - a.a), seen);
  float h = b.r * exp(-dt / 0.18) * seen;
  h = mix(h, 1.0, step(avg * 1.25 + 0.04, lo) * step(h, 0.5));
  if (isf_FragNormCoord.x < 0.5) {
    return vec4(mix(a.rgb, vec3(lo, mid, hi), mix(1.0, 1.0 - exp(-dt / 0.06), seen)), avg);
  }
  return vec4(h, fract(TIME), max(lo, max(mid, hi)), 1.0);
}
float bass() { return heard(0.0).r; }
float mids() { return heard(0.0).g; }
float highs() { return heard(0.0).b; }
float hit() { return heard(1.0).r; }
float level() { return heard(1.0).b; }
// The kick, or the beat's own pulse while music plays (silence: 0).
float pulse() { return max(hit(), exp(-6.0 * PHASE) * smoothstep(0.05, 0.2, level())); }

float luma(vec3 c) { return dot(c, vec3(0.2126, 0.7152, 0.0722)); }
float hash(vec2 p) {
  vec2 q = fract(p * vec2(123.34, 456.21));
  q += dot(q, q + 45.32);
  return fract(q.x * q.y);
}
float noise(vec2 p) {
  vec2 i = floor(p);
  vec2 f = fract(p);
  f = f * f * (3.0 - 2.0 * f);
  return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
vec3 hue(float h) { return clamp(abs(fract(h + vec3(0.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0) - 1.0, 0.0, 1.0); }
vec3 picture(vec2 uv) { return IMG_NORM_PIXEL(inputImage, uv).rgb; }
// ── end of the shared music block ──────────────────────────────────────
// ── shared: depth (the same in every standard shader that reads depth) ─
// How near each point is, 1 near … 0 far: the depth model's map while one
// is bound (DEPTH_READY). Without it (not installed yet, not a Mac), a
// guess from the picture, bright and central reading near, so the look
// still holds: pass 1 makes it once a frame, at a quarter size, in `near`.
vec4 nearPass(vec2 uv) {
  if (DEPTH_READY > 0.5) { return vec4(0.0); }
  // Brightness blurred over a ring of taps, so the guess is smooth.
  float l = luma(picture(uv));
  for (int i = 0; i < 6; i++) {
    float a = float(i) * 1.0472;
    l += luma(picture(uv + 0.035 * vec2(cos(a) * RENDERSIZE.y / max(RENDERSIZE.x, 1.0), sin(a))));
  }
  vec2 c = (uv - vec2(0.5, 0.45)) * vec2(RENDERSIZE.x / max(RENDERSIZE.y, 1.0) * 0.6, 1.0);
  return vec4(mix(smoothstep(0.1, 0.8, l / 7.0), 1.0 - smoothstep(0.15, 0.7, length(c)), 0.45), 0.0, 0.0, 1.0);
}
float depthAt(vec2 uv) { return mix(IMG_NORM_PIXEL(near, uv).r, IMG_NORM_PIXEL(depth, uv).r, DEPTH_READY); }
// 1 inside the figure (nearer than `cut`), soft over `soft`.
float figureAt(vec2 uv, float cut, float soft) { return smoothstep(cut - soft, cut + soft, depthAt(uv)); }
// ── end of the shared depth block ──────────────────────────────────────

void main() {
  vec2 uv = isf_FragNormCoord;
  if (PASSINDEX == 0.0) { gl_FragColor = listen(); return; }
  if (PASSINDEX == 1.0) { gl_FragColor = nearPass(uv); return; }
  vec3 c = picture(uv);
  // The music's part, all of it through react: 0 in silence, 0 at react 0.
  float p = pulse() * react;
  float turn = mids() * react;
  // 1 in the room, 0 on the figure: the depth map, or the guess without one.
  float room = 1.0 - figureAt(uv, cut, 0.08);
  vec3 tint = hue(colour + 0.12 * turn);
  vec3 washed = mix(c, luma(c) * tint * 1.6, room * (0.6 + 0.4 * p));
  // amount 0 is the picture as it came in.
  gl_FragColor = vec4(mix(c, washed, amount), 1.0);
}
