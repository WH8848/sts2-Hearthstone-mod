extends SceneTree

func _init():
	var files := [
		"res://jaina/localization/zhs/cards.json",
		"res://jaina/localization/eng/cards.json",
		"res://jaina/localization/zhs/relics.json",
		"res://jaina/localization/eng/relics.json",
	]
	for p in files:
		var f := FileAccess.open(p, FileAccess.READ)
		if f == null:
			print(p, " -> FILE MISSING")
			continue
		var text := f.get_as_text()
		var parsed = JSON.parse_string(text)
		if parsed is Dictionary:
			print(p, " -> OK (", parsed.size(), " entries)")
		else:
			print(p, " -> BROKEN")
	quit()
