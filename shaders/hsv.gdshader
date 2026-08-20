shader_type canvas_item;

// HSV 色调偏移着色器：对贴图进行色相/饱和度/亮度偏移（用于给原画/图标统一色调）。
// h: 色相偏移（0-1，环绕；如 0.542 为蓝色偏移）
// s: 饱和度乘数偏移（>0 增饱和，<0 减饱和）
// v: 亮度乘数偏移（>0 增亮，<0 变暗）

uniform float h : hint_range(-1.0, 1.0) = 0.0;
uniform float s : hint_range(-1.0, 1.0) = 0.0;
uniform float v : hint_range(-1.0, 1.0) = 0.0;

// RGB -> HSV
vec3 rgb2hsv(vec3 c) {
    vec4 K = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    vec4 p = mix(vec4(c.bg, K.wz), vec4(c.gb, K.xy), step(c.b, c.g));
    vec4 q = mix(vec4(p.xyw, c.r), vec4(c.r, p.yzx), step(p.x, c.r));
    float d = q.x - min(q.w, q.y);
    float e = 1.0e-10;
    return vec3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

// HSV -> RGB
vec3 hsv2rgb(vec3 c) {
    vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    vec3 p = abs(fract(c.xxx + K.xyz) * 6.0 - K.www);
    return c.z * mix(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}

void fragment() {
    vec4 tex = texture(TEXTURE, UV);
    vec3 hsv = rgb2hsv(tex.rgb);
    hsv.x = mod(hsv.x + h, 1.0);
    hsv.y = clamp(hsv.y * (1.0 + s), 0.0, 1.0);
    hsv.z = clamp(hsv.z * (1.0 + v), 0.0, 1.0);
    COLOR = vec4(hsv2rgb(hsv), tex.a);
}
