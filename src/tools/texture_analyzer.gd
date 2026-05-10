@tool
extends EditorScript

## 纹理分析工具 — 输出所有六边形纹理的裁剪尺寸和间距数据
## 在 Godot 编辑器中通过"编辑器 > 工具"运行

func _run():
	var tex_dir := "res://src/assets/tiles/hex_terrain"
	var results: Array[Dictionary] = []
	
	var dir := DirAccess.open(tex_dir)
	if not dir:
		print("[TextureAnalyzer] 无法打开目录: %s" % tex_dir)
		return
	
	dir.list_dir_begin()
	var file_name := dir.get_next()
	while file_name != "":
		if file_name.ends_with(".png"):
			var full_path := tex_dir + "/" + file_name
			var tex := load(full_path) as Texture2D
			if tex:
				var img := tex.get_image()
				var info := _analyze_texture(img, file_name)
				results.append(info)
		file_name = dir.get_next()
	
	# 输出结果
	print("\n========================================")
	print("纹理分析结果 (%d 纹理)" % results.size())
	print("========================================")
	
	# 统计
	var widths: Array[int] = []
	var heights: Array[int] = []
	for r in results:
		widths.append(r.crop_w)
		heights.append(r.crop_h)
	
	widths.sort()
	heights.sort()
	
	print("宽度范围: %d ~ %d (中位数: %d)" % [widths[0], widths[-1], widths[widths.size()/2]])
	print("高度范围: %d ~ %d (中位数: %d)" % [heights[0], heights[-1], heights[heights.size()/2]])
	print("")
	
	# 建议的拼接参数
	var avg_w := 0.0
	var avg_h := 0.0
	for r in results:
		avg_w += r.crop_w
		avg_h += r.crop_h
	avg_w /= results.size()
	avg_h /= results.size()
	
	var hex_size := avg_w / 2.0
	var col_spacing := avg_w * 0.75  # 平顶: 1.5 * half_w
	var row_spacing := avg_w * 0.866  # sqrt(3) * half_w
	
	print("平均裁剪尺寸: %.1f x %.1f" % [avg_w, avg_h])
	print("建议 HEX_SIZE: %.1f (纹理宽/2)" % hex_size)
	print("建议列间距: %.1f (宽*0.75)" % col_spacing)
	print("建议行间距: %.1f (宽*0.866)" % row_spacing)
	print("")
	
	# 详细数据
	print("详细数据:")
	print("%-25s %5s %5s %8s %8s %8s" % ["文件名", "宽", "高", "偏移X", "偏移Y", "中心X"])
	for r in results:
		print("%-25s %5d %5d %8.1f %8.1f %8.1f" % [
			r.file_name, r.crop_w, r.crop_h,
			r.offset_x, r.offset_y, r.center_x
		])
	
	print("\n========================================")
	print("将以上数据提供给开发者以调整拼接参数")
	print("========================================")


func _analyze_texture(img: Image, file_name: String) -> Dictionary:
	var w := img.get_width()
	var h := img.get_height()
	
	var min_x := w
	var max_x := 0
	var min_y := h
	var max_y := 0
	
	for y in range(h):
		for x in range(w):
			if img.get_pixel(x, y).a > 0.01:
				if x < min_x: min_x = x
				if x > max_x: max_x = x
				if y < min_y: min_y = y
				if y > max_y: max_y = y
	
	var crop_w := max_x - min_x + 1
	var crop_h := max_y - min_y + 1
	var center_x := (min_x + max_x) / 2.0
	var center_y := (min_y + max_y) / 2.0
	var offset_x := center_x - w / 2.0  # 中心相对于画布中心的偏移
	var offset_y := center_y - h / 2.0
	
	return {
		"file_name": file_name,
		"crop_w": crop_w,
		"crop_h": crop_h,
		"center_x": center_x,
		"center_y": center_y,
		"offset_x": offset_x,
		"offset_y": offset_y,
	}
