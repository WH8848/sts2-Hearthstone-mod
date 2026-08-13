extends SceneTree
func _init():
    var packed: PackedScene = load("res://assets/minion_visuals/prince_renathal.tscn")
    if packed == null:
        print("LOAD FAILED: packed scene is null")
        quit()
        return
    var state: SceneState = packed.get_state()
    var root_index := 0
    for i in range(state.get_node_count()):
        var inst = state.get_node_instance(i)
        if inst == null:
            root_index = i
            break
    # 检查根节点的 script 属性（属性位于 [GDSCENE] section，用 get_node_property_count 遍历找 script）
    var script_found := false
    for p in range(state.get_node_property_count(root_index)):
        var name: StringName = state.get_node_property_name(root_index, p)
        if str(name) == "script":
            script_found = true
            print("SCRIPT PROP FOUND on root")
    if not script_found:
        print("NO SCRIPT PROP on root node")
    quit()
