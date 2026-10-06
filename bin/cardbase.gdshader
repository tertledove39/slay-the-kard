shader_type canvas_item;

// 定义一个亮度系数参数，可以在 Inspector 中调节
uniform float emission_intensity : hint_range(0.0, 10.0) = 2.0;

void fragment() {
    // 获取原始纹理颜色
    vec4 tex_color = texture(TEXTURE, UV);

    // 计算一个简单的“自发光”颜色（这里使用纹理颜色乘以强度）
    // 你可以根据需要修改这个逻辑，比如使用固定颜色
    vec3 emissive_color = tex_color.rgb * emission_intensity;

    // 将原始颜色和发光颜色相加
    vec3 final_color = tex_color.rgb + emissive_color;

    // 设置最终输出颜色
    COLOR = vec4(final_color, tex_color.a); // 保留原始 Alpha
}