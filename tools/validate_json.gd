extends SceneTree

func _init():
	# 注意：Godot 的 JSON.parse_string 宽松（容忍尾逗号），而游戏本体用
	# System.Text.Json（默认不允许尾逗号）——所以除了解析还要显式查尾逗号。
	var files := [
		"res://jaina/localization/zhs/cards.json",
		"res://jaina/localization/eng/cards.json",
		"res://jaina/localization/zhs/relics.json",
		"res://jaina/localization/eng/relics.json",
		"res://jaina/localization/zhs/powers.json",
		"res://jaina/localization/eng/powers.json",
		"res://jaina/localization/zhs/card_keywords.json",
		"res://jaina/localization/eng/card_keywords.json",
		"res://jaina/localization/zhs/monsters.json",
		"res://jaina/localization/eng/monsters.json",
		"res://jaina/localization/zhs/characters.json",
		"res://jaina/localization/eng/characters.json",
		"res://jaina/localization/zhs/gameplay_ui.json",
		"res://jaina/localization/eng/gameplay_ui.json",
	]
	for p in files:
		var f := FileAccess.open(p, FileAccess.READ)
		if f == null:
			print(p, " -> FILE MISSING")
			continue
		var text := f.get_as_text()
		var parsed = JSON.parse_string(text)
		if parsed is Dictionary:
			if text.contains(",\n}") or text.contains(", \n}"):
				print(p, " -> TRAILING COMMA (system.text.json 会报错)")
			else:
				print(p, " -> OK (", parsed.size(), " entries)")
		else:
			print(p, " -> BROKEN")
	quit()
