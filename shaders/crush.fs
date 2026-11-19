/*{
  "DESCRIPTION": "crush: the picture broken by the sound. jpeg crush: a real JPEG squeeze (8×8 blocks through a DCT, quantised and back), harder on every kick, so the picture collapses into ringing blocks and smeared colour and recovers between beats; the mids squeeze the colour harder than the detail, the hats knock blocks down to flat squares, kicks drop wrong-coloured blocks in. The figure can stay cleaner than the room (figureClear) · wave tear: the picture torn sideways in bands by the actual shape of the sound wave, red, green and blue each torn by a moment apart, so the colour fringes follow the music; kicks tear wider, the mids stretch the wave across the picture, the hats pull the colours apart.",
  "CREDIT": "Chris Poucet, simply-jam",
  "CATEGORIES": ["glitch", "depth", "sound", "beat", "modes", "pressure"],
  "INPUTS": [
    { "NAME": "inputImage", "TYPE": "image" },
    { "NAME": "depth", "TYPE": "depth" },
    { "NAME": "wave", "TYPE": "audio" },
    { "NAME": "spectrum", "TYPE": "audioFFT" },
    { "NAME": "amount", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 1.0, "DESCRIPTION": "how strong the crush is: 1 as crushed (jpeg) or torn (wave) between kicks as tuned, kicks push it harder; 0 the picture untouched" },
    { "NAME": "react", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5, "DESCRIPTION": "how much the music moves it: 0 holds it still, 0.5 as tuned, 1 twice as much" },
    { "NAME": "mode", "TYPE": "long", "MIN": 0, "MAX": 1, "DEFAULT": 0, "VALUES": [0, 1], "LABELS": ["jpeg crush", "wave tear"] },
    { "NAME": "cell", "TYPE": "float", "MIN": 1.0, "MAX": 4.0, "DEFAULT": 2.0, "DESCRIPTION": "jpeg: pixels per block pixel (blocks are 8 of them: 2 makes 16-pixel blocks) · wave: band height in pixels ×2" },
    { "NAME": "figureClear", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5, "DESCRIPTION": "how much cleaner the figure stays than the room" },
    { "NAME": "kickAmt", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.7, "DESCRIPTION": "kicks crush harder, drop in wrong-coloured blocks, tear wider" },
    { "NAME": "midsAmt", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5, "DESCRIPTION": "mids squeeze the colour (jpeg) or stretch the wave over the picture (tear)" },
    { "NAME": "highsAmt", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5, "DESCRIPTION": "hats flatten blocks (jpeg) or pull the colours apart (tear)" },
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
    { "TARGET": "rowsIn", "FLOAT": true },
    { "TARGET": "coeffs", "FLOAT": true },
    { "TARGET": "colsOut", "FLOAT": true },
    {}
  ]
}*/

// jpeg crush is the transform done separably, 8 taps a pass: rows forward
// (rowsIn), columns forward and quantised (coeffs), columns back (colsOut),
// rows back and to RGB (the last pass). Every pixel of a block pixel (cell)
// computes the same value, so the buffers can be read with plain linear
// sampling at block-pixel centres. Colour is YCbCr, as in a JPEG.

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
// amount 1 is the look as tuned: it rests at REST and the music pushes it
// from there. Lower, it rests lower (near 0 only the music brings it in),
// and below 0.1 it fades out to the picture as it came in at 0.
const float REST = 0.35;
float press() { return REST * amount; }
float wet() { return clamp(amount * 10.0, 0.0, 1.0); }
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

vec3 toYcc(vec3 c) {
  return vec3(dot(c, vec3(0.299, 0.587, 0.114)) - 0.5, dot(c, vec3(-0.168736, -0.331264, 0.5)), dot(c, vec3(0.5, -0.418688, -0.081312)));
}
vec3 toRgb(vec3 y) {
  float l = y.x + 0.5;
  return vec3(l + 1.402 * y.z, l - 0.344136 * y.y - 0.714136 * y.z, l + 1.772 * y.y);
}
float cellPx() { return floor(clamp(cell, 1.0, 4.0) + 0.5); }
// this pixel's block pixel (integer), and where block pixel q is, in uv
vec2 blockPixel() { return floor(isf_FragNormCoord * RENDERSIZE / cellPx()); }
vec2 uvOf(vec2 q) { return clamp((q + 0.5) * cellPx() / RENDERSIZE, 0.0, 1.0); }
float basis(float k, float u) { return cos((2.0 * k + 1.0) * u * 3.14159265 / 16.0) * (u < 0.5 ? 0.35355339 : 0.5); }

// the camera averaged over one block pixel
vec3 camCell(vec2 q) {
  float n = cellPx();
  if (n < 1.5) { return cam(uvOf(q)); }
  vec2 h = 0.25 * n / RENDERSIZE;
  vec2 c = uvOf(q);
  return 0.25 * (cam(c + vec2(h.x, h.y)) + cam(c - vec2(h.x, h.y)) + cam(c + vec2(h.x, -h.y)) + cam(c + vec2(-h.x, h.y)));
}

vec4 rowsForward() {
  vec2 q = blockPixel();
  float u = mod(q.x, 8.0);
  float x0 = q.x - u;
  vec3 s = vec3(0.0);
  for (int k = 0; k < 8; k++) { s += toYcc(camCell(vec2(x0 + float(k), q.y))) * basis(float(k), u); }
  return vec4(s, 1.0);
}

vec4 colsForward(float kick, float mids, float hats) {
  vec2 q = blockPixel();
  float v = mod(q.y, 8.0);
  float y0 = q.y - v;
  float u = mod(q.x, 8.0);
  vec3 s = vec3(0.0);
  for (int k = 0; k < 8; k++) { s += IMG_NORM_PIXEL(rowsIn, uvOf(vec2(q.x, y0 + float(k)))).rgb * basis(float(k), v); }
  // quantise: coarser for higher frequencies, coarser still for colour
  vec2 blk = floor(q / 8.0);
  vec2 centre = uvOf(blk * 8.0 + 4.0);
  float clean = 1.0 - 0.85 * figureClear * figureAt(centre);
  float crush = clamp(press() + 0.9 * kick, 0.0, 1.5) * clean;
  float base = mix(0.008, 1.1, crush * crush);
  float steep = 1.0 + (u + v) * (0.4 + 0.8 * crush);
  float qy = base * steep;
  float qc = base * steep * (1.6 + 5.0 * mids);
  // the hats knock a few blocks down to their flat average (DC only)
  float flatten = step(hash(blk + floor(TIME * 12.0)), 0.12 * hats) * step(0.5, u + v);
  if (u + v < 0.5) {
    // the block's average: kept finer, so the picture's colour survives
    float qd = max(base * 0.5, 0.01);
    s.x = floor(s.x / qd + 0.5) * qd;
    s.yz = floor(s.yz / (qd * 0.6) + 0.5) * qd * 0.6;
  } else {
    s.x = (1.0 - flatten) * floor(s.x / qy + 0.5) * qy;
    s.yz = (1.0 - flatten) * floor(s.yz / qc + 0.5) * qc;
  }
  // a kick drops a few blocks in the wrong colour, as a broken file does
  float seed = floor(TIME - kickAge());
  float wrong = step(hash(blk * 1.37 + seed * 7.1), 0.015 * kickAmt) * step(kickAge(), 0.12) * step(u + v, 0.5) * min(reactK(), 1.0);
  s.yz += wrong * (vec2(hash(blk + seed), hash(blk - seed)) - 0.5) * 2.4;
  return vec4(s, 1.0);
}

vec4 colsBack() {
  vec2 q = blockPixel();
  float y = mod(q.y, 8.0);
  float y0 = q.y - y;
  vec3 s = vec3(0.0);
  for (int v = 0; v < 8; v++) { s += IMG_NORM_PIXEL(coeffs, uvOf(vec2(q.x, y0 + float(v)))).rgb * basis(y, float(v)); }
  return vec4(s, 1.0);
}

vec3 rowsBack() {
  vec2 q = blockPixel();
  float x = mod(q.x, 8.0);
  float x0 = q.x - x;
  vec3 s = vec3(0.0);
  for (int u = 0; u < 8; u++) { s += IMG_NORM_PIXEL(colsOut, uvOf(vec2(x0 + float(u), q.y))).rgb * basis(x, float(u)); }
  return toRgb(s);
}

// the waveform at t (0..1 along its 512 samples), -1..1
float waveAt(float t) { return IMG_NORM_PIXEL(wave, vec2(fract(t), 0.25)).r * 2.0 - 1.0; }

vec3 waveTear(vec2 uv, float kick, float mids, float hats) {
  float band = 2.0 * cellPx();
  float row = floor(uv.y * RENDERSIZE.y / band);
  float rows = RENDERSIZE.y / band;
  float span = mix(0.35, 1.0, mids);             // how much of the wave spans the picture
  float t = row / rows * span;
  float clean = 1.0 - 0.85 * figureClear * figureAt(uv);
  float tear = (0.14 * press() + 0.12 * kick) * clean;
  float apart = (0.004 + 0.04 * hats) * span;
  float r = cam(vec2(uv.x + waveAt(t) * tear, uv.y)).r;
  float g = cam(vec2(uv.x + waveAt(t + apart) * tear, uv.y)).g;
  float b = cam(vec2(uv.x + waveAt(t + 2.0 * apart) * tear, uv.y)).b;
  return vec3(r, g, b);
}

void main() {
  vec2 uv = isf_FragNormCoord;
  if (PASSINDEX == 0.0) { gl_FragColor = sigPass(); return; }
  if (PASSINDEX == 1.0) { gl_FragColor = depthTimePass(uv); return; }
  float m = floor(mode + 0.5);
  float kick = kickNow() * kickAmt;
  float mids = midsNow() * midsAmt;
  float hats = highsNow() * highsAmt;
  if (PASSINDEX < 4.5) {
    if (m > 0.5) { gl_FragColor = vec4(0.0); return; }      // wave tear needs no transform
    if (PASSINDEX == 2.0) { gl_FragColor = rowsForward(); return; }
    if (PASSINDEX == 3.0) { gl_FragColor = colsForward(kick, mids, hats); return; }
    gl_FragColor = colsBack(); return;
  }
  vec3 outc = m < 0.5 ? rowsBack() : waveTear(uv, kick, mids, hats);
  // nothing pressed and no kick, or amount 0: the picture as it came
  outc = mix(cam(uv), outc, wet() * clamp((press() + kick) * 10.0, 0.0, 1.0));
  gl_FragColor = vec4(clamp(outc, 0.0, 1.0), 1.0);
}
