/*{
  "DESCRIPTION": "glass: a pane between the camera and the room. reeded glass: fluted bathroom glass, each flute a narrow upside-down slice of what's behind, bending harder on the kicks, drifting on the mids, the ridges catching light on the hats · rain on the window: beads of water on a misted pane, each a tiny lens; runners slide down a step on every beat leaving clear trails, more beads on the mids, glinting on the hats · breath on glass: the pane mists over (faster on the mids) and each kick wipes a stroke clear across it, as a hand would; the hats stir the mist's grain. figureClear lets the face show through.",
  "CREDIT": "Chris Poucet, simply-jam",
  "CATEGORIES": ["texture", "blur", "depth", "sound", "beat", "modes", "pressure"],
  "INPUTS": [
    { "NAME": "inputImage", "TYPE": "image" },
    { "NAME": "depth", "TYPE": "depth" },
    { "NAME": "wave", "TYPE": "audio" },
    { "NAME": "spectrum", "TYPE": "audioFFT" },
    { "NAME": "amount", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 1.0, "DESCRIPTION": "pressure: how much of the glass there is (reeds: how hard they bend; rain: how misted; breath: how thick the mist)" },
    { "NAME": "react", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5, "DESCRIPTION": "how much the music moves it: 0 holds it still, 0.5 as tuned, 1 twice as much" },
    { "NAME": "mode", "TYPE": "long", "MIN": 0, "MAX": 2, "DEFAULT": 0, "VALUES": [0, 1, 2], "LABELS": ["reeded glass", "rain on the window", "breath on glass"] },
    { "NAME": "scale", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5, "DESCRIPTION": "reeds: how wide the flutes · rain: how big the beads · breath: how wide a wipe" },
    { "NAME": "figureClear", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.3, "DESCRIPTION": "how far the figure shows through clearer than the room" },
    { "NAME": "regrow", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.4, "DESCRIPTION": "breath: how fast the mist comes back over a wipe" },
    { "NAME": "kickAmt", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.7, "DESCRIPTION": "kicks bend the reeds, shake the runners, wipe the mist" },
    { "NAME": "midsAmt", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5, "DESCRIPTION": "mids drift the reeds, bring more beads, mist the pane faster" },
    { "NAME": "highsAmt", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5, "DESCRIPTION": "hats make the glass glint" },
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
    { "TARGET": "mist", "WIDTH": "$WIDTH / 4", "HEIGHT": "$HEIGHT / 4", "FLOAT": true, "PERSISTENT": true },
    {}
  ]
}*/

// `mist` (quarter size) is the breath mode's fog on the pane, 0 clear to 1
// misted; the other modes don't need it and write nothing there. The rain
// is drawn fresh each frame from hashes, so it costs no buffer.

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
float hash1(float x) { return fract(sin(x * 91.3458) * 47453.5453); }
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
vec2 h22(vec2 p) { return fract(sin(vec2(dot(p, vec2(127.1, 311.7)), dot(p, vec2(269.5, 183.3)))) * 43758.5453); }
float vnoise(vec2 p) {
  vec2 i = floor(p); vec2 f = fract(p); f = f * f * (3.0 - 2.0 * f);
  return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
// the picture through frosted glass: a 14-tap spiral blur turned at random
// per pixel, so its structure reads as the glass's grain. r is in 360ths
// of the picture's height, the same at any size.
vec3 frosted(vec2 uv, float r) {
  vec2 px = r / 360.0 * vec2(RENDERSIZE.y / max(RENDERSIZE.x, 1.0), 1.0);
  float turn = hash(floor(uv * RENDERSIZE)) * 6.2831;
  vec3 s = cam(uv) * 2.0;
  for (int i = 0; i < 12; i++) {
    float a = float(i) * 2.39996 + turn;
    float rr = sqrt((float(i) + 0.5) / 12.0);
    s += cam(uv + vec2(cos(a), sin(a)) * rr * px);
  }
  return s / 14.0;
}

// a kick landed this frame (from the sound, or the Link beat with beatSync)
float kickEdge() {
  float snd = step(kickAge(), 0.5 * dtNow());
  float link = step(fract(BEAT), dtNow() * TEMPO / 60.0) * step(0.5, beatSync);
  return max(snd, link);
}
// how long ago the last kick was, either source
float sinceKick() { return mix(kickAge(), fract(BEAT) * 60.0 / max(TEMPO, 1.0), step(0.5, beatSync)); }

float segDist(vec2 p, vec2 a, vec2 b, out float along) {
  vec2 ab = b - a;
  along = clamp(dot(p - a, ab) / max(dot(ab, ab), 1e-6), 0.0, 1.0);
  return length(p - a - ab * along);
}

vec4 mistPass(vec2 uv) {
  if (floor(mode + 0.5) < 1.5) { return vec4(0.0); }
  float fresh = 1.0 - step(1.0, FRAMEINDEX);
  float aspect = RENDERSIZE.x / max(RENDERSIZE.y, 1.0);
  float m = IMG_NORM_PIXEL(mist, uv).r;
  m = mix(m, 1.0, fresh);
  float dt = dtNow();
  float mids = midsNow() * midsAmt;
  // the mist comes back, unevenly, faster on the mids
  float grow = (0.05 + 0.5 * regrow * regrow) * (0.4 + 0.6 * vnoise(uv * 9.0)) * (1.0 + 2.0 * mids);
  m = min(m + dt * grow, 1.0);
  // a wipe: a stroke drawn over a quarter second after each kick
  float age = sinceKick();
  float seed = floor((TIME - age) * 7.0);
  vec2 a = vec2(0.1 + 0.8 * hash1(seed), 0.15 + 0.7 * hash1(seed + 1.3));
  vec2 b = vec2(0.1 + 0.8 * hash1(seed + 2.7), 0.15 + 0.7 * hash1(seed + 4.1));
  if (length((b - a) * vec2(aspect, 1.0)) < 0.35) { b = vec2(1.0) - a; }
  float drawn = clamp(age / 0.25, 0.0, 1.0);
  float along;
  float dist = segDist(uv * vec2(aspect, 1.0), a * vec2(aspect, 1.0), mix(a, b, drawn) * vec2(aspect, 1.0), along);
  float w = (0.05 + 0.12 * scale) * (0.7 + 0.6 * kickAmt);
  float wipe = (1.0 - smoothstep(w * 0.6, w, dist + 0.02 * vnoise(uv * 40.0))) * step(age, 0.3) * kickAmt;
  m = min(m, 1.0 - wipe);
  return vec4(m, 0.0, 0.0, 1.0);
}

void main() {
  vec2 uv = isf_FragNormCoord;
  if (PASSINDEX == 0.0) { gl_FragColor = sigPass(); return; }
  if (PASSINDEX == 1.0) { gl_FragColor = depthTimePass(uv); return; }
  if (PASSINDEX == 2.0) { gl_FragColor = mistPass(uv); return; }

  float kick = kickNow() * kickAmt;
  float mids = midsNow() * midsAmt;
  float hats = highsNow() * highsAmt;
  float m = floor(mode + 0.5);
  float aspect = RENDERSIZE.x / max(RENDERSIZE.y, 1.0);
  vec3 c = cam(uv);
  float clear = figureAt(uv) * figureClear;
  vec3 outc;

  if (m < 0.5) {
    // reeded glass: vertical flutes, each a cylinder lens that flips the slice behind it
    float n = mix(70.0, 14.0, scale);
    float drift = 0.25 * sin(TIME * 0.21) + 0.6 * mids;
    float x = uv.x * n + drift;
    float f = fract(x);
    float cx = (floor(x) + 0.5 - drift) / n;
    float k = (1.5 + 3.0 * kick) * amount;
    float bend = (f - 0.5) / n;
    vec3 r;
    r.r = cam(vec2(mix(cx - bend * k * 1.06, uv.x, clear), uv.y)).r;
    r.g = cam(vec2(mix(cx - bend * k, uv.x, clear), uv.y)).g;
    r.b = cam(vec2(mix(cx - bend * k * 0.94, uv.x, clear), uv.y)).b;
    // a little softness through the glass, and light on each ridge
    vec3 soft2 = cam(vec2(mix(cx - bend * k, uv.x, clear), uv.y + 2.5 / RENDERSIZE.y)) + cam(vec2(mix(cx - bend * k, uv.x, clear), uv.y - 2.5 / RENDERSIZE.y));
    r = mix(r, soft2 * 0.5, 0.35 * (1.0 - clear));
    float ridge = pow(abs(f - 0.5) * 2.0, 10.0);
    float shade = 0.88 + 0.12 * sin(f * 3.14159);
    outc = r * shade + ridge * (0.05 + 0.25 * hats) * (1.0 - clear);
    outc = mix(c, outc, min(1.0, amount * 2.0));
  } else if (m < 1.5) {
    // rain: a misted pane, beads that clear and flip what's behind them, runners with trails
    vec2 p = (uv - 0.5) * vec2(aspect, 1.0);
    float size = mix(0.6, 1.8, scale);
    vec2 off = vec2(0.0);
    float wet = 0.0;      // 1 where water has cleared the mist
    float glint = 0.0;
    float rimDark = 0.0;  // the edge of a bead, where it bends light away
    // beads, two grids
    for (int l = 0; l < 2; l++) {
      float g = (l == 0 ? 14.0 : 26.0) / size;
      vec2 q = p * g + float(l) * 17.3;
      vec2 id = floor(q);
      vec2 h = h22(id);
      float there = step(h.x, 0.2 + 0.4 * mids);
      vec2 ctr = 0.25 + 0.5 * h22(id + 7.1);
      float rad = mix(0.12, 0.38, h.y);
      vec2 dq = (fract(q) - ctr) / rad;
      float d = length(dq * vec2(1.0, 0.9));
      float bead = (1.0 - smoothstep(0.85, 1.0, d)) * there;
      off -= dq * bead * rad / g * 1.6;
      wet = max(wet, bead);
      rimDark = max(rimDark, there * smoothstep(0.6, 0.9, d) * (1.0 - smoothstep(0.9, 1.0, d)));
      glint = max(glint, bead * smoothstep(0.5, 0.9, dot(normalize(dq + 1e-4), vec2(-0.6, 0.8))) * smoothstep(0.4, 0.95, d));
    }
    // runners: one a column, sliding down a step on each beat, creeping between
    float cols = 9.0 / size;
    float cxi = floor((p.x + 2.0) * cols);
    float hx = hash1(cxi);
    float colX = (cxi + 0.3 + 0.4 * hash1(cxi + 3.3)) / cols - 2.0;
    float steps = floor(BEAT) + smoothstep(0.0, 0.2, fract(BEAT));
    float fall = fract(hx + TIME * 0.02 * (0.5 + hx) + steps * (0.03 + 0.06 * kickAmt) * (0.5 + hx));
    float ry = 0.6 - fall * 1.3;
    float wob = 0.004 * sin(p.y * 60.0 + hx * 20.0);
    vec2 rd = vec2((p.x - colX - wob) * cols * 5.0, (p.y - ry) * cols * 3.2);
    float rdist = length(rd);
    float runner = (1.0 - smoothstep(0.8, 1.0, rdist)) * step(0.35, hx);
    off -= vec2(rd.x, rd.y * 0.6) * runner * 0.012;
    wet = max(wet, runner);
    float trail = (1.0 - smoothstep(0.08, 0.16, abs(p.x - colX - wob) * cols)) * step(ry, p.y) * smoothstep(ry + 0.9, ry, p.y) * step(0.35, hx);
    wet = max(wet, trail * 0.85);
    glint = max(glint, runner * smoothstep(0.3, 0.9, rdist) * step(0.0, rd.y));
    float misty = amount * (1.0 - clear * 0.8);
    vec3 behind = frosted(uv, 14.0 * misty) * (1.0 - 0.12 * misty) + vec3(0.06, 0.07, 0.08) * misty;
    vec3 through = cam(uv + off * vec2(1.0 / aspect, 1.0));
    through = mix(through * 1.15 + 0.03, behind, 0.15);
    outc = mix(behind, through, wet) * (1.0 - 0.35 * rimDark);
    outc += glint * (0.15 + 0.6 * hats);
  } else {
    // breath: the mist buffer, frosted where it is, streaky at its edges
    float mi = IMG_NORM_PIXEL(mist, uv).r;
    float streak = vnoise(vec2(uv.x * 60.0, uv.y * 4.0));
    mi = smoothstep(0.15, 0.75, mi + 0.15 * (streak - 0.5)) * amount * (1.0 - clear);
    float grain = 0.04 * (hash(floor(uv * RENDERSIZE / 2.0) + floor(TIME * 24.0)) - 0.5) * (0.3 + hats);
    vec3 fog = frosted(uv, 3.0 + 16.0 * mi) * (1.0 - 0.25 * mi) + vec3(0.13, 0.14, 0.16) * mi + grain * mi;
    outc = mix(c, fog, mi);
  }
  gl_FragColor = vec4(clamp(outc, 0.0, 1.0), 1.0);
}
