/*{
  "DESCRIPTION": "hearts: floating hearts over the picture that pump with the bass, glow with the highs and flash on the beat; fx_* are performance effects (0 = off, held pad = on)",
  "CATEGORIES": ["tussen examples"],
  "INPUTS": [
    { "NAME": "inputImage", "TYPE": "image" },
    { "NAME": "spectrum", "TYPE": "audioFFT" },
    { "NAME": "gain", "TYPE": "float", "MIN": 1.0, "MAX": 16.0, "DEFAULT": 5.0 },
    { "NAME": "density", "TYPE": "float", "MIN": 3.0, "MAX": 14.0, "DEFAULT": 7.0 },
    { "NAME": "mixAmount", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.85 },
    { "NAME": "speed", "TYPE": "float", "MIN": 0.0, "MAX": 2.0, "DEFAULT": 0.4 },
    { "NAME": "fx_kaleido", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_ripple", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_glitch", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_zoom", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_rainbow", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_invert", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_mono", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_strobe", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_rain", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_bigheart", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_burst", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_blackout", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_mirror", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_pixel", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_hearteye", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_spin", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 }
  ]
}*/

// Heart signed distance (negative inside), centred at 0, ~1 unit tall.
float heart(vec2 p) {
  p.y = p.y + 0.5;   // ISF's y runs up: point at the bottom, lobes on top
  p.x = abs(p.x);
  if (p.y + p.x > 1.0) {
    return sqrt(dot(p - vec2(0.25, 0.75), p - vec2(0.25, 0.75))) - sqrt(2.0) / 4.0;
  }
  vec2 a = p - vec2(0.0, 1.0);
  vec2 b = p - 0.5 * max(p.x + p.y, 0.0);
  return sqrt(min(dot(a, a), dot(b, b))) * sign(p.x - p.y);
}

float hash(vec2 p) {
  return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float band(float lo, float hi) {
  float s = 0.0;
  for (int i = 0; i < 8; i++) {
    float x = mix(lo, hi, (float(i) + 0.5) / 8.0);
    s += IMG_NORM_PIXEL(spectrum, vec2(x, 0.25)).r;
  }
  return clamp(s / 8.0 * gain, 0.0, 1.5);
}

vec3 hue(float h) {
  return clamp(abs(mod(h * 6.0 + vec3(0.0, 4.0, 2.0), 6.0) - 3.0) - 1.0, 0.0, 1.0);
}

vec2 rot(vec2 p, float a) {
  float c = cos(a), s = sin(a);
  return vec2(c * p.x - s * p.y, s * p.x + c * p.y);
}

void main() {
  vec2 uv0 = isf_FragNormCoord;
  float aspect = RENDERSIZE.x / RENDERSIZE.y;

  float bass = band(0.0, 0.06);
  float mids = band(0.06, 0.3);
  float highs = band(0.3, 0.9);
  float flash = exp(-5.0 * PHASE);

  // ── where we read the picture from (space effects) ──
  vec2 uv = uv0;
  vec2 c = (uv - 0.5) * vec2(aspect, 1.0);
  // zoom: punches in on every beat
  c /= 1.0 + fx_zoom * (0.25 + 0.5 * flash + 0.3 * bass);
  // spin
  c = rot(c, fx_spin * (TIME * 1.5 + flash));
  // kaleidoscope: 6-way mirror
  if (fx_kaleido > 0.01) {
    float a = atan(c.y, c.x);
    float r = length(c);
    float seg = 6.2831853 / 6.0;
    a = abs(mod(a + TIME * 0.3, seg) - seg * 0.5);
    c = mix(c, vec2(cos(a), sin(a)) * r, fx_kaleido);
  }
  uv = c / vec2(aspect, 1.0) + 0.5;
  // mirror: left half folded onto the right
  uv.x = mix(uv.x, 0.5 - abs(uv.x - 0.5), step(0.01, fx_mirror));
  // ripple: waves out from the centre with the bass
  float rr = length((uv - 0.5) * vec2(aspect, 1.0));
  uv += fx_ripple * (0.02 + 0.04 * bass) * sin(rr * 40.0 - TIME * 8.0) * normalize(uv - 0.5 + 1e-4);
  // pixelate
  float px = mix(1.0, 48.0, fx_pixel);
  if (fx_pixel > 0.01) {
    vec2 cells = RENDERSIZE / px;
    uv = (floor(uv * cells) + 0.5) / cells;
  }
  // glitch: horizontal slices jump, RGB split
  float slice = floor(uv.y * 24.0);
  float jump = step(0.7, hash(vec2(slice, floor(TIME * 12.0)))) * (hash(vec2(slice, TIME)) - 0.5);
  uv.x += fx_glitch * jump * 0.15;
  float split = fx_glitch * (0.01 + 0.02 * highs);

  vec3 cam;
  cam.r = IMG_NORM_PIXEL(inputImage, uv + vec2(split, 0.0)).r;
  cam.g = IMG_NORM_PIXEL(inputImage, uv).g;
  cam.b = IMG_NORM_PIXEL(inputImage, uv - vec2(split, 0.0)).b;

  // mono: black and white picture (hearts stay coloured)
  cam = mix(cam, vec3(dot(cam, vec3(0.299, 0.587, 0.114))), fx_mono);

  // ── the hearts ──
  float dens = density * (1.0 + fx_rain * 1.2);
  vec2 g = vec2(uv0.x * aspect, uv0.y) * dens;
  g.y -= TIME * (speed + fx_rain * 1.5) * dens * 0.15;
  vec2 cell = floor(g);
  vec2 f = fract(g) - 0.5;
  float r = hash(cell);
  f.x += 0.18 * sin(TIME * 1.3 + r * 6.283);
  float size = 0.18 + 0.22 * r + 0.35 * bass + 0.08 * flash + fx_burst * (0.25 + 0.4 * flash);
  float d = heart(f / size + vec2(0.0, 0.1)) * size;

  vec3 pink = vec3(1.0, 0.18, 0.55);
  vec3 red = vec3(1.0, 0.05, 0.2);
  vec3 violet = vec3(0.7, 0.3, 1.0);
  vec3 col = mix(mix(pink, red, r), violet, clamp(mids, 0.0, 1.0) * step(0.6, r));
  // rainbow: every heart cycles through colours
  col = mix(col, hue(r + TIME * 0.5 + uv0.y), fx_rainbow);

  float fill = 1.0 - smoothstep(0.0, 0.01, d);
  float glow = exp(-abs(d) * (30.0 - 18.0 * clamp(highs, 0.0, 1.0))) * (0.4 + highs);

  vec3 base = mix(cam, cam * vec3(1.1, 0.8, 0.95) + pink * 0.15 * bass, mixAmount);
  vec3 outc = base + col * glow * mixAmount;
  outc = mix(outc, col * (0.8 + 0.6 * flash), fill * mixAmount);

  // big heart: one giant heart in the middle beating with the bass
  if (fx_bigheart > 0.01) {
    vec2 q = (uv0 - 0.5) * vec2(aspect, 1.0);
    float bs = 0.35 + 0.15 * bass + 0.08 * flash;
    float bd = heart(q / bs + vec2(0.0, 0.1)) * bs;
    vec3 bc = mix(red, hue(TIME * 0.2), fx_rainbow);
    float bfill = 1.0 - smoothstep(0.0, 0.006, bd);
    float bglow = exp(-abs(bd) * 12.0);
    outc = mix(outc, bc * (0.9 + 0.5 * flash), bfill * fx_bigheart * 0.85);
    outc += bc * bglow * fx_bigheart * (0.6 + highs);
  }

  // heart eye: the picture only inside a big heart, pink outside
  if (fx_hearteye > 0.01) {
    vec2 q = (uv0 - 0.5) * vec2(aspect, 1.0);
    float hs = 0.42 + 0.1 * bass;
    float hd = heart(q / hs + vec2(0.0, 0.1)) * hs;
    float inside = 1.0 - smoothstep(-0.005, 0.005, hd);
    outc = mix(outc, mix(pink * 0.25, outc, inside), fx_hearteye);
  }

  float v = length(uv0 - 0.5);
  outc += pink * smoothstep(0.35, 0.8, v) * (0.15 + 0.35 * bass) * mixAmount;

  // invert
  outc = mix(outc, 1.0 - outc, fx_invert);
  // strobe: white on the 16ths
  float s16 = step(0.5, fract(BEAT * 4.0));
  outc = mix(outc, vec3(1.0), fx_strobe * (1.0 - s16));
  // blackout
  outc = mix(outc, vec3(0.0), fx_blackout);

  gl_FragColor = vec4(clamp(outc, 0.0, 1.0), 1.0);
}
