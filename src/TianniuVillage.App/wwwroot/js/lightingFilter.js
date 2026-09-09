"use strict";

const LightingVertex = `
attribute vec2 aVertexPosition;
attribute vec2 aTextureCoord;
uniform mat3 projectionMatrix;
varying vec2 vTextureCoord;
void main(void) {
    gl_Position = vec4((projectionMatrix * vec3(aVertexPosition, 1.0)).xy, 0.0, 1.0);
    vTextureCoord = aTextureCoord;
}
`;

const LightingFragment = `
precision mediump float;
varying vec2 vTextureCoord;
uniform sampler2D uSampler;
uniform vec3 u_ambient;
uniform vec2 u_resolution;
uniform vec2 u_cameraOffset;
uniform float u_cameraScale;
uniform float u_time;
uniform float u_lightCount;
uniform vec3 u_lights[16];
uniform vec4 u_lightColors[16];

void main(void) {
    vec4 scene = texture2D(uSampler, vTextureCoord);
    vec3 light = u_ambient;

    vec2 worldPos = (vTextureCoord * u_resolution - u_cameraOffset) / u_cameraScale;

    for (int i = 0; i < 16; i++) {
        if (float(i) >= u_lightCount) break;
        vec2 lpos = u_lights[i].xy;
        float lr = u_lights[i].z;
        float dist = distance(worldPos, lpos);
        float falloff = clamp(1.0 - dist / lr, 0.0, 1.0);
        falloff *= falloff;
        light += u_lightColors[i].rgb * falloff;
    }

    float lum = dot(light, vec3(0.299, 0.587, 0.114));
    if (lum > 0.02) {
        light *= (1.0 + sin(u_time * 7.3) * 0.03 * (1.0 - clamp(lum, 0.0, 1.0)));
    }

    light = clamp(light, vec3(0.0), vec3(1.4));
    gl_FragColor = vec4(scene.rgb * light, scene.a);
}
`;

class LightingFilter extends PIXI.Filter {
  constructor() {
    super(LightingVertex, LightingFragment, {
      u_ambient: new Float32Array([1, 1, 1]),
      u_resolution: new Float32Array([4096, 4096]),
      u_cameraOffset: new Float32Array([0, 0]),
      u_cameraScale: 1.0,
      u_time: 0.0,
      u_lightCount: 0.0,
      u_lights: new Float32Array(48),
      u_lightColors: new Float32Array(48)
    });
  }

  clearLights() {
    this.uniforms.u_lightCount = 0;
  }

  addLight(wx, wy, radius, r, g, b, intensity) {
    const i = Math.round(this.uniforms.u_lightCount);
    if (i >= 16) return;
    const off = i * 3;
    this.uniforms.u_lights[off] = wx;
    this.uniforms.u_lights[off + 1] = wy;
    this.uniforms.u_lights[off + 2] = radius;
    this.uniforms.u_lightColors[off] = r * intensity;
    this.uniforms.u_lightColors[off + 1] = g * intensity;
    this.uniforms.u_lightColors[off + 2] = b * intensity;
    this.uniforms.u_lightCount = i + 1;
  }

  setAmbient(r, g, b) {
    this.uniforms.u_ambient[0] = r;
    this.uniforms.u_ambient[1] = g;
    this.uniforms.u_ambient[2] = b;
  }

  setCamera(ox, oy, scale) {
    this.uniforms.u_cameraOffset[0] = ox;
    this.uniforms.u_cameraOffset[1] = oy;
    this.uniforms.u_cameraScale = scale;
  }

  setResolution(w, h) {
    this.uniforms.u_resolution[0] = w;
    this.uniforms.u_resolution[1] = h;
  }

  setTime(t) {
    this.uniforms.u_time = t;
  }
}
