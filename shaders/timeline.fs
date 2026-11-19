/*{
  "DESCRIPTION": "audiodev timeline: the camera's own past from an atlas ring (16 quarter-size slots over `reach` seconds), as delays, scrubs, slit-scans, per-pixel time maps, ghosts and echoes of the figure, and the figure and the room on different clocks.",
  "CREDIT": "simply-creative FX library (Chris), ported to tussen ISF",
  "CATEGORIES": ["time", "history", "depth", "sound", "beat", "modes", "pressure"],
  "INPUTS": [
    { "NAME": "inputImage", "TYPE": "image" },
    { "NAME": "depth", "TYPE": "depth" },
    { "NAME": "wave", "TYPE": "audio" },
    { "NAME": "spectrum", "TYPE": "audioFFT" },
    { "NAME": "amount", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 1.0, "DESCRIPTION": "pressure: how far back / how much past, per mode" },
    { "NAME": "react", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5, "DESCRIPTION": "how much the music moves it: 0 holds it still, 0.5 as tuned, 1 twice as much" },
    { "NAME": "mode", "TYPE": "long", "MIN": 0, "MAX": 24, "DEFAULT": 0,
      "VALUES": [0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24],
      "LABELS": ["delay","scratch","reverse","rewind","slow-mo","beat loop","slit","jelly","luma time","depth time","depth bands","time patches","interlace","time RGB","ghosts","beat echoes","zoom ghosts","silhouette stack","outline rings","room echoes","kick throw-back","now/past gate","two clocks","where you were","silhouette swap"] },
    { "NAME": "where", "TYPE": "long", "MIN": 0, "MAX": 2, "DEFAULT": 0, "DESCRIPTION": "what travels in time: 0 the whole frame · 1 the figure (over the live room) · 2 the room (under the live figure)" },
    { "NAME": "reach", "TYPE": "float", "MIN": 0.5, "MAX": 3.0, "DEFAULT": 1.5, "DESCRIPTION": "seconds of history the 16 slots hold" },
    { "NAME": "slit", "TYPE": "long", "MIN": 0, "MAX": 4, "DEFAULT": 0, "DESCRIPTION": "slit shape (mode 6): 0 rows top-now · 1 rows feet-now · 2 columns centre-now · 3 rings · 4 split" },
    { "NAME": "heads", "TYPE": "float", "MIN": 1.0, "MAX": 4.0, "DEFAULT": 3.0, "DESCRIPTION": "past figures (mode 14)" },
    { "NAME": "kickAmt", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.6 },
    { "NAME": "midsAmt", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.3 },
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
    { "TARGET": "ring", "WIDTH": "$WIDTH", "HEIGHT": "$HEIGHT * 5 / 4", "FLOAT": true, "PERSISTENT": true },
    {}
  ]
}*/

// timeline — the history family. One ring holds the past; each mode reads it.
//
// heads (one read head, `where` picks what travels: 0 frame · 1 figure · 2 room)
// mode 0  · delay           FX 4 Time travel (where=2), 155 Past self (where=1), 260 Pressure-time figure (where=1),
//                           242 Figure as mask for time (where=2), 301 Past room, pushed figure (where=2), 105 Piano scrub (where=0)
// mode 1  · scratch         FX 159 Scratch
// mode 2  · reverse         FX 160 Reverse, 273 Room reverse loop (where=2: held = loops backwards)
// mode 3  · rewind          FX 175 Rewind
// mode 4  · slow-mo         FX 187 Slow-mo figure (where=1)
// mode 5  · beat loop       FX 205 Room loop (where=2)
// per-pixel time maps
// mode 6  · slit            FX 17 Slit-scan (slit=0), 204 Height-delay figure (slit=1 where=1),
//                           172 Time slit x (slit=2), 164 Radial time (slit=3), 291 Two-speed room (slit=4 where=2)
// mode 7  · jelly           FX 60 Jelly
// mode 8  · luma time       FX 162 Luma time
// mode 9  · depth time      FX 18 Depth-time (where=1)
// mode 10 · depth bands     FX 163 Depth bands time
// mode 11 · time patches    FX 165 Time patches
// mode 12 · interlace       FX 59 Interlace time
// mode 13 · time RGB        FX 152 Time RGB (where=1)
// the figure's past selves (live figure always on top)
// mode 14 · ghosts          FX 151 Now + past (heads=1), 156 Ghost trio (heads=3), 154 Mids reach (heads=3), 166 Key heads (heads=4)
// mode 15 · beat echoes     FX 158 Beat echoes
// mode 16 · zoom ghosts     FX 170 Zoom ghosts
// mode 17 · stack           FX 129 Time smear figure, 130 Strobe silhouettes
// mode 18 · outline rings   FX 219 Echo outline layers, 238 Delay room echo-fg (where=2)
// mode 19 · room echoes     FX 184 Key-count room echoes
// mode 20 · kick throw-back FX 153
// mode 21 · now/past gate   FX 157
// two clocks / masks in time
// mode 22 · two clocks      FX 167
// mode 23 · where you were  FX 169
// mode 24 · silhouette swap FX 279
//
// History is an atlas ring: `ring` is the frame size plus a quarter: rows 0..3 are 16 quarter-size
// slots written round robin, one per reach/15 s; row 4 holds the room plate (tile 0) and the
// state pixels (top row, right end). Reading interpolates between slots, so δt glides.

// ════ shared: signals (copy verbatim) ══════════════════════════════════
// sig is 2×1 and kept from frame to frame:
//   pixel 0: r kick 0..1 (1 on an onset, decays) · g bass envelope · b fract(TIME) · a s since the onset
// (FLOAT buffers are half floats: never store absolute TIME, only fract(TIME) and ages)
//   pixel 1: r lows 0..1 · g mids 0..1 · b highs 0..1 · a mids slow average
// The kick is Chris's waveform-domain detector: 150 Hz low-pass RMS over
// the 512-sample program, an onset when it clears 1.35× its own envelope.
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
float lowsNow() { return reactK() * sigB().r; }
float midsNow() { return reactK() * sigB().g; }
float highsNow() { return reactK() * sigB().b; }

// ════ shared: depth (copy verbatim; depth shaders only) ════════════════
// depthBuf.r: 1 = nearest, toward 0 = far. The ML map when tussen has it
// (the ML map, DEPTH_READY), else blurred brightness (bright = near)
// with a little centre weighting (the speaker stands in the middle).
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
// 1 where the effect applies (`where`: everywhere, the figure, the room)
float regionAt(vec2 uv) {
  float w = floor(where + 0.5);
  float f = figureAt(uv);
  return w < 0.5 ? 1.0 : (w < 1.5 ? f : 1.0 - f);
}
// ═══════════════════════════════════════════════════════════════════════

// ════ history: the atlas ring ══════════════════════════════════════════
vec3 camAt(vec2 uv) { return IMG_NORM_PIXEL(inputImage, clamp(uv, 0.0, 1.0)).rgb; }
float pfig(float d) { return smoothstep(figure - soft, figure + soft, d); }
float slotLen() { return max(reach, 0.25) / 15.0; }
float slotNow() { return mod(floor(TIME / slotLen()), 16.0); }
float histMax() { return 13.9 * slotLen(); }        // always inside the ring
vec2 ringTile(vec2 cell, vec2 uv) {
  vec2 h = 0.75 / (IMG_SIZE(ring) / vec2(4.0, 5.0)); // keep the bilinear tap inside its tile
  return (cell + clamp(uv, h, 1.0 - h)) * vec2(0.25, 0.2);
}
vec4 tileAt(float k, vec2 uv) {
  float idx = mod(slotNow() - k + 16.0, 16.0);
  return IMG_NORM_PIXEL(ring, ringTile(vec2(mod(idx, 4.0), floor(idx / 4.0)), uv));
}
// the camera `age` seconds ago: rgb + its depth (1 = near) in a. Quarter size, interpolated between slots.
vec4 pastAt(vec2 uv, float age) {
  float L = slotLen(); float fl = fract(TIME / L) * L;
  age = clamp(age, 0.0, histMax());
  if (age <= fl) {
    vec4 now = vec4(camAt(uv), depthAt(uv));
    return mix(now, tileAt(1.0, uv), age / max(fl, 1e-4));
  }
  float k = 1.0 + (age - fl) / L;
  float k0 = floor(k);
  return mix(tileAt(k0, uv), tileAt(k0 + 1.0, uv), k - k0);
}
// one tap: the nearest slot (stacks, masks, rims)
vec4 pastNear(vec2 uv, float age) {
  float L = slotLen(); float fl = fract(TIME / L) * L;
  age = clamp(age, 0.0, histMax());
  if (age < 0.5 * fl) return vec4(camAt(uv), depthAt(uv));
  return tileAt(clamp(floor(1.5 + (age - fl) / L), 1.0, 15.0), uv);
}
vec3 plateAt(vec2 uv) { return IMG_NORM_PIXEL(ring, ringTile(vec2(0.0, 4.0), uv)).rgb; }
vec4 stRead(float i) { vec2 rs = IMG_SIZE(ring); return IMG_NORM_PIXEL(ring, vec2(rs.x - 0.5 - i, rs.y - 0.5) / rs); }

// state pixels (half floats: no absolute TIME stored, only TIME mod 1 and counters)
//   0: eng (glided pressure+kick+mids) · prev kick · TIME mod 1 · s since the last kick
//   1: kick parity (157 flip) · its 60 ms slide · live column x (172) · its throw
//   2: reverse head (160) s · look · burst left s
//   3: rewind head (175) s · speed · spin-up ramp · held
//   4: slow-mo head (187) s · slowness
//   5: patch seed (165) · ring drift (164)
vec4 statePass(float i) {
  vec4 s0 = stRead(0.0);
  vec4 s = stRead(i);
  float first = step(FRAMEINDEX, 1.5);
  float dt = clamp(fract(mod(TIME, 1.0) - s0.z), 1.0 / 240.0, 0.1);
  float k = kickNow(); float m = midsNow();
  float hit = step(0.95, k) * step(s0.y, 0.95);
  float MAXB = histMax();
  if (i < 0.5) {
    float target = clamp(amount + kickAmt * k + midsAmt * m, 0.0, 1.0);
    float e = s.x + (1.0 - exp(-dt / (target > s.x ? 0.05 : 0.45))) * (target - s.x);
    float since = hit > 0.5 ? 0.0 : min(s.w + dt, 30.0);
    if (first > 0.5) { e = target; since = 30.0; }
    return vec4(e, k, mod(TIME, 1.0), since);
  }
  if (i < 1.5) {
    if (first > 0.5) s = vec4(0.0, 0.0, 0.5, 0.0);
    if (hit > 0.5) { s.x = 1.0 - s.x; s.w = (s.x * 2.0 - 1.0) * 0.3; }   // SWEEP 0.3
    s.y += (1.0 - exp(-dt / 0.06)) * (s.x - s.y);
    s.w *= exp(-dt / 1.5);                                              // HOME_TAU
    s.z += (1.0 - exp(-dt / 0.18)) * (0.5 + s.w - s.z);                 // SWEEP_TAU
    return s;
  }
  if (i < 2.5) {   // FX 160: held past 0.35 reverses at 1×, a kick re-triggers a 0.4 s burst from now
    if (first > 0.5) s = vec4(0.0);
    s.z = max(s.z - dt, 0.0);
    if (hit > 0.5) { s.z = 0.4; s.x = 0.0; }
    float on = max(step(0.35, amount), step(0.001, s.z));
    if (on > 0.5) { s.x += dt * (2.0 + 1.5 * m); if (s.x > MAXB) s.x = 0.0; }
    else s.x = max(0.0, s.x - dt * 6.0);
    s.y += (1.0 - exp(-dt / 0.06)) * (on - s.y);
    return s;
  }
  if (i < 3.5) {   // FX 175: pressure rewinds (up to 2.5× backwards), release snaps home with a spin-up
    if (first > 0.5) s = vec4(0.0);
    float kenv = hit > 0.5 ? 1.0 : exp(-s0.w / 0.12);
    float p = clamp(amount + 0.9 * kickAmt / 0.6 * kenv, 0.0, 1.0);
    float held = s.w > 0.5 ? step(0.06, p) : step(0.12, p);
    if (held > 0.5) {
      s.y += (1.0 - exp(-dt / 0.3)) * (p * (1.0 + 0.4 * m) - s.y);
      s.x = min(MAXB, s.x + dt * (1.0 + 2.5 * s.y));
    } else {
      if (s.x > 0.5 / 60.0) s.z = max(s.z, clamp(s.x * 2.0, 0.35, 1.0));
      s.x = 0.0; s.y = 0.0;
      s.z *= exp(-dt / 0.35);
    }
    s.w = held;
    return s;
  }
  if (i < 4.5) {   // FX 187: slowness plays at down to ¼ speed, release catches up at 2×
    if (first > 0.5) s = vec4(0.0);
    float sl = clamp(amount + kickAmt * k + midsAmt * m, 0.0, 1.0);
    float pull = (1.0 - sl) * min(s.x / 0.1, 1.0);
    s.x = clamp(s.x + dt * (0.75 * sl - pull), 0.0, MAXB);
    s.y = sl;
    return s;
  }
  if (first > 0.5) s = vec4(0.0);
  s.x = mod(s.x + hit, 16.0);
  s.y = fract(s.y + dt * 0.6 * m);
  return s;
}

vec4 ringPass(vec2 uv) {
  vec2 rs = RENDERSIZE;
  vec2 p = uv * rs;
  if (p.y > rs.y - 1.0 && p.x > rs.x - 8.0) return statePass(floor(rs.x - p.x));
  vec2 ts = rs / vec2(4.0, 5.0);
  vec2 cell = min(floor(p / ts), vec2(3.0, 4.0));
  vec2 l = (p - cell * ts) / ts;
  vec4 prev = IMG_NORM_PIXEL(ring, uv);
  float first = step(FRAMEINDEX, 1.5);
  float idx = cell.y * 4.0 + cell.x;
  if (first < 0.5 && (cell.y > 3.5 ? cell.x > 0.5 : abs(idx - slotNow()) > 0.5)) return prev;
  vec2 o = 1.0 / IMG_SIZE(inputImage);
  vec3 c = 0.25 * (camAt(l + o * vec2(-1.0, -1.0)) + camAt(l + o * vec2(1.0, -1.0))
                 + camAt(l + o * vec2(-1.0, 1.0)) + camAt(l + o * vec2(1.0, 1.0)));
  if (cell.y > 3.5) {
    if (cell.x > 0.5) return prev;
    // the room plate: takes the camera in slowly wherever it is not the figure (FX 214's trick)
    return vec4(mix(prev.rgb, c, max(first, 0.03 * (1.0 - figureAt(l)))), 1.0);
  }
  return vec4(c, depthAt(l));
}
// ═══════════════════════════════════════════════════════════════════════

vec3 hueTri(float h) { return clamp(abs(fract(h + vec3(0.0, 2.0, 1.0) / 3.0) * 6.0 - 3.0) - 1.0, 0.0, 1.0); }
vec3 hueCos(float h) { return 0.5 + 0.5 * cos(6.2831853 * (h + vec3(0.0, 0.33, 0.67))); }
float h21(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float h11(float x) { return fract(sin(x * 91.7) * 43758.5453); }
float vnoise(vec2 p) {
  vec2 i = floor(p); vec2 f = fract(p);
  f = f * f * (3.0 - 2.0 * f);
  return mix(mix(h21(i), h21(i + vec2(1.0, 0.0)), f.x), mix(h21(i + vec2(0.0, 1.0)), h21(i + vec2(1.0, 1.0)), f.x), f.y);
}
float pfield(vec2 p, float seed) {
  vec2 o = vec2(h21(vec2(seed, 1.7)), h21(vec2(seed, 9.2))) * 97.0;
  return clamp(0.65 * vnoise(p + o) + 0.35 * vnoise(p * 2.1 - o.yx), 0.0, 0.999);
}
float spb() { return 60.0 / (TEMPO > 1.0 ? TEMPO : 120.0); }   // seconds per beat, 120 bpm fallback
// how the past frame P lands, by `where`: whole · past figure over the live room · live figure over the past room
vec3 composeW(vec2 uv, vec3 pastC, float pastD, vec3 cam, vec3 roomNow, float figNow, float W) {
  if (W < 0.5) return pastC;
  if (W < 1.5) return mix(roomNow, pastC, pfig(pastD));
  return mix(mix(pastC, plateAt(uv), pfig(pastD)), cam, figNow);   // the past room without its past figure
}
// 4-neighbour outline of the past figure at `age`
float pastEdge(vec2 uv, float age, float px) {
  vec2 t = px / RENDERSIZE;
  float c = pfig(pastNear(uv, age).a);
  float s = pfig(pastNear(uv + vec2(t.x, 0.0), age).a) + pfig(pastNear(uv - vec2(t.x, 0.0), age).a)
          + pfig(pastNear(uv + vec2(0.0, t.y), age).a) + pfig(pastNear(uv - vec2(0.0, t.y), age).a);
  return clamp(abs(4.0 * c - s) * 0.5, 0.0, 1.0);
}
// another picture in place of the second (NDI) input: the room without you, graded cool
vec3 otherPic(vec3 room) { float l = lumaOf(room); return mix(vec3(0.03, 0.05, 0.16), vec3(0.55, 0.85, 1.0), l * 1.2); }

void main() {
  vec2 uv = isf_FragNormCoord;
  if (PASSINDEX == 0.0) { gl_FragColor = sigPass(); return; }
  if (PASSINDEX == 1.0) { gl_FragColor = depthPass(uv); return; }
  if (PASSINDEX == 2.0) { gl_FragColor = ringPass(uv); return; }

  float md = floor(mode + 0.5);
  float W = floor(where + 0.5);
  vec3 cam = camAt(uv);
  float figNow = figureAt(uv);
  vec3 roomNow = mix(cam, plateAt(uv), figNow);       // the live room, the figure taken out
  float k = kickNow();
  float m = midsNow();
  vec4 s0 = stRead(0.0);
  float eng = s0.x;
  float since = s0.w;
  float R = histMax();
  float aspect = RENDERSIZE.x / max(RENDERSIZE.y, 1.0);
  vec3 col = cam;

  if (md < 0.5) {
    // ── delay (4, 155, 260, 242, 301, 105) ──────────────────────────
    const vec3  TINT = vec3(1.08, 0.94, 0.78);   // FX 4 sepia for the past room
    const float PAST_TINT = 0.35;
    const float KICK_FLASH = 0.25;
    const float LIVE_GHOST = 0.22;               // FX 155 cold live ghost while the past self shows
    const vec3  GHOST_TINT = vec3(0.55, 0.8, 1.2);
    const float RIM = 0.9;                       // FX 155 amber outline of the past self
    const float SEAM = 0.8;                      // FX 242 seam between now and then
    const float PUSH = 0.35;                     // FX 301 the live figure pushed forward along its motion
    float back = R * eng;
    float e = smoothstep(0.008, 0.13, back);
    vec4 P = pastAt(uv, back);
    if (W > 0.5 && W < 1.5) {
      col = roomNow;
      col = mix(col, cam * GHOST_TINT, figNow * LIVE_GHOST * e);
      col = mix(col, cam, figNow * (1.0 - e));
      float pf = pfig(P.a);
      vec3 pc = mix(P.rgb, vec3(lumaOf(P.rgb)) * vec3(1.1, 0.95, 0.75), PAST_TINT * e) * (1.0 + 0.35 * k * e);
      col = mix(col, pc, pf * e);
      col = mix(col, vec3(1.0, 0.7, 0.3), pastEdge(uv, back, 3.0 * (1.0 + 0.6 * m)) * pf * RIM * e);
    } else {
      vec3 room = W > 1.5 ? mix(P.rgb, plateAt(uv), pfig(P.a)) : P.rgb;
      vec3 aged = mix(room, vec3(lumaOf(room)) * TINT, 0.6);
      room = mix(room, aged, clamp((PAST_TINT + 0.5 * m) * e, 0.0, 1.0)) * (1.0 + KICK_FLASH * k);
      col = room;
      if (W > 1.5) {
        vec3 push = cam + PUSH * e * (cam - pastNear(uv, 0.1).rgb);
        float ring6 = 0.0;
        for (int i = 0; i < 6; i++) {
          float a = float(i) * 1.0471976;
          ring6 += figureAt(uv + vec2(cos(a) / aspect, sin(a)) * 0.006);
        }
        float seam = clamp((figNow - ring6 / 6.0) * 6.0, 0.0, 1.0);   // the inner edge only (soft masks stay dark)
        col = mix(room, clamp(push, 0.0, 1.5), figNow) + vec3(seam * SEAM * e);
      }
    }
  } else if (md < 1.5) {
    // ── scratch (159): strokes on the beat, pressure = depth ────────
    const float SHAPE = 0.8;
    const float BLUR = 0.05;
    const float DIR_TINT = 0.25;
    const float SHEAR = 0.006;
    float amtS = clamp(amount + kickAmt * exp(-since / 0.5) + 0.5 * midsAmt * m, 0.0, 1.0);
    float b0 = amtS * R * pow(sin(3.14159265 * fract(BEAT)), SHAPE);
    float b1 = amtS * R * pow(sin(3.14159265 * fract(BEAT + 0.02)), SHAPE);
    float vel = (b1 - b0) / (0.02 * spb());              // s of history per s, + = pulling back
    float sp = clamp(abs(vel) / 2.0, 0.0, 1.0);
    vec2 suv = uv + vec2(SHEAR * sp * sin(uv.y * 90.0 + TIME * 7.0), 0.0);
    vec3 c = vec3(0.0); float ws = 0.0;
    for (int i = 0; i < 5; i++) {
      float t = float(i) / 4.0 - 0.5;
      float w = 1.0 - abs(t);
      c += w * pastNear(suv, b0 + t * vel * BLUR).rgb;
      ws += w;
    }
    c /= ws;
    c = mix(c, c * (vel > 0.0 ? vec3(0.8, 0.95, 1.2) : vec3(1.2, 1.0, 0.8)), DIR_TINT * sp);
    c *= 1.0 + 0.2 * k * (1.0 - clamp(b0 / 0.066, 0.0, 1.0));
    col = composeW(uv, c, pastNear(uv, b0).a, cam, roomNow, figNow, W);
  } else if (md < 2.5) {
    // ── reverse (160, 273) ─────────────────────────────────────────
    vec4 st = stRead(2.0);
    float back = st.x; float look = st.y;
    float y = fract(uv.y + TIME * 0.9);
    float band = smoothstep(0.0, 0.04, y) * (1.0 - smoothstep(0.04, 0.12, y)) * look;
    vec2 ruv = uv + vec2(0.012 * (1.0 + m) * band * sin(uv.y * 300.0 + TIME * 40.0), 0.0);
    vec4 P = pastAt(ruv, back);
    vec3 c = mix(P.rgb, vec3(lumaOf(P.rgb)), 0.35 * look) * mix(vec3(1.0), vec3(0.85, 0.95, 1.15), look);
    c += 0.6 * band * (0.25 + 0.8 * k) * vec3(0.8, 0.9, 1.0);
    col = composeW(uv, c, P.a, cam, roomNow, figNow, W);
  } else if (md < 3.5) {
    // ── rewind (175) ───────────────────────────────────────────────
    vec4 st = stRead(3.0);
    float back = st.x; float spd = st.y; float ramp = st.z;
    float row = floor(uv.y * 90.0 + TIME * 60.0);
    float lineN = step(1.0 - 0.08 * spd * (1.0 + 1.2 * m), h11(row + floor(TIME * 30.0)));
    vec2 ruv = uv + vec2(0.03 * spd * (uv.y - 0.5) + 0.02 * lineN * spd, 0.0);
    float r2 = ramp * ramp;
    vec2 suv = vec2(uv.x + 0.02 * ramp * sin(uv.y * 18.0 + TIME * 25.0), fract(uv.y + 0.35 * r2));
    vec4 P = back < 0.5 / 60.0 ? vec4(camAt(suv), depthAt(suv)) : pastAt(fract(ruv), back);
    vec3 c = mix(P.rgb, vec3(lumaOf(P.rgb)), 0.3 * spd);
    c += 0.5 * lineN * spd * vec3(0.9, 0.95, 1.0);
    c *= 1.0 - 0.55 * ramp;
    col = composeW(uv, c, P.a, cam, roomNow, figNow, W);
  } else if (md < 4.5) {
    // ── slow-mo (187) ──────────────────────────────────────────────
    float back = stRead(4.0).x;
    vec4 P = pastAt(uv, back);
    float behind = back / max(R, 0.01);
    vec3 c = mix(P.rgb, P.rgb * vec3(1.12, 1.0, 0.85), 0.18 * smoothstep(0.0, 0.5, behind));
    col = composeW(uv, c, P.a, cam, roomNow, figNow, W);
  } else if (md < 5.5) {
    // ── beat loop (205): ping-pong through the last 2 beats ─────────
    float amtL = clamp(amount + kickAmt * k + midsAmt * m, 0.0, 1.0);
    float ph2 = mod(BEAT, 2.0);
    float tri = ph2 < 1.0 ? ph2 : 2.0 - ph2;
    float back = min(2.0 * spb(), R) * tri;
    vec4 P = pastAt(uv, back);
    vec3 c = P.rgb * (1.0 - 0.25 * (1.0 - smoothstep(0.0, 0.15, fract(BEAT))));
    vec3 base = W > 1.5 ? mix(cam, plateAt(uv), figNow) : cam;
    c = mix(base, c, amtL);
    float bar = step(uv.y, 0.012);
    float head = 1.0 - smoothstep(0.0, 0.008, abs(uv.x - tri));
    c = mix(c, mix(vec3(0.12), vec3(1.0, 0.6, 0.25), head), bar * 0.7 * amtL);
    col = composeW(uv, c, P.a, cam, roomNow, figNow, W);
  } else if (md < 6.5) {
    // ── slit (17, 204, 172, 164, 291) ──────────────────────────────
    float sh = floor(slit + 0.5);
    float back = 0.0;
    vec3 add = vec3(0.0);
    float cool = 0.0;
    float dim = 0.0;
    if (sh < 0.5) {            // FX 17: rows, top now → bottom past, mids bend the slit
      float t = 1.0 - uv.y;
      t = clamp(t + 0.25 * m * eng * sin(uv.x * 7.0 + TIME * 1.3), 0.0, 1.0);
      back = min(eng * (1.0 + 0.8 * k), 1.0) * min(R, 0.98) * t;
    } else if (sh < 1.5) {     // FX 204: feet live, head in the past
      float g = pow(smoothstep(0.08, 0.92, uv.y), 1.3);
      back = R * eng * g;
      cool = 0.25 * g * clamp(back / 0.5, 0.0, 1.0);
    } else if (sh < 2.5) {     // FX 172: columns, the live column swept by each kick
      float cx = stRead(1.0).z;
      float dist = clamp(abs(uv.x - cx) / 0.5, 0.0, 1.0);
      back = pow(dist, max(1.2 - 0.5 * m, 0.3)) * eng * R;
      add = (0.35 + 0.6 * k) * exp(-abs(uv.x - cx) * 300.0) * smoothstep(0.0, 0.1, eng) * vec3(0.9, 1.0, 1.0);
    } else if (sh < 3.5) {     // FX 164: rings from the figure's centre, further out = further back
      const float RINGS = 9.0; const float EDGE_R = 1.1;
      float d = length((uv - vec2(0.5, 0.45)) * vec2(aspect, 1.0));
      float x = d / EDGE_R * RINGS - stRead(5.0).y;
      float q = x < 0.0 ? 0.0 : clamp(floor(x) / (RINGS - 1.0), 0.0, 1.0);
      back = q * R * eng;
      float sd = abs(fract(x + 0.5) - 0.5) * EDGE_R / RINGS;
      add = 0.5 * eng * (1.0 - smoothstep(0.0, 0.006, sd)) * step(1.0, x) * vec3(0.9, 0.95, 1.0);
      float pr = (1.0 - k) * EDGE_R * 1.3;
      add += 1.2 * exp(-pow((d - pr) / 0.05, 2.0)) * k * vec3(1.0, 0.9, 0.7);
      dim = 0.25 * q;          // AGE_DIM: the outer (older) rings dim
    } else {                   // FX 291: right of the seam one beat ago, the seam drifts with the mids
      float sx = 0.5 + 0.18 * sin(TIME * 0.6) * m - 0.15 * m;
      back = step(sx, uv.x) * min(spb(), R) * eng;
      add = vec3(0.6, 0.8, 1.0) * exp(-abs(uv.x - sx) * 400.0) * eng;
    }
    vec4 P = pastAt(uv, back);
    vec3 c = P.rgb;
    c = mix(c, c * vec3(0.8, 0.95, 1.2), cool);
    c = c * (1.0 - dim) + add;
    col = composeW(uv, c, P.a, cam, roomNow, figNow, W);
  } else if (md < 7.5) {
    // ── jelly (60): rolling shutter + a travelling wobble (half-second ring) ──
    float ph = 6.2831853 * (uv.y * 1.5 + TIME * 0.9);
    float lag = 0.6 * (1.0 - uv.y) + 0.4 * (0.5 + 0.5 * sin(ph));
    float back = eng * (0.6 + 0.4 * m) * min(R, 0.5) * lag;
    vec2 suv = vec2(uv.x + eng * (1.0 + 1.5 * k) * 0.025 * cos(ph), uv.y);
    vec4 P = pastAt(suv, back);
    col = composeW(uv, P.rgb, P.a, cam, roomNow, figNow, W);
  } else if (md < 8.5) {
    // ── luma time (162): bright from the past, dark now ────────────
    vec2 o = vec2(0.006, 0.0);
    float l = (lumaOf(cam) * 2.0 + lumaOf(camAt(uv + o.xy)) + lumaOf(camAt(uv - o.xy))
             + lumaOf(camAt(uv + o.yx)) + lumaOf(camAt(uv - o.yx))) / 6.0;
    float t = pow(smoothstep(0.25 - 0.15 * m, 0.85, l), 1.3);
    float back = t * eng * R;
    vec4 P = pastAt(uv, back);
    vec3 c = P.rgb * mix(vec3(1.0), vec3(1.12, 0.98, 0.82), 0.25 * back / max(R, 0.01));
    c *= 1.0 + 0.3 * k * smoothstep(0.0, 0.1, back);
    col = composeW(uv, c, P.a, cam, roomNow, figNow, W);
  } else if (md < 9.5) {
    // ── depth time (18): near now, far in the past ─────────────────
    float t = smoothstep(0.15, 0.85, 1.0 - depthAt(uv));
    float back = eng * (0.4 + 0.6 * m) * R * t;
    vec4 P = pastAt(uv, back);
    vec3 c = P.rgb + 0.6 * k * eng * vec3(0.2, 0.9, 1.0) * smoothstep(0.0, 0.05, back) * lumaOf(P.rgb);
    col = composeW(uv, c, P.a, cam, roomNow, figNow, W);
  } else if (md < 10.5) {
    // ── depth bands (163): near band furthest back, mid band less, far band now ──
    float ff = 1.0 - figure;                               // FIGURE_FAR in the old 0 = near space
    float nearEnd = 0.5 * ff; float midEnd = 0.8 * ff; const float SB = 0.015;
    float bN = eng * R;
    float bM = bN * max(0.05, 0.5 - 0.35 * m);
    float sepN = smoothstep(0.008, 0.066, bN); float sepM = smoothstep(0.008, 0.066, bM);
    vec4 PM = pastAt(uv, bM); vec4 PN = pastAt(uv, bN);
    float dM = 1.0 - PM.a; float dN = 1.0 - PN.a;
    vec3 cM = mix(PM.rgb, vec3(0.95, 0.85, 0.35) * (0.3 + lumaOf(PM.rgb)), 0.35 * eng * sepM) * (1.0 + 0.35 * k * sepM);
    vec3 cN = mix(PN.rgb, vec3(1.0, 0.45, 0.3) * (0.3 + lumaOf(PN.rgb)), 0.35 * eng * sepN) * (1.0 + 0.35 * k * sepN);
    col = roomNow;
    col = mix(col, cam, smoothstep(midEnd - SB, midEnd + SB, 1.0 - depthAt(uv)) * (1.0 - smoothstep(ff - SB, ff + SB, 1.0 - depthAt(uv))));
    col = mix(col, cM, smoothstep(nearEnd - SB, nearEnd + SB, dM) * (1.0 - smoothstep(midEnd - SB, midEnd + SB, dM)));
    col = mix(col, cN, 1.0 - smoothstep(nearEnd - SB, nearEnd + SB, dN));
  } else if (md < 11.5) {
    // ── time patches (165) ─────────────────────────────────────────
    const float LEVELS = 5.0;
    float seed = stRead(5.0).x;
    float e = smoothstep(0.008, 0.1, eng * R);
    vec2 p = vec2(uv.x * aspect, uv.y) * 2.6 + TIME * 0.05 * vec2(1.0, 0.6);
    float n = mix(pfield(p, seed), pfield(p, seed - 1.0), k);
    n = clamp((n - 0.2) / 0.6, 0.0, 0.999);
    float q = n * LEVELS;
    float lv = floor(q) / (LEVELS - 1.0);
    vec4 P = pastAt(uv, eng * R * lv);
    vec3 c = P.rgb * mix(vec3(1.0), vec3(0.85, 0.92, 1.1), 0.25 * lv * e);
    float fr = fract(q); float w = fwidth(q) * 1.5;
    float seam = (1.0 - smoothstep(0.0, w, min(fr, 1.0 - fr))) * step(0.02, q) * step(q, LEVELS - 0.02);  // no seam at the clamped ends
    c += seam * 0.55 * (1.0 + 0.6 * m) * e * vec3(1.0, 0.85, 0.6);
    col = composeW(uv, c, P.a, cam, roomNow, figNow, W);
  } else if (md < 12.5) {
    // ── interlace (59): odd bands δt ago ───────────────────────────
    const float LINE_PX = 4.0;
    float py = uv.y * RENDERSIZE.y;
    float odd = step(0.5, mod(floor(py / LINE_PX), 2.0));
    float side = mod(floor(py / (LINE_PX * 2.0)), 2.0) * 2.0 - 1.0;
    vec2 puv = clamp(uv + vec2(side * 0.04 * eng * k, 0.0), 0.0, 1.0);
    vec4 P = pastAt(puv, min(1.0 * eng * (1.0 + 0.4 * m), R));
    vec3 past = P.rgb * (1.0 - 0.12 * eng);
    past = mix(past, past * vec3(0.8, 0.95, 1.15), clamp(0.5 * m, 0.0, 1.0) * eng);
    col = mix(cam, composeW(uv, past, P.a, cam, roomNow, figNow, W), odd);
  } else if (md < 13.5) {
    // ── time RGB (152): R now, G δt, B 2δt ─────────────────────────
    float d = clamp(eng * 0.73 * (1.0 + 0.25 * m * sin(TIME * 1.3)), 0.0, R * 0.5);
    float a = TIME * 0.4;
    vec2 drift = 0.012 * m * eng * vec2(cos(a), sin(a));
    vec4 P1 = pastAt(uv + drift, d); vec4 P2 = pastAt(uv - drift, 2.0 * d);
    vec3 c1 = W > 0.5 ? mix(roomNow, P1.rgb, pfig(P1.a)) : P1.rgb;
    vec3 c2 = W > 0.5 ? mix(roomNow, P2.rgb, pfig(P2.a)) : P2.rgb;
    vec3 c = vec3(cam.r, c1.g, c2.b);
    c *= 1.0 + 0.25 * k * clamp(length(c - cam) * 2.0, 0.0, 1.0);
    col = c;
  } else if (md < 14.5) {
    // ── ghosts (151, 154, 156, 166): n past figures behind the live one ──
    float n = clamp(floor(heads + 0.5), 1.0, 4.0);
    float op = clamp(amount + kickAmt * k + midsAmt * m, 0.0, 1.0);
    float far = R * clamp(0.3 + 0.7 * amount + 0.12 * m, 0.0, 1.0);
    col = cam;
    for (int i = 4; i >= 1; i--) {               // oldest first, newer on top
      float fi = float(i);
      if (fi > n) continue;
      float back = max(far * fi / n, 0.03);
      vec4 P = pastAt(uv, back);
      vec3 tint = n < 1.5 ? vec3(0.45, 0.85, 1.25)
                : (fi < 1.5 ? vec3(1.25, 0.45, 0.95) : (fi < 2.5 ? vec3(0.75, 0.45, 1.25) : (fi < 3.5 ? vec3(0.35, 0.6, 1.3) : vec3(0.3, 0.95, 1.0))));
      vec3 g = mix(P.rgb, (0.2 + lumaOf(P.rgb)) * tint, n < 1.5 ? 0.65 : 0.7) * (1.0 + 0.5 * k);
      g += 0.6 * pastEdge(uv, back, 3.0) * tint;                        // FX 154 rim
      col = mix(col, g, pfig(P.a) * 0.85 * (1.0 - 0.28 * (fi - 1.0)) * op);
    }
    if (n < 1.5) {                                // FX 151 read-head bar
      float bar = step(uv.y, 0.012);
      float hd = 1.0 - smoothstep(0.0, 0.006, abs(uv.x - far / max(R, 0.01)));
      col = mix(col, mix(vec3(0.15), vec3(0.45, 0.85, 1.25), hd), bar * 0.7 * op);
    }
    col = mix(col, cam, figNow);
  } else if (md < 15.5) {
    // ── beat echoes (158): ½, 1 and 2 beats ago ────────────────────
    float hit = kickAmt * 1.5 * k; float mm = midsAmt * m;
    vec3 ops = vec3(clamp(amount + hit + mm, 0.0, 1.0) * 0.85, clamp(amount + hit, 0.0, 1.0) * 0.65, clamp(amount + hit, 0.0, 1.0) * 0.45);
    col = cam;
    for (int i = 2; i >= 0; i--) {
      float fi = float(i);
      float beats = fi < 0.5 ? 0.5 : (fi < 1.5 ? 1.0 : 2.0);
      vec3 tint = fi < 0.5 ? vec3(1.0, 0.35, 0.75) : (fi < 1.5 ? vec3(0.35, 0.75, 1.0) : vec3(0.4, 1.0, 0.55));
      float o = fi < 0.5 ? ops.x : (fi < 1.5 ? ops.y : ops.z);
      vec4 P = pastAt(uv, beats * spb());
      vec3 g = mix(P.rgb, tint * (0.25 + 1.1 * lumaOf(P.rgb)), 0.65) * (1.0 + 0.4 * k);
      col = mix(col, g, pfig(P.a) * o);
    }
    col = mix(col, cam, figNow);
  } else if (md < 16.5) {
    // ── zoom ghosts (170) ──────────────────────────────────────────
    float op = clamp(amount + kickAmt * k + midsAmt * m, 0.0, 1.0);
    float gap = (10.0 + 18.0 * m) / 60.0 * R / 1.5;
    const vec2 ZC = vec2(0.5, 0.45);
    col = cam;
    for (int j = 2; j >= 0; j--) {
      float fi = float(j + 1);
      float s = 1.0 + 0.12 * fi * (1.0 + 0.6 * k);
      vec2 puv = ZC + (uv - ZC) / s;
      float inside = step(0.0, puv.x) * step(puv.x, 1.0) * step(0.0, puv.y) * step(puv.y, 1.0);
      vec4 P = pastAt(puv, fi * gap);
      vec3 gc = j == 0 ? vec3(0.3, 0.95, 1.0) : (j == 1 ? vec3(1.0, 0.3, 0.85) : vec3(1.0, 0.8, 0.25));
      vec3 pc = mix(P.rgb, gc * (0.3 + 0.9 * lumaOf(P.rgb)), 0.45);
      col = mix(col, pc, pfig(P.a) * inside * op * 0.85 * (1.0 - 0.45 * float(j) / 2.0));
    }
    col = mix(col, cam, figNow);
  } else if (md < 17.5) {
    // ── silhouette stack (129, 130): past figures, hue per age ─────
    float span = min(R, 0.7) * eng;
    float h0 = TIME * 0.05 + 0.35 * m;
    col = cam;
    for (int i = 8; i >= 1; i--) {
      float a = float(i) / 8.0;                    // 1 = oldest
      vec4 P = pastNear(uv, max(span * a, 0.02));
      vec3 c = mix(P.rgb, hueTri(h0 + 0.85 * a) * (0.35 + 0.9 * lumaOf(P.rgb)), 0.7);
      col = mix(col, c, pfig(P.a) * (1.0 - 0.6 * a) * smoothstep(0.0, 0.25, eng));
    }
    col = mix(col, cam, figNow);
  } else if (md < 18.5) {
    // ── outline rings (219; 238 with where=2) ──────────────────────
    float amtO = clamp(amount + kickAmt * k + midsAmt * m, 0.0, 1.0);
    if (W > 1.5) {          // FX 238: the room one beat late, the figure's outline half a beat ago
      vec3 room = mix(cam, pastAt(uv, min(spb(), R)).rgb, smoothstep(0.0, 0.6, amtO));
      float hb = min(0.5 * spb(), R);
      vec4 P = pastNear(uv, hb);
      room = mix(room, P.rgb, pfig(P.a) * 0.15 * amtO);
      room += vec3(1.0, 0.35, 0.85) * pastEdge(uv, hb, 4.0) * 1.2 * amtO;
      col = mix(room, cam, figNow);
    } else {
      vec3 room = cam * (1.0 - 0.25 * amtO);
      float sp = max(R / 6.0 * clamp(0.25 + 0.75 * amount + 0.2 * m, 0.0, 1.0), 0.05);
      for (int i = 5; i >= 0; i--) {
        float fi = float(i);
        float a = i < 4 ? amtO : k;
        float age = fi / 5.0;
        float line = pastEdge(uv, sp * (fi + 1.0), 4.0);
        room = mix(room, mix(vec3(1.0, 0.85, 0.45), vec3(0.55, 0.25, 0.75), age) * (1.15 - 0.4 * age), line * a * (1.0 - 0.35 * age));
      }
      col = mix(room, cam, figNow);
    }
  } else if (md < 19.5) {
    // ── room echoes (184): copies of the past frame, shoved and shrunk, lighten-stacked ──
    float count = clamp(amount * 4.0 + 2.5 * kickAmt / 0.6 * k + 0.6 * m, 0.0, 6.0);
    float spread = min((3.0 + 11.0 * amount) / 60.0, R / 6.0);
    vec3 acc = roomNow;
    for (int i = 6; i >= 1; i--) {
      float fi = float(i);
      float w = clamp(count - fi + 1.0, 0.0, 1.0);
      if (w <= 0.0) continue;
      float side = mod(fi, 2.0) < 0.5 ? 1.0 : -1.0;
      vec2 q = clamp((uv - 0.5) * (1.0 + 0.015 * fi) + 0.5 + vec2(side * 0.018 * ceil(fi * 0.5), 0.0), 0.0, 1.0);
      vec3 c = pastAt(q, fi * spread).rgb;
      vec3 hu = 0.6 + 0.4 * cos(6.2831853 * (fi / 6.0 + vec3(0.0, 0.33, 0.67)));
      c = mix(c, c * hu * 1.4, 0.35) * (1.0 + 0.5 * k);
      acc = mix(acc, max(acc, c), 0.55 * w * (1.0 - 0.6 * (fi - 1.0) / 6.0));
    }
    col = mix(acc, cam, figNow);
  } else if (md < 20.5) {
    // ── kick throw-back (153): each kick throws the head ~1 s back, it catches up ──
    float THROW = min(1.0, R * 0.9);
    float back = max(0.0, THROW - since * THROW / (0.45 * (1.0 + 0.8 * m))) + 0.33 * amount * (R / 1.5);
    float thrown = clamp(back / THROW, 0.0, 1.0);
    float op = clamp(amount * 0.5 + 0.9 * thrown + 0.25 * m, 0.0, 1.0) * smoothstep(0.008, 0.066, back);
    vec4 P = pastAt(uv, back);
    float pf = pfig(P.a);
    const vec3 TINT = vec3(0.45, 0.8, 1.25);
    vec3 ghost = mix(P.rgb, TINT * (0.25 + 1.1 * lumaOf(P.rgb)), 0.6) * (1.0 + 0.6 * k);
    ghost += 0.8 * pastEdge(uv, back, 3.0) * TINT;
    col = mix(roomNow, cam, figNow);
    col = mix(col, ghost, pf * op * mix(1.0, 0.55, figNow));
  } else if (md < 21.5) {
    // ── now/past gate (157): pressure crossfades now↔past, each kick swaps who's in front ──
    float amt = eng;
    float flip = stRead(1.0).y;
    vec4 P = pastAt(uv, min(56.0 / 60.0, R));
    float dNow = 1.0 - depthAt(uv); float dPast = 1.0 - P.a;
    float fNow = figNow; float fPast = pfig(P.a) * step(0.002, amt);
    vec3 cPast = mix(P.rgb, vec3(lumaOf(P.rgb)), 0.35) * vec3(0.75, 0.9, 1.25);
    float aNow = fNow * (1.0 - amt * (1.0 - 0.45));
    float aPast = fPast * amt;
    float bias = (2.0 * flip - 1.0) * 0.15;
    float pastFront = smoothstep(-0.03, 0.03, (dNow + bias) - dPast);
    vec3 nowOver = mix(mix(roomNow, cPast, aPast), cam, aNow);
    vec3 pastOver = mix(mix(roomNow, cam, aNow), cPast, aPast);
    col = mix(nowOver, pastOver, pastFront);
    col += 0.6 * k * fNow * fPast * amt * mix(vec3(1.0, 0.85, 0.6), vec3(0.75, 0.9, 1.25) * 0.6, pastFront) * 0.5;
  } else if (md < 22.5) {
    // ── two clocks (167): the room late by pressure, the figure late by the mids ──
    float rLag = clamp(amount + kickAmt * k, 0.0, 1.0);
    float rBack = rLag * min(1.2, R);
    float fBack = pow(clamp(m, 0.0, 1.0), 1.5) * R;
    float fLag = fBack / max(R, 0.01);
    vec4 PR = pastAt(uv, rBack);
    vec3 room = mix(PR.rgb, plateAt(uv), pfig(PR.a));          // the late room, its own figure taken out
    room = mix(room, mix(room, vec3(lumaOf(room)), 0.4) * vec3(1.1, 0.95, 0.78), 0.5 * rLag) * (1.0 + 0.2 * k);
    vec4 PF = pastAt(uv, fBack);
    vec3 cf = mix(PF.rgb, mix(PF.rgb, vec3(lumaOf(PF.rgb)), 0.4) * vec3(0.8, 0.95, 1.2), 0.5 * fLag);
    col = mix(room, cf, pfig(PF.a));
    vec2 p = uv - vec2(0.03, 0.04);
    float barR = step(0.0, p.x) * step(p.x, 0.25 * rLag) * step(0.0, p.y) * step(p.y, 0.006);
    float barF = step(0.0, p.x) * step(p.x, 0.25 * fLag) * step(0.012, p.y) * step(p.y, 0.018);
    col = mix(col, vec3(1.0, 0.75, 0.4), barR);
    col = mix(col, vec3(0.4, 0.75, 1.0), barF);
  } else if (md < 23.5) {
    // ── where you were (169): your past silhouette is a window into another picture ──
    float amt = clamp(amount + kickAmt * k + midsAmt * m, 0.0, 1.0);
    float on = smoothstep(0.0, 0.15, amt);
    float back = mix(0.1, R, amt);
    float was = pfig(pastNear(uv, back).a);
    float edge = pastEdge(uv, back, 3.0);
    vec3 win = mix(cam, otherPic(roomNow), was);
    win += 0.9 * (1.0 + k) * edge * vec3(1.0, 0.92, 0.75);
    win = mix(win, cam, figNow);
    col = mix(cam, win, on);
  } else {
    // ── silhouette swap (279): the cut-out from a second ago, the colour live ──
    float lag = min(62.0 / 60.0, R) * eng;
    float mk = pfig(pastAt(uv, lag).a);
    vec3 c = mix(otherPic(roomNow), cam, mk);
    vec2 tx = 2.0 / RENDERSIZE;
    float e = abs(figNow - figureAt(uv + vec2(tx.x, 0.0))) + abs(figNow - figureAt(uv + vec2(0.0, tx.y)));
    c = mix(c, vec3(0.5, 0.9, 1.0), clamp(e * 4.0, 0.0, 1.0) * 0.25 * clamp(lag / 0.17, 0.0, 1.0));
    col = mix(cam, c, smoothstep(0.0, 0.05, lag));
  }
  gl_FragColor = vec4(col, 1.0);
}
