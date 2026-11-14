/*{
  "DESCRIPTION": "hearts3d: three passes around a depth buffer. 1: the picture's depth (the ML depth map when tussen has one, else blurred brightness; depthFallback blends to brightness). 2: ray-marched 3D hearts, depth-tested against it, colour + z into a float buffer. 3: depth of field from that z.",
  "CATEGORIES": ["tussen examples"],
  "INPUTS": [
    { "NAME": "inputImage", "TYPE": "image" },
    { "NAME": "depth", "TYPE": "depth" },
    { "NAME": "spectrum", "TYPE": "audioFFT" },
    { "NAME": "gain", "TYPE": "float", "MIN": 1.0, "MAX": 16.0, "DEFAULT": 5.0 },
    { "NAME": "speed", "TYPE": "float", "MIN": 0.0, "MAX": 3.0, "DEFAULT": 0.8 },
    { "NAME": "spacing", "TYPE": "float", "MIN": 1.2, "MAX": 4.0, "DEFAULT": 2.2 },
    { "NAME": "camNear", "TYPE": "float", "MIN": 1.0, "MAX": 8.0, "DEFAULT": 3.0 },
    { "NAME": "camDepth", "TYPE": "float", "MIN": 0.0, "MAX": 10.0, "DEFAULT": 4.0 },
    { "NAME": "depthInvert", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "focus", "TYPE": "float", "MIN": 1.0, "MAX": 15.0, "DEFAULT": 4.0 },
    { "NAME": "dof", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.6 },
    { "NAME": "fog", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5 },
    { "NAME": "depthFallback", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "showDepth", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 }
  ],
  "PASSES": [
    { "TARGET": "depthBuf", "WIDTH": "$WIDTH / 2", "HEIGHT": "$HEIGHT / 2", "FLOAT": true, "PERSISTENT": true },
    { "TARGET": "sceneBuf", "FLOAT": true },
    {}
  ]
}*/

float hash3(vec3 p) {
  return fract(sin(dot(p, vec3(127.1, 311.7, 74.7))) * 43758.5453);
}

float band(float lo, float hi) {
  float s = 0.0;
  for (int i = 0; i < 8; i++) {
    float x = mix(lo, hi, (float(i) + 0.5) / 8.0);
    s += IMG_NORM_PIXEL(spectrum, vec2(x, 0.25)).r;
  }
  return clamp(s / 8.0 * gain, 0.0, 1.5);
}

// 2D heart SDF, y up, point at the bottom, centred.
float heart2(vec2 p) {
  p.y += 0.5;
  p.x = abs(p.x);
  if (p.y + p.x > 1.0) {
    return sqrt(dot(p - vec2(0.25, 0.75), p - vec2(0.25, 0.75))) - sqrt(2.0) / 4.0;
  }
  vec2 a = p - vec2(0.0, 1.0);
  vec2 b = p - 0.5 * max(p.x + p.y, 0.0);
  return sqrt(min(dot(a, a), dot(b, b))) * sign(p.x - p.y);
}

vec2 rot2(vec2 p, float a) {
  float c = cos(a), s = sin(a);
  return vec2(c * p.x - s * p.y, s * p.x + c * p.y);
}

float cellId(vec3 p) {
  vec3 q = p;
  q.z -= TIME * speed * 2.0;
  return hash3(floor(q / spacing));
}

// A lattice of puffy hearts drifting toward the viewer; `bass` swells them.
float map(vec3 p, float bass) {
  vec3 q = p;
  q.z -= TIME * speed * 2.0;
  float id = hash3(floor(q / spacing));
  q = mod(q, spacing) - 0.5 * spacing;
  q.xz = rot2(q.xz, TIME * (0.6 + id) + id * 6.283);
  q.xy = rot2(q.xy, 0.3 * sin(TIME + id * 9.0));
  float s = 0.32 + 0.18 * id + 0.18 * bass;
  vec3 h = q / s;
  float d2 = heart2(h.xy);
  vec2 w = vec2(d2, abs(h.z) - 0.18);
  float d = min(max(w.x, w.y), 0.0) + length(max(w, 0.0)) - 0.12;
  return d * s * 0.8;
}

vec3 normalAt(vec3 p, float bass) {
  vec2 e = vec2(0.002, 0.0);
  return normalize(vec3(
    map(p + e.xyy, bass) - map(p - e.xyy, bass),
    map(p + e.yxy, bass) - map(p - e.yxy, bass),
    map(p + e.yyx, bass) - map(p - e.yyx, bass)));
}

float luma(vec3 c) {
  return dot(c, vec3(0.299, 0.587, 0.114));
}

void main() {
  vec2 uv = isf_FragNormCoord;

  if (PASSINDEX == 0.0) {
    // ── pass 1: the picture's depth, z in scene units, in .r ──
    vec2 px = 4.0 / RENDERSIZE;
    float l = 0.0;
    for (int y = -2; y <= 2; y++) {
      for (int x = -2; x <= 2; x++) {
        l += luma(IMG_NORM_PIXEL(inputImage, uv + vec2(float(x), float(y)) * px).rgb);
      }
    }
    l /= 25.0;
    l = mix(l, 1.0 - l, depthInvert);
    float near = smoothstep(0.1, 0.9, l);
    // the ML map: 1 = nearest, toward 0 = far (relative per frame). It
    // reads 0 everywhere with no model: its max over a 3×3 grid says
    // whether one is there, smoothed over a few frames in .b.
    float m = 0.0;
    for (int y = 0; y < 3; y++) {
      for (int x = 0; x < 3; x++) {
        m = max(m, IMG_NORM_PIXEL(depth, (vec2(float(x), float(y)) + 0.5) / 3.0).r);
      }
    }
    float ready = IMG_NORM_PIXEL(depthBuf, uv).b * step(1.0, FRAMEINDEX);
    ready += 0.2 * (step(0.02, m) - ready);
    near = mix(IMG_NORM_PIXEL(depth, uv).r, near, max(depthFallback, 1.0 - ready));
    float z = camNear + (1.0 - near) * camDepth;
    gl_FragColor = vec4(z, l, ready, 1.0);
    return;
  }

  if (PASSINDEX == 1.0) {
    // ── pass 2: ray-march the hearts, depth-test against the picture ──
    float aspect = RENDERSIZE.x / RENDERSIZE.y;
    float bass = band(0.0, 0.06);
    float highs = band(0.3, 0.9);
    float flash = exp(-5.0 * PHASE);
    vec3 cam = IMG_NORM_PIXEL(inputImage, uv).rgb;
    float camZ = IMG_NORM_PIXEL(depthBuf, uv).r;

    vec3 rd = normalize(vec3((uv.x - 0.5) * aspect, uv.y - 0.5, 1.4));
    float maxT = camZ / rd.z + 0.5;
    float t = 0.05;
    float hit = 0.0;
    for (int i = 0; i < 80; i++) {
      float d = map(rd * t, bass);
      if (d < 0.001) {
        hit = 1.0;
        break;
      }
      t += d;
      if (t > maxT) {
        break;
      }
    }
    float heartZ = mix(1000.0, t * rd.z, hit);

    vec3 col = cam;
    float z = camZ;
    if (heartZ < camZ + 0.3) {
      vec3 p = rd * t;
      vec3 n = normalAt(p, bass);
      float id = cellId(p);
      vec3 ld = normalize(vec3(-0.5, 0.8, -0.6));
      float diff = max(dot(n, ld), 0.0);
      float rim = pow(1.0 - max(dot(n, -rd), 0.0), 3.0);
      float spec = pow(max(dot(reflect(rd, n), ld), 0.0), 32.0);
      vec3 base = mix(vec3(1.0, 0.12, 0.35), vec3(1.0, 0.45, 0.7), id);
      vec3 hc = base * (0.25 + 0.85 * diff) + vec3(1.0, 0.7, 0.85) * rim * (0.6 + highs) + vec3(spec);
      hc *= 0.9 + 0.5 * flash;
      float fz = clamp((heartZ - camNear) / (camDepth + 2.0), 0.0, 1.0);
      hc = mix(hc, cam, fz * fog);
      // Where heart and picture meet in depth: a soft seam, not a hard cut.
      float seam = smoothstep(0.0, 0.3, camZ - heartZ);
      col = mix(cam, hc, seam);
      z = mix(camZ, heartZ, seam);
    }
    // A pink glow where hearts pass just behind the picture's surface.
    col += vec3(1.0, 0.2, 0.5) * 0.25 * bass * (1.0 - smoothstep(0.0, 0.6, abs(heartZ - camZ)));
    gl_FragColor = vec4(col, z);
    return;
  }

  // ── pass 3: depth of field from the depth buffer ──
  vec4 c0 = IMG_NORM_PIXEL(sceneBuf, uv);
  float coc = clamp(abs(c0.a - focus) / 6.0, 0.0, 1.0) * dof;
  vec2 r = coc * 10.0 / RENDERSIZE;
  vec3 acc = c0.rgb;
  float wsum = 1.0;
  for (int i = 0; i < 12; i++) {
    float a = float(i) * 2.39996;
    float rr = sqrt((float(i) + 0.5) / 12.0);
    vec2 o = vec2(cos(a), sin(a)) * rr * r;
    vec4 s = IMG_NORM_PIXEL(sceneBuf, uv + o);
    // Only blur from samples at least as far: sharp things don't bleed backward.
    float w = step(c0.a - 0.5, s.a);
    acc += s.rgb * w;
    wsum += w;
  }
  vec3 col = acc / wsum;

  // The depth buffer itself, for checking: near white, far black.
  vec3 dv = vec3(1.0 - clamp((c0.a - camNear) / (camDepth + 1.0), 0.0, 1.0));
  col = mix(col, dv, showDepth);
  gl_FragColor = vec4(clamp(col, 0.0, 1.0), 1.0);
}
