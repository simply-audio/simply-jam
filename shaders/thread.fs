/*{
  "DESCRIPTION": "thread: the picture made of thread, finer on the figure than on the room, the darkest parts left as bare cloth. cross-stitch: an embroidery of X stitches in a limited floss palette on aida cloth · weave: a woven twill tapestry, the picture in the weft over a dark warp · knit: a jumper's V stitches in fuzzy yarn. Kicks swell the thread (cross-stitch) or make the weave slip (weave) or stretch the knit (knit); the mids sway the rows like a hand that isn't quite steady; the hats run a sheen along the thread.",
  "CREDIT": "Chris Poucet, simply-jam",
  "CATEGORIES": ["texture", "depth", "sound", "beat", "modes", "pressure"],
  "INPUTS": [
    { "NAME": "inputImage", "TYPE": "image" },
    { "NAME": "depth", "TYPE": "depth" },
    { "NAME": "wave", "TYPE": "audio" },
    { "NAME": "spectrum", "TYPE": "audioFFT" },
    { "NAME": "amount", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 1.0, "DESCRIPTION": "pressure: how far the thread takes over from the camera" },
    { "NAME": "react", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5, "DESCRIPTION": "how much the music moves it: 0 holds it still, 0.5 as tuned, 1 twice as much" },
    { "NAME": "mode", "TYPE": "long", "MIN": 0, "MAX": 2, "DEFAULT": 0, "VALUES": [0, 1, 2], "LABELS": ["cross-stitch", "weave", "knit"] },
    { "NAME": "scale", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.55, "DESCRIPTION": "how big the stitches are" },
    { "NAME": "fineFigure", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.6, "DESCRIPTION": "how much finer the stitches are on the figure (0: the same everywhere)" },
    { "NAME": "cloth", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.15, "DESCRIPTION": "the cloth showing through: 0 black, 1 cream" },
    { "NAME": "palette", "TYPE": "float", "MIN": 2.0, "MAX": 12.0, "DEFAULT": 6.0, "DESCRIPTION": "how many shades each colour channel of thread comes in" },
    { "NAME": "kickAmt", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.6, "DESCRIPTION": "kicks swell the thread, slip the weave, stretch the knit" },
    { "NAME": "midsAmt", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5, "DESCRIPTION": "mids sway the rows" },
    { "NAME": "highsAmt", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5, "DESCRIPTION": "hats run a sheen along the thread" },
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

// Each mode draws one cell of thread from a single cell-centre colour, at
// two grid sizes, the fine one blended in over the figure. Coordinates are
// in rows of the picture's height, so a stitch is the same size at any
// resolution.

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
vec3 cam(vec2 uv) { return IMG_NORM_PIXEL(inputImage, clamp(uv, 0.0, 1.0)).rgb; }
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float hash1(float x) { return fract(sin(x * 91.3458) * 47453.5453); }

// the colour behind a cell: four taps round its centre, saturated a little, in palette steps
vec3 threadColour(vec2 centre, vec2 cellUv) {
  vec3 s = 0.25 * (cam(centre + cellUv * vec2(0.25, 0.25)) + cam(centre + cellUv * vec2(-0.25, 0.25)) + cam(centre + cellUv * vec2(0.25, -0.25)) + cam(centre + cellUv * vec2(-0.25, -0.25)));
  float l = lumaOf(s);
  s = clamp(l + (s - l) * 1.25, 0.0, 1.0);
  // steps even to the eye: finer in the shadows, where a dark room lives
  float steps = max(palette - 1.0, 1.0);
  vec3 g = floor(sqrt(s) * steps + 0.5) / steps;
  return g * g;
}
vec3 clothColour(vec2 f) {
  vec3 col = mix(vec3(0.05, 0.05, 0.06), vec3(0.92, 0.88, 0.78), cloth);
  // aida: a woven grid with a hole at each stitch's corners
  float weave = 0.85 + 0.15 * sin(f.x * 6.2831 * 2.0) * sin(f.y * 6.2831 * 2.0);
  float hole = smoothstep(0.12, 0.22, min(min(f.x, 1.0 - f.x), min(f.y, 1.0 - f.y)) + 0.0);
  return col * weave * mix(0.45, 1.0, hole);
}

vec3 crossStitch(vec2 uv, float rows, float kick, float mids, float hats) {
  float aspect = RENDERSIZE.x / max(RENDERSIZE.y, 1.0);
  vec2 p = uv * vec2(aspect, 1.0) * rows;
  p.x += 0.25 * mids * sin(floor(p.y) * 1.7 + TIME * 2.0);
  vec2 id = floor(p); vec2 f = fract(p);
  vec2 cellUv = vec2(1.0 / aspect, 1.0) / rows;
  vec3 col = threadColour((id + 0.5) * cellUv, cellUv);
  vec3 bg = clothColour(f);
  float l = lumaOf(col);
  float sewn = step(0.07, l + 0.02 * hash(id));
  float w = 0.13 + 0.09 * kick;
  float d1 = abs(f.x - f.y) * 0.7071;           // the under stitch, / 
  float d2 = abs(f.x + f.y - 1.0) * 0.7071;     // the over stitch, \ 
  float t1 = 1.0 - smoothstep(w * 0.7, w, d1);
  float t2 = 1.0 - smoothstep(w * 0.7, w, d2);
  // twisted floss: stripes across each thread, a rounded profile, a sheen running along it
  float tw1 = 0.8 + 0.2 * sin((f.x + f.y) * 40.0);
  float tw2 = 0.8 + 0.2 * sin((f.x - f.y) * 40.0);
  float r1 = sqrt(max(1.0 - d1 / w, 0.0));
  float r2 = sqrt(max(1.0 - d2 / w, 0.0));
  float sheenPos = fract(TIME * 0.7 + hash(id) * 0.3);
  float sheen = hats * smoothstep(0.15, 0.0, abs(f.x - sheenPos)) * 0.5;
  vec3 c1 = col * tw1 * (0.55 + 0.45 * r1);
  vec3 c2 = col * tw2 * (0.6 + 0.5 * r2) + sheen * r2;
  vec3 outc = mix(bg, c1, t1 * sewn);
  // the over stitch casts a little shadow onto the under one
  outc *= 1.0 - 0.3 * (1.0 - smoothstep(w, w * 1.6, d2)) * (1.0 - t2) * sewn;
  return mix(outc, c2, t2 * sewn);
}

vec3 weave(vec2 uv, float rows0, float kick, float mids, float hats) {
  float aspect = RENDERSIZE.x / max(RENDERSIZE.y, 1.0);
  float rows = rows0 * 1.5;   // thread is finer than a stitch
  vec2 p = uv * vec2(aspect, 1.0) * rows;
  float row = floor(p.y);
  // the weft slips sideways on a kick, every other row the other way
  p.x += kick * 0.6 * (mod(row, 2.0) * 2.0 - 1.0) + 0.3 * mids * sin(row * 0.9 + TIME * 1.5);
  vec2 id = floor(p); vec2 f = fract(p);
  vec2 cellUv = vec2(1.0 / aspect, 1.0) / rows;
  vec3 col = threadColour((id + 0.5) * cellUv, cellUv);
  // a 2/2 twill: the weft goes over two warps and under two, stepping each row
  float ph = mod(id.x + id.y, 4.0);
  float over = step(ph, 1.5);
  float weftProfile = sin(f.y * 3.14159);
  float warpProfile = sin(f.x * 3.14159);
  float fibre = 0.9 + 0.1 * hash(floor(p * vec2(8.0, 1.0)));
  vec3 warpCol = mix(vec3(0.06, 0.055, 0.05), col, 0.6) + vec3(0.85, 0.8, 0.7) * 0.15 * cloth;
  vec3 weftCol = col * (0.45 + 0.6 * weftProfile) * fibre;
  float sheen = hats * pow(weftProfile, 6.0) * (0.5 + 0.5 * sin(p.x * 1.3 - TIME * 6.0)) * 0.35;
  weftCol += sheen;
  vec3 warpC = warpCol * (0.4 + 0.7 * warpProfile);
  // where one thread dives under the other, a shadow
  vec3 top = over > 0.5 ? weftCol * mix(0.7, 1.0, warpProfile) : warpC * mix(0.6, 1.0, weftProfile);
  // a float spans two cells: a gap only where it dives under (a weft float
  // ends left in phase 0 and right in phase 1; a warp float, below in 2, above in 3)
  float gx = over > 0.5 ? (ph < 0.5 ? smoothstep(0.0, 0.1, f.x) : smoothstep(1.0, 0.9, f.x)) : smoothstep(0.0, 0.05, f.x) * smoothstep(1.0, 0.95, f.x);
  float gy = over > 0.5 ? smoothstep(0.0, 0.06, f.y) * smoothstep(1.0, 0.94, f.y) : (ph < 2.5 ? smoothstep(0.0, 0.1, f.y) : smoothstep(1.0, 0.9, f.y));
  float gap = gx * gy;
  return top * mix(0.55, 1.0, gap);
}

vec3 knit(vec2 uv, float rows, float kick, float mids, float hats) {
  float aspect = RENDERSIZE.x / max(RENDERSIZE.y, 1.0);
  vec2 p = uv * vec2(aspect, 1.0) * rows * vec2(0.7, 0.85 / (1.0 + 0.25 * kick));   // a yarn stitch is bigger than a floss one
  float row = floor(p.y);
  p.x += 0.35 * mids * sin(row * 0.7 + TIME * 1.7);
  vec3 outc = clothColour(fract(p)) * 0.4;
  float best = 0.0;
  // each stitch is a V of two leaning loops that hang into the row below:
  // look at this row and the one above
  for (int k = 0; k < 2; k++) {
    vec2 q = p + vec2(0.0, float(k));
    vec2 id = floor(q); vec2 f = fract(q) - vec2(0.5, float(k) * 0.0);
    f.y = fract(q.y) - 0.5 + 0.35 * float(k) - 0.0;
    vec2 cellUv = vec2(1.0 / (aspect * 0.7), 1.0 / 0.85) / rows;
    vec3 col = threadColour((id + 0.5) * cellUv, cellUv);
    for (int side = 0; side < 2; side++) {
      float sgn = side == 0 ? -1.0 : 1.0;
      vec2 c = vec2(0.22 * sgn, 0.0);
      vec2 d = f - c;
      float a = sgn * 0.55;
      vec2 r = vec2(cos(a) * d.x - sin(a) * d.y, sin(a) * d.x + cos(a) * d.y);
      float e = length(r / vec2(0.2, 0.52));
      float leg = 1.0 - smoothstep(0.8, 1.0, e);
      if (leg > best * 0.999) {
        float fuzz = 0.85 + 0.15 * hash(floor((uv * RENDERSIZE) / 1.5));
        float ply = 0.75 + 0.25 * sin(r.y * 40.0 + r.x * 12.0);
        float shade = 0.6 + 0.6 * sqrt(max(1.0 - e, 0.0));
        vec3 yarn = col * shade * ply * fuzz + hats * 0.25 * pow(max(1.0 - e, 0.0), 2.0) * fuzz;
        outc = mix(outc, yarn, leg);
        best = max(best, leg);
      }
    }
  }
  return outc;
}

vec3 look(vec2 uv, float m, float rows, float kick, float mids, float hats) {
  if (m < 0.5) { return crossStitch(uv, rows, kick, mids, hats); }
  if (m < 1.5) { return weave(uv, rows, kick, mids, hats); }
  return knit(uv, rows, kick, mids, hats);
}

void main() {
  vec2 uv = isf_FragNormCoord;
  if (PASSINDEX == 0.0) { gl_FragColor = sigPass(); return; }
  if (PASSINDEX == 1.0) { gl_FragColor = depthTimePass(uv); return; }

  float kick = kickNow() * kickAmt;
  float mids = midsNow() * midsAmt;
  float hats = highsNow() * highsAmt;
  float m = floor(mode + 0.5);
  vec3 c = cam(uv);
  float fig = figureAt(uv) * step(0.01, fineFigure);
  float rows = mix(110.0, 30.0, scale);
  vec3 coarse = look(uv, m, rows, kick, mids, hats);
  vec3 outc = coarse;
  if (fig > 0.001) {
    vec3 fine = look(uv, m, rows * (1.0 + 1.5 * fineFigure), kick, mids, hats);
    outc = mix(coarse, fine, fig);
  }
  gl_FragColor = vec4(clamp(mix(c, outc, amount), 0.0, 1.0), 1.0);
}
