/*{
  "DESCRIPTION": "djaybg: you in front, djay Pro behind. The ML depth map cuts the figure (near) out of the camera and lays it over djay's picture; brightness depth stands in until the model is ready.",
  "CATEGORIES": ["tussen audiodev"],
  "INPUTS": [
    { "NAME": "cam", "TYPE": "image" },
    { "NAME": "plate", "TYPE": "image" },
    { "NAME": "depth", "TYPE": "depth" },
    { "NAME": "spectrum", "TYPE": "audioFFT" },
    { "NAME": "cut", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.55 },
    { "NAME": "soft", "TYPE": "float", "MIN": 0.01, "MAX": 0.4, "DEFAULT": 0.08 },
    { "NAME": "plateLevel", "TYPE": "float", "MIN": 0.0, "MAX": 1.5, "DEFAULT": 1.0 },
    { "NAME": "rim", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5 },
    { "NAME": "react", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.8 }
  ]
}*/

float luma(vec3 c) { return dot(c, vec3(0.299, 0.587, 0.114)); }

float bass() {
  float s = 0.0;
  for (int i = 0; i < 6; i++) {
    s += IMG_NORM_PIXEL(spectrum, vec2((float(i) + 0.5) / 6.0 * 0.06, 0.25)).r;
  }
  return clamp(s / 6.0 * 5.0, 0.0, 1.0);
}

// Nearness 0..1 at uv: the ML map when it has one (its nearest point is 1),
// else blurred brightness.
float nearAt(vec2 uv) {
  float d = IMG_NORM_PIXEL(depth, uv).r;
  float ready = step(0.5, max(max(IMG_NORM_PIXEL(depth, vec2(0.25, 0.5)).r, IMG_NORM_PIXEL(depth, vec2(0.5, 0.5)).r),
                              max(IMG_NORM_PIXEL(depth, vec2(0.75, 0.5)).r, IMG_NORM_PIXEL(depth, vec2(0.5, 0.25)).r)));
  vec2 px = 3.0 / RENDERSIZE;
  float l = (luma(IMG_NORM_PIXEL(cam, uv).rgb) + luma(IMG_NORM_PIXEL(cam, uv + vec2(px.x, 0.0)).rgb)
           + luma(IMG_NORM_PIXEL(cam, uv - vec2(px.x, 0.0)).rgb) + luma(IMG_NORM_PIXEL(cam, uv + vec2(0.0, px.y)).rgb)) * 0.25;
  return mix(l, d, ready);
}

void main() {
  vec2 uv = isf_FragNormCoord;
  vec3 me = IMG_NORM_PIXEL(cam, uv).rgb;
  vec3 bg = IMG_NORM_PIXEL(plate, uv).rgb;
  float b = bass() * react;

  float n = nearAt(uv);
  float figure = smoothstep(cut - soft, cut + soft, n);

  // A rim of light where you meet djay's picture, pulsing with the bass.
  vec2 e = 2.0 / RENDERSIZE;
  float edge = abs(nearAt(uv + vec2(e.x, 0.0)) - nearAt(uv - vec2(e.x, 0.0)))
             + abs(nearAt(uv + vec2(0.0, e.y)) - nearAt(uv - vec2(0.0, e.y)));
  float glow = smoothstep(0.02, 0.15, edge) * figure * rim * (0.5 + b);

  vec3 back = bg * plateLevel * (0.85 + 0.3 * b);
  vec3 col = mix(back, me, figure) + vec3(0.6, 0.85, 1.0) * glow;
  gl_FragColor = vec4(clamp(col, 0.0, 1.0), 1.0);
}
