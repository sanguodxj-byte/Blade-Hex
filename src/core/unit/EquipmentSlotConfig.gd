# EquipmentSlotConfig.gd
# 装备部位渲染配置 — 单一真相源，与 C# ItemData.EquipSlot 的 int 值一一对应
# 不定义自己的枚举，直接用 const int 确保与 C# 永远一致
class_name EquipmentSlotConfig
extends RefCounted


## =========================================
# 部位常量 — 直接对应 C# ItemData.EquipSlot 的底层 int 值
# 改 C# 枚举顺序时必须同步改这里
## =========================================

const SLOT_BODY: int    = 0   # 身体层 — 基础身形/服装（不可换装）
const SLOT_COSTUME: int = 1   # 服装层 — 外套/斗篷/胸甲
const SLOT_HANDS: int   = 2   # 手甲层 — 手套/护腕
const SLOT_HEAD: int    = 3   # 头部层 — 头发/面部/兽头
const SLOT_HELMET: int  = 4   # 头盔层 — 帽盔/兜帽
const SLOT_WEAPON: int  = 5   # 武器层 — 武器/盾牌

const ALL_SLOTS: Array[int] = [SLOT_BODY, SLOT_COSTUME, SLOT_HANDS, SLOT_HEAD, SLOT_HELMET, SLOT_WEAPON]


## =========================================
# 部位配置数据类
## =========================================

class SlotConfig:
	var slot: int
	var anchor_offset: Vector3     # 相对于角色根节点的锚点偏移
	var z_order: int               # 渲染优先级（越大越靠前）
	var default_size: Vector2      # 默认贴图尺寸
	var pixel_size: float          # Sprite3D 像素大小
	var sort_offset: float         # z 轴额外偏移（避免 z-fighting）


## =========================================
# 配置表 — 唯一的锚点/尺寸定义
## =========================================

static var _table: Dictionary = {}

static func _static_init():
	_table = {
		SLOT_BODY:    _cfg(SLOT_BODY,    Vector3(0, 0, 0),    0, Vector2(64, 96),  1.0, 0.000),
		SLOT_COSTUME: _cfg(SLOT_COSTUME, Vector3(0, 2, 0),    1, Vector2(72, 100), 1.0, -0.01),
		SLOT_HANDS:   _cfg(SLOT_HANDS,   Vector3(0, -10, 0),  2, Vector2(48, 48),  1.0, -0.02),
		SLOT_HEAD:    _cfg(SLOT_HEAD,    Vector3(0, 48, 0),   3, Vector2(48, 48),  1.0, -0.03),
		SLOT_HELMET:  _cfg(SLOT_HELMET,  Vector3(0, 52, 0),   4, Vector2(56, 56),  1.0, -0.04),
		SLOT_WEAPON:  _cfg(SLOT_WEAPON,  Vector3(28, -5, 0),  5, Vector2(32, 80),  1.0, -0.05),
	}


static func _cfg(slot: int, anchor: Vector3, z: int, size: Vector2, pixel: float, sort: float) -> SlotConfig:
	var c := SlotConfig.new()
	c.slot = slot
	c.anchor_offset = anchor
	c.z_order = z
	c.default_size = size
	c.pixel_size = pixel
	c.sort_offset = sort
	return c


## =========================================
# 查询接口
## =========================================

## 获取指定部位的配置
static func get_config(slot: int) -> SlotConfig:
	return _table.get(slot)


## 获取所有部位配置（按 z_order 排序）
static func get_all_sorted() -> Array[SlotConfig]:
	var arr: Array[SlotConfig] = []
	arr.assign(_table.values())
	arr.sort_custom(func(a, b): return a.z_order < b.z_order)
	return arr


## 部位 → 可读名称
static func get_slot_name(slot: int) -> String:
	match slot:
		SLOT_BODY:    return "身体"
		SLOT_COSTUME: return "服装"
		SLOT_HANDS:   return "手甲"
		SLOT_HEAD:    return "头部"
		SLOT_HELMET:  return "头盔"
		SLOT_WEAPON:  return "武器"
		_:            return "未知"


## 部位是否允许换装（BODY 层不可清除）
static func is_swappable(slot: int) -> bool:
	return slot != SLOT_BODY
