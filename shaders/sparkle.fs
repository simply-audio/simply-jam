/*{
  "DESCRIPTION": "sparkle: the hackathon look's embers on their own. On every kick the figure's outline is frozen as a hot line, which then breaks up into sparkles that twinkle, drift away from the figure and cool from yellow to magenta as they die. Draws over whatever comes in, so it plays alone or on top of another look (dim darkens what's beneath). kickAmt: how fully a kick re-stamps the outline · midsAmt: how far the embers drift out · highsAmt: the hats stamp extra sparkles and quicken the twinkle.",
  "CREDIT": "Chris Poucet, from the tussen hackathon session's edge memory (tussen-composite v17–v21)",
  "CATEGORIES": ["overlay", "figure", "depth", "sound", "beat", "modes", "pressure"],
  "INPUTS": [
    { "NAME": "inputImage", "TYPE": "image" },
    { "NAME": "depth", "TYPE": "depth" },
    { "NAME": "wave", "TYPE": "audio" },
    { "NAME": "spectrum", "TYPE": "audioFFT" },
    { "NAME": "amount", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 1.0, "DESCRIPTION": "how bright the embers and the frozen line are, and how dark the room goes (1 as tuned; 0 the picture untouched)" },
    { "NAME": "react", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5, "DESCRIPTION": "how much the music moves it: 0 holds it still, 0.5 as tuned, 1 twice as much" },
    { "NAME": "palette", "TYPE": "long", "MIN": 0, "MAX": 3, "DEFAULT": 0, "VALUES": [0, 1, 2, 3], "LABELS": ["yellow to magenta", "white to cyan", "gold to red", "the picture's colours"], "DESCRIPTION": "the embers' colour, born to dying" },
    { "NAME": "line", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.6, "DESCRIPTION": "how bright the outline glows while it is still frozen, before it breaks up" },
    { "NAME": "dim", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0, "DESCRIPTION": "darkens the picture beneath (1: the embers alone on black)" },
    { "NAME": "stream", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.15, "DESCRIPTION": "embers peel off the outline all the time, not only on kicks" },
    { "NAME": "spread", "TYPE": "float", "MIN": 0.0, "MAX": 0.3, "DEFAULT": 0.06, "DESCRIPTION": "how fast the embers drift away from the figure (picture heights a second)" },
    { "NAME": "lift", "TYPE": "float", "MIN": -0.3, "MAX": 0.3, "DEFAULT": 0.03, "DESCRIPTION": "how fast they rise (negative: they fall like dust)" },
    { "NAME": "emberLife", "TYPE": "float", "MIN": 0.3, "MAX": 6.0, "DEFAULT": 2.4, "DESCRIPTION": "seconds an outline takes to burn away" },
    { "NAME": "grain", "TYPE": "float", "MIN": 60.0, "MAX": 300.0, "DEFAULT": 150.0, "DESCRIPTION": "sparkles a picture height (more = finer)" },
    { "NAME": "kickAmt", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 1.0, "DESCRIPTION": "how fully a kick re-stamps the outline" },
    { "NAME": "midsAmt", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5, "DESCRIPTION": "the mids blow the embers further out" },
    { "NAME": "highsAmt", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5, "DESCRIPTION": "the hats stamp extra sparkles and quicken the twinkle" },
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
    { "TARGET": "life", "WIDTH": "$WIDTH / 4", "HEIGHT": "$HEIGHT / 4", "FLOAT": true, "PERSISTENT": true },
    {}
  ]
}*/

// `life` (quarter size) is the edge memory: 1 where an outline was just
// stamped, burning down to 0. Above SOLID_UNTIL it draws as a frozen line;
// below, each sparkle cell survives with a chance that falls with its life,
// so the line breaks up into embers. The field drifts away from the figure
// (down the figure mask's slope) and rises; each cell has its own pace.

const float STAMP_WINDOW = 0.06;  // s after a kick during which the outline stamps
const float SOLID_UNTIL  = 0.7;
const float DOT          = 0.45;
const float TWINKLE_DUTY = 0.6;
const float FLICKER      = 0.10;
const float EDGE_T       = 0.035;

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
// from there. amount scales it all down to 0, the picture as it came in.
const float REST = 0.9;
float press() { return REST * amount; }
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
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
vec3 cam(vec2 uv) { return IMG_NORM_PIXEL(inputImage, uv).rgb; }

// the outline now: depth edges plus the figure cut
float edgeAt(vec2 uv) {
  vec2 o = 1.0 / IMG_SIZE(depthBuf);
  float gd = abs(depthAt(uv + vec2(o.x, 0.0)) - depthAt(uv - vec2(o.x, 0.0)))
           + abs(depthAt(uv + vec2(0.0, o.y)) - depthAt(uv - vec2(0.0, o.y)));
  float gf = abs(figureAt(uv + vec2(o.x, 0.0)) - figureAt(uv - vec2(o.x, 0.0)))
           + abs(figureAt(uv + vec2(0.0, o.y)) - figureAt(uv - vec2(0.0, o.y)));
  return max(smoothstep(EDGE_T, EDGE_T * 2.0, gd), smoothstep(0.25, 0.7, gf));
}
vec2 cellOf(vec2 uv) {
  float aspect = RENDERSIZE.x / max(RENDERSIZE.y, 1.0);
  return floor(uv * vec2(aspect, 1.0) * grain);
}

vec4 lifePass(vec2 uv) {
  float dt = dtNow();
  float aspect = RENDERSIZE.x / max(RENDERSIZE.y, 1.0);
  float edge = edgeAt(uv);
  float stamp = step(kickAge(), STAMP_WINDOW);
  stamp = max(stamp, step(0.5, beatSync) * step(PHASE * 60.0 / max(TEMPO, 1.0), STAMP_WINDOW));
  vec2 cell = cellOf(uv);
  float pace = 0.5 + hash(cell + 11.3);
  // away from the figure: down the slope of a wide figure mask
  vec2 o = 6.0 / IMG_SIZE(depthBuf);
  vec2 g = vec2(figureAt(uv + vec2(o.x, 0.0)) - figureAt(uv - vec2(o.x, 0.0)), figureAt(uv + vec2(0.0, o.y)) - figureAt(uv - vec2(0.0, o.y)));
  vec2 away = length(g) > 0.02 ? -normalize(g) : vec2(0.0, 1.0);
  vec2 wander = (vec2(hash(cell), hash(cell + 7.31)) - 0.5) * 2.0 * FLICKER;
  vec2 drift = (spread * (1.0 + 2.5 * midsNow() * midsAmt) * away + vec2(0.0, lift) + wander) / vec2(aspect, 1.0);
  vec2 from = uv - drift * pace * dt;
  float fadeRate = (0.6 + 0.8 * hash(cell + 13.7)) / emberLife;
  float l = max(IMG_NORM_PIXEL(life, from).r - fadeRate * dt, 0.0) * step(1.0, FRAMEINDEX);
  // a kick replaces the field with this instant's outline
  l = mix(l, edge, stamp * kickAmt * min(reactK(), 1.0));
  float t30 = floor(TIME * 30.0);
  l = max(l, edge * SOLID_UNTIL * 0.9 * step(hash(cell + t30), stream * 0.08));
  l = max(l, edge * SOLID_UNTIL * 0.9 * step(hash(cell + t30 + 3.1), highsNow() * highsAmt * 0.3));
  return vec4(l, 0.0, 0.0, 1.0);
}

vec3 emberColour(float heat, vec3 pic) {
  float p = floor(palette + 0.5);
  vec3 hot = vec3(1.0, 0.95, 0.1); vec3 cool = vec3(1.0, 0.15, 0.9);
  if (p > 0.5) { hot = vec3(1.0); cool = vec3(0.1, 0.75, 1.0); }
  if (p > 1.5) { hot = vec3(1.0, 0.85, 0.35); cool = vec3(0.9, 0.08, 0.05); }
  if (p > 2.5) {
    float l = max(max(pic.r, pic.g), max(pic.b, 0.05));
    vec3 vivid = pic / l;
    hot = mix(vivid, vec3(1.0), 0.5); cool = vivid * 0.8;
  }
  return mix(cool, hot, heat);
}

void main() {
  vec2 uv = isf_FragNormCoord;
  if (PASSINDEX == 0.0) { gl_FragColor = sigPass(); return; }
  if (PASSINDEX == 1.0) { gl_FragColor = depthTimePass(uv); return; }
  if (PASSINDEX == 2.0) { gl_FragColor = lifePass(uv); return; }

  float aspect = RENDERSIZE.x / max(RENDERSIZE.y, 1.0);
  vec3 c = cam(uv);
  float l = IMG_NORM_PIXEL(life, uv).r;
  vec2 cell = cellOf(uv);
  float dying = clamp(l / SOLID_UNTIL, 0.0, 1.0);
  float survive = step(hash(cell + 3.7), dying);
  vec2 cuv = fract(uv * vec2(aspect, 1.0) * grain) - 0.5;
  float dotMask = 1.0 - smoothstep(0.8, 1.0, length(cuv) / (DOT * (0.35 + 0.65 * dying)));
  float hz = 12.0 * (1.0 + highsNow() * highsAmt);
  float glint = step(1.0 - TWINKLE_DUTY, hash(cell + 0.173 * floor(TIME * hz)));
  float solid = smoothstep(SOLID_UNTIL, SOLID_UNTIL + 0.1, l);
  float spark = smoothstep(0.0, 0.12, l) * (1.0 - solid) * survive * dotMask * glint;
  vec3 col = emberColour(dying, c);
  vec3 base = c * (1.0 - dim * amount);
  vec3 outc = mix(base, col, clamp(press() * spark, 0.0, 1.0));
  // the frozen outline: a hot line, glowing, until it breaks up
  // (added as light, so it glows hot on dark and never turns muddy)
  // a half-bright yellow reads olive, so the line is lit at full strength and fades by its width
  float hot = clamp(press() * line * 2.0, 0.0, 1.0) * solid;
  outc = mix(outc, mix(emberColour(1.0, c), vec3(1.0), 0.25), hot);
  gl_FragColor = vec4(clamp(outc, 0.0, 1.0), 1.0);
}
