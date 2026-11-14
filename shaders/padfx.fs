/*{
  "DESCRIPTION": "Pad FX: 24 performance effects, each an fx_* float 0..1 (pad pressure: harder = stronger), all 0 = the picture untouched. Warps (zoom, mirror, tile, ripple, twirl, pixel, slices, patches: a few random regions pixelate, shove or stretch and jump to new places on every beat and kick, harder = more and stronger patches, glitch), reads (RGB split, blur, figure lift: the bright parts pushed forward over a darkened room, swelling on the kick), memory (trails, tunnel, datamosh drag: blocks of the last frame dragged along, a kick lets the live picture back in; echo: a ghost of the picture one beat ago), grades (invert, mono, hue, posterize, thermal) and finishers (film light leak flaring on kicks and flickering with the highs, blackout, scanlines). Sits last on every chain, never swapped, so the pads work on whichever look is live.",
  "CREDIT": "audiodev set; maths from the simply-creative FX library (12, 13, 23, 26, 27, 28, 41, 42, 50, 52, 71, 95, 131 …)",
  "CATEGORIES": ["performance-pads", "feedback", "history", "sound", "beat", "pressure"],
  "INPUTS": [
    { "NAME": "inputImage", "TYPE": "image" },
    { "NAME": "wave", "TYPE": "audio" },
    { "NAME": "spectrum", "TYPE": "audioFFT" },
    { "NAME": "fx_zoom", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_mirror", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_tile", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_ripple", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_twirl", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_pixel", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_slices", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_patches", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_glitch", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_rgb", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_blur", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_lift", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_trails", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_tunnel", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_drag", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_echo", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_invert", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_mono", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_hue", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_posterize", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_thermal", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_leak", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_blackout", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "fx_scan", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "gain", "TYPE": "float", "MIN": 0.25, "MAX": 8.0, "DEFAULT": 1.0 }
  ],
  "PASSES": [
    { "TARGET": "sig", "WIDTH": "2", "HEIGHT": "1", "FLOAT": true, "PERSISTENT": true },
    { "TARGET": "echo", "WIDTH": "$WIDTH / 2", "HEIGHT": "$HEIGHT / 2", "FLOAT": true, "PERSISTENT": true },
    { "TARGET": "fb", "PERSISTENT": true },
    {}
  ]
}*/

// ── grab these first ────────────────────────────────────────────────
const float ZOOM_MAX    = 0.45;  // fraction of the frame cut away at full press
const vec2  PATCH_GRID  = vec2(7.0, 4.0); // patches live one per cell of this grid, at most
const float PATCH_SHARE = 0.4;   // share of cells holding a patch at full press (a light press: about 1 in 12)
const float LIFT_GROW   = 0.10;  // how much bigger the lifted figure gets at full press
const vec2  LIFT_SHADOW = vec2(0.012, -0.018); // the lifted figure's shadow falls down-right (y up)
const float DRAG_PX     = 0.010; // uv a datamosh block drags per frame at full press
const float ECHO_DRIFT  = 0.025; // uv the beat's ghost sits off the live picture at full press
const vec3  LEAK_HOT    = vec3(1.0, 0.32, 0.08); // the light leak's colour at its edge …
const vec3  LEAK_CORE   = vec3(1.0, 0.82, 0.40); // … and in its core
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
  if (fx_zoom > 0.001) { p *= 1.0 - ZOOM_MAX * fx_zoom * (0.8 + 0.2 * kick); }
  if (fx_twirl > 0.001) {
    float r = length(p);
    p = rot(p, 6.2831853 * TWIRL_TURNS * fx_twirl * smoothstep(0.7, 0.0, r));
  }
  if (fx_ripple > 0.001) {
    float r = length(p);
    p += normalize(p + 1e-5) * RIPPLE_AMP * fx_ripple * sin(40.0 * r - TIME * 10.0) * (0.5 + kick);
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
  if (fx_patches > 0.001) {
    // patches: random rectangles, at most one per grid cell, re-thrown on every beat and every kick;
    // each pixelates, shoves its contents aside, or stretches its left edge across itself
    float age = kickAge();
    float kickSeed = step(age, 30.0) * fract(floor((TIME - age) * 4.0) * 0.6180339) * 977.0;
    float seed = kickSeed + floor(BEAT) * 13.0;
    vec2 off = vec2(hash(vec2(seed, 1.0)), hash(vec2(seed, 2.0)));
    vec2 g = q * PATCH_GRID + off;
    vec2 cell = floor(g);
    vec2 f = g - cell;
    float pick = step(hash(cell + seed), mix(0.08, PATCH_SHARE, fx_patches));
    vec2 lo = vec2(hash(cell + seed + 3.1), hash(cell + seed + 4.7)) * 0.45;
    vec2 hi = lo + 0.3 + 0.25 * vec2(hash(cell + seed + 5.3), hash(cell + seed + 6.9));
    float inside = pick * step(lo.x, f.x) * step(f.x, hi.x) * step(lo.y, f.y) * step(f.y, hi.y);
    if (inside > 0.5) {
      float kind = hash(cell + seed + 8.2);
      float s = fx_patches * (0.6 + 0.4 * kick);
      if (kind < 0.4) {
        float cells = mix(90.0, 10.0, s);
        vec2 cs = vec2(cells, cells / aspect);
        q = (floor(q * cs) + 0.5) / cs;
      } else if (kind < 0.7) {
        q += (vec2(hash(cell + seed + 9.4), hash(cell + seed + 10.1)) - 0.5) * 0.18 * s;
      } else {
        float edge = (cell.x + lo.x - off.x) / PATCH_GRID.x;
        float row = floor(q.y * RENDERSIZE.y / 3.0);
        q.x = mix(q.x, edge, s) + (hash(vec2(row, floor(TIME * 15.0))) - 0.5) * 0.01 * s;
      }
    }
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

// Figure lift, depth-free: the bright parts (taken as near) grow toward the viewer and cast a
// shadow, the rest of the room dims. The kick swells the lift.
float liftKey(vec2 q) { return smoothstep(0.35, 0.65, lumaOf(IMG_NORM_PIXEL(inputImage, q).rgb)); }
vec3 lift(vec2 q, vec3 c, float kick) {
  float s = 1.0 - LIFT_GROW * fx_lift * (0.7 + 0.3 * kick);
  vec2 ql = (q - 0.5) * s + 0.5;
  vec3 cl = readAt(ql);
  float key = smoothstep(0.35, 0.65, lumaOf(cl));
  float shadow = liftKey((q - LIFT_SHADOW * fx_lift - 0.5) * s + 0.5);
  vec3 room = c * (1.0 - 0.6 * fx_lift * shadow) * (1.0 - 0.35 * fx_lift);
  return mix(room, cl * (1.0 + 0.2 * fx_lift * kick), key * smoothstep(0.0, 0.2, fx_lift));
}

// Film light leak: two warm soft sources wandering along the left and top edges,
// flaring on the kick and flickering with the highs; screened over the picture.
vec3 leak(vec2 uv, float kick, float hi) {
  float t = TIME * 0.07;
  vec2 a = vec2(-0.05 + 0.12 * sin(t * 1.3), 0.3 + 0.25 * (1.0 + sin(t)));
  vec2 b = vec2(0.2 + 0.3 * (1.0 + sin(t * 0.7 + 2.0)), 1.05);
  vec2 da = (uv - a) * vec2(2.2, 0.9);
  vec2 db = (uv - b) * vec2(0.8, 2.6);
  float la = exp(-3.0 * dot(da, da));
  float lb = exp(-3.0 * dot(db, db));
  vec3 col = mix(LEAK_HOT, LEAK_CORE, la) * la + LEAK_HOT * lb * 0.8;
  return col * (0.55 + 0.45 * kick + 0.25 * hi * (0.5 + 0.5 * sin(TIME * 40.0)));
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
    // echo: hold the incoming picture from the start of the beat; alpha keeps the phase to spot the wrap
    vec4 held = IMG_NORM_PIXEL(echo, uv);
    float take = max(step(PHASE + 0.5, held.a), 1.0 - step(1.0, FRAMEINDEX));
    gl_FragColor = vec4(mix(held.rgb, IMG_NORM_PIXEL(inputImage, uv).rgb, take), PHASE);
    return;
  }

  if (PASSINDEX == 2.0) {
    vec2 q = warpUV(uv, kick);
    vec3 c = readAt(q);
    if (fx_lift > 0.001) { c = lift(q, c, kick); }
    c = grade(c, kick);
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
    if (fx_drag > 0.001) {
      // datamosh drag: each block drags the last frame its own way (new ways every beat); a kick lets the live picture in
      float aspect = RENDERSIZE.x / max(RENDERSIZE.y, 1.0);
      vec2 cell = floor(uv * vec2(24.0, 24.0 / aspect));
      float bt = floor(BEAT);
      vec2 dir = vec2(hash(cell + bt), hash(cell + bt + 17.3)) - 0.5;
      vec3 prev = IMG_NORM_PIXEL(fb, uv - dir * 2.0 * DRAG_PX * fx_drag).rgb;
      float keep = fx_drag * 0.96 * (1.0 - 0.85 * kick) * step(0.25, hash(cell + 3.7 + bt));
      c = mix(c, prev, keep);
    }
    if (fx_echo > 0.001) {
      // echo: the picture from one beat ago, offset a little, brightest early in the beat
      float bt = floor(BEAT);
      vec2 off = (vec2(hash(vec2(bt, 4.0)), hash(vec2(bt, 9.0))) - 0.5) * 2.0 * ECHO_DRIFT * fx_echo;
      vec3 g = IMG_NORM_PIXEL(echo, uv + off).rgb;
      float a = fx_echo * mix(1.0, 0.35, PHASE);
      c = mix(c, max(c, g), a);
      c = mix(c, g, 0.3 * a);
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
  if (fx_leak > 0.001) {
    vec3 l = clamp(leak(uv, kick, sigB().b) * fx_leak * 1.4, 0.0, 1.0);
    c = 1.0 - (1.0 - c) * (1.0 - l);
    c += vec3(0.04, 0.015, 0.0) * fx_leak;
  }
  if (fx_blackout > 0.001) { c *= 1.0 - fx_blackout; }
  gl_FragColor = vec4(clamp(c, 0.0, 1.0), 1.0);
}
