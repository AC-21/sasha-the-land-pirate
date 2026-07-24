#!/usr/bin/env python3
"""Generate and validate Sasha's first-look scout in Blender 5.1.

Run from the repository root:

  /Applications/Blender.app/Contents/MacOS/Blender --background \
    --factory-startup --python \
    ContentSource/Blender/SashaScout/generate_sasha_scout.py

Then prove the saved source and interchange artifact by reopening the blend:

  /Applications/Blender.app/Contents/MacOS/Blender --background \
    ContentSource/Blender/SashaScout/veh_sasha_scout_firstlook.blend \
    --python ContentSource/Blender/SashaScout/generate_sasha_scout.py -- \
    --validate-existing

The canonical Blender scene uses +Y forward and +Z up. Exported runtime intent is
+Z forward and +Y up; the root records that mapping explicitly. Runtime contract
positions (x, y, z) therefore map to Blender (x, z, y).
"""

from __future__ import annotations

import argparse
from array import array
import hashlib
import json
import math
from pathlib import Path
import sys
from typing import Iterable, Sequence

import bpy
from mathutils import Euler, Vector


CONTENT_ID = "veh_sasha_scout_a"
SOURCE_VERSION = "0.1.0-firstlook"
LOD0_BASE_MINIMUM_TRIANGLES = 24_000
LOD0_BASE_MAXIMUM_TRIANGLES = 28_000
ROOT_NAME = CONTENT_ID
PACKAGE_DIR = Path(__file__).resolve().parent
BLEND_PATH = PACKAGE_DIR / "veh_sasha_scout_firstlook.blend"
MANIFEST_PATH = PACKAGE_DIR / "veh_sasha_scout_firstlook.asset.json"
DERIVED_DIR = PACKAGE_DIR / "Derived" / "Quarantine"
GLB_PATH = DERIVED_DIR / "veh_sasha_scout_firstlook.glb"
VALIDATION_PATH = DERIVED_DIR / "validation_report.json"
CONTACT_SHEET_PATH = DERIVED_DIR / "veh_sasha_scout_turntable_contact.png"

MATERIAL_SPECS = {
    "MAT_IRON_CHARCOAL": {
        "base_color": (0.075, 0.087, 0.091, 1.0),
        "metallic": 0.72,
        "roughness": 0.34,
    },
    "MAT_DUST_OCHRE_ENAMEL": {
        "base_color": (0.46, 0.19, 0.075, 1.0),
        "metallic": 0.28,
        "roughness": 0.42,
    },
    "MAT_HEAVY_RUBBER": {
        "base_color": (0.018, 0.021, 0.020, 1.0),
        "metallic": 0.0,
        "roughness": 0.72,
    },
}

# Runtime (Unity) contract positions, transformed by runtime_to_blender().
CONTACT_POSITIONS = {
    "CONTACT_FL": (-1.12, 0.62, 1.55),
    "CONTACT_FR": (1.12, 0.62, 1.55),
    "CONTACT_RL": (-1.12, 0.62, -1.55),
    "CONTACT_RR": (1.12, 0.62, -1.55),
}
PIVOT_NAMES = {
    "CONTACT_FL": "STEER_FL",
    "CONTACT_FR": "STEER_FR",
    "CONTACT_RL": "HUB_RL",
    "CONTACT_RR": "HUB_RR",
}
WHEEL_NAMES = {
    "CONTACT_FL": "WHEEL_FL",
    "CONTACT_FR": "WHEEL_FR",
    "CONTACT_RL": "WHEEL_RL",
    "CONTACT_RR": "WHEEL_RR",
}
SUSPENSION_NAMES = {
    key: "SUSPENSION_" + key.removeprefix("CONTACT_")
    for key in CONTACT_POSITIONS
}
SOCKET_POSITIONS = {
    "SOCKET_UPGRADE_FRONT": (0.0, 0.62, 2.62),
    "SOCKET_UPGRADE_CARGO_01": (0.0, 1.42, -1.38),
    "SOCKET_UPGRADE_UNDERBODY": (0.0, 0.39, 0.08),
    "SOCKET_CARGO_01": (-0.47, 1.42, -1.45),
    "SOCKET_CARGO_02": (0.47, 1.42, -1.45),
    "SOCKET_TOOL_DEPLOY": (0.0, 0.82, 2.80),
    "SOCKET_DRIVER_CAMERA": (0.0, 2.08, -0.25),
    "DOOR_DRIVER": (-1.08, 1.45, 0.38),
    "SOCKET_SERVICE_ENGINE": (0.72, 1.16, 1.36),
    "SOCKET_SERVICE_REAR": (0.88, 1.25, -1.72),
}

REFERENCE_INPUTS = [
    {
        "name": "Gemini_Generated_Image_3o6cmz3o6cmz3o6c.png",
        "bytes": 7_381_431,
        "sha256": "e22fab3fb70c790546b2cdb4942404a45cca8abfe031646a7cdba6dc0d32f4cc",
        "dimensions": [2048, 2048],
    },
    {
        "name": "Gemini_Generated_Image_3txydy3txydy3txy (1).png",
        "bytes": 6_395_877,
        "sha256": "2f69a4e9818e6a2b9e336895319d16245598342772532ecf465913db635f4932",
        "dimensions": [2048, 2048],
    },
    {
        "name": "Gemini_Generated_Image_3txydy3txydy3txy.png",
        "bytes": 6_395_877,
        "sha256": "2f69a4e9818e6a2b9e336895319d16245598342772532ecf465913db635f4932",
        "dimensions": [2048, 2048],
        "note": "byte-identical duplicate",
    },
    {
        "name": "Gemini_Generated_Image_biin01biin01biin.png",
        "bytes": 6_668_182,
        "sha256": "a6bc54ae9cea7418528e797fb23e6501a75eb292a545e19c89b09bfafc422797",
        "dimensions": [2048, 2048],
    },
    {
        "name": "Gemini_Generated_Image_eoyufueoyufueoyu.png",
        "bytes": 7_828_739,
        "sha256": "77fcdf59ecbaf45420b11adb5dde11d2c7a893a8c612a593a376e682645bd975",
        "dimensions": [2816, 1536],
    },
    {
        "name": "Gemini_Generated_Image_gcdtxxgcdtxxgcdt (1).png",
        "bytes": 7_738_676,
        "sha256": "746e8a4348212665ff8e0d3bb55415dbd6e534f7f19c79f35cb51586ee4ce716",
        "dimensions": [2816, 1536],
    },
    {
        "name": "Gemini_Generated_Image_gcdtxxgcdtxxgcdt.png",
        "bytes": 7_738_676,
        "sha256": "746e8a4348212665ff8e0d3bb55415dbd6e534f7f19c79f35cb51586ee4ce716",
        "dimensions": [2816, 1536],
        "note": "byte-identical duplicate",
    },
    {
        "name": "Gemini_Generated_Image_h4m7ywh4m7ywh4m7.png",
        "bytes": 7_108_706,
        "sha256": "7e46693f4494f856ac784cb25b8899d3f56cdee3be7526a161ed6f5bff510f1f",
        "dimensions": [2816, 1536],
    },
    {
        "name": "Gemini_Generated_Image_mus84umus84umus8.png",
        "bytes": 8_008_276,
        "sha256": "30d4ae2cacb8d268858b33bece989198f9b5894c4031f7b0b46f2a294c3d54d9",
        "dimensions": [2816, 1536],
    },
    {
        "name": "Gemini_Generated_Image_tnk88atnk88atnk8.png",
        "bytes": 7_216_752,
        "sha256": "4f829feae13d33c9c619d425e38143095c21a3344fbaf4ecf8cbed66a976e5ab",
        "dimensions": [2816, 1536],
    },
]

ZIP_AUDIT = [
    {
        "name": "eccentric custom off-road vehicle 3d model.zip",
        "bytes": 39_576_775,
        "sha256": "f0c74b347b9b148532cd1ddb767fafe9dbcacbf1a6a4071dc2bbb354cdea11b2",
        "member": "eccentric+custom+off-road+vehicle+3d+model.fbx",
        "member_bytes": 41_251_696,
    },
    {
        "name": "military logistics vehicle 3d model.zip",
        "bytes": 40_302_127,
        "sha256": "0a4664c990e6c21aa95afc4b4aa86d76c5e6ba9c03946406dc6f40e9016b9bef",
        "member": "military+logistics+vehicle+3d+model.fbx",
        "member_bytes": 41_980_576,
    },
    {
        "name": "post-apocalyptic military vehicle 3d model.zip",
        "bytes": 38_254_096,
        "sha256": "fb085783d20860dc7b6a540e03ccdcde7b1013ffe1208e69aa0cf99eec9c38b9",
        "member": "post-apocalyptic+military+vehicle+3d+model.fbx",
        "member_bytes": 39_919_536,
    },
    {
        "name": "post-apocalyptic vehicle 3d model-2.zip",
        "bytes": 2_044_688,
        "sha256": "a238be54d6c9d31ae36478bbc79a20dc098481e39ccc27623885a114a762fe94",
        "member": "post-apocalyptic+vehicle+3d+model.fbx",
        "member_bytes": 2_190_608,
    },
    {
        "name": "post-apocalyptic vehicle 3d model.zip",
        "bytes": 2_044_688,
        "sha256": "d45adb8db6fb217111652ac6c6a42a7a0ee9684512dc708b30b033e33cbb39e0",
        "member": "post-apocalyptic+vehicle+3d+model.fbx",
        "member_bytes": 2_190_608,
    },
    {
        "name": "rugged post-apocalyptic buggy 3d model.zip",
        "bytes": 40_423_786,
        "sha256": "a614c69ecc35a98f2f68c7824992b4c09f19bbd12b0837173ccb7f5c6558e6d8",
        "member": "rugged+post-apocalyptic+buggy+3d+model.fbx",
        "member_bytes": 42_110_400,
    },
    {
        "name": "steampunk junkyard vehicle 3d model.zip",
        "bytes": 3_858_374,
        "sha256": "a53ba5e86beaec50abda3c228c8c75105dfeceaeaad934de625dc89f72cd112e",
        "member": "steampunk+junkyard+vehicle+3d+model.fbx",
        "member_bytes": 4_067_984,
    },
]


def runtime_to_blender(position: Sequence[float]) -> tuple[float, float, float]:
    """Map Unity-style (x, up-y, forward-z) to Blender (x, forward-y, up-z)."""
    return (position[0], position[2], position[1])


def parse_args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--validate-existing", action="store_true")
    return parser.parse_args(argv)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (
        bpy.data.meshes,
        bpy.data.curves,
        bpy.data.materials,
        bpy.data.cameras,
        bpy.data.lights,
    ):
        for datablock in list(datablocks):
            datablocks.remove(datablock)


def configure_scene() -> None:
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.length_unit = "METERS"
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 640
    scene.render.resolution_y = 480
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.render.image_settings.color_mode = "RGBA"
    scene.world.color = (0.025, 0.032, 0.038)
    scene.view_settings.look = "AgX - Medium High Contrast"


def create_materials() -> dict[str, bpy.types.Material]:
    materials: dict[str, bpy.types.Material] = {}
    for name, spec in MATERIAL_SPECS.items():
        material = bpy.data.materials.new(name)
        material.use_nodes = True
        principled = material.node_tree.nodes.get("Principled BSDF")
        principled.inputs["Base Color"].default_value = spec["base_color"]
        principled.inputs["Metallic"].default_value = spec["metallic"]
        principled.inputs["Roughness"].default_value = spec["roughness"]
        materials[name] = material
    return materials


def set_parent(child: bpy.types.Object, parent: bpy.types.Object) -> None:
    child.parent = parent
    child.matrix_parent_inverse.identity()


def create_empty(
    name: str,
    parent: bpy.types.Object | None = None,
    location: Sequence[float] = (0.0, 0.0, 0.0),
    display_type: str = "PLAIN_AXES",
    display_size: float = 0.2,
) -> bpy.types.Object:
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    obj.empty_display_type = display_type
    obj.empty_display_size = display_size
    obj.location = location
    if parent is not None:
        set_parent(obj, parent)
    return obj


def tag_render(obj: bpy.types.Object) -> bpy.types.Object:
    obj["asset_role"] = "render"
    return obj


def assign_material(
    obj: bpy.types.Object,
    material: bpy.types.Material,
) -> None:
    obj.data.materials.clear()
    obj.data.materials.append(material)


def apply_bevel(
    obj: bpy.types.Object,
    width: float,
    segments: int = 3,
) -> None:
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if width > 0.0:
        modifier = obj.modifiers.new("Causal edge radius", "BEVEL")
        modifier.width = width
        modifier.segments = segments
        modifier.limit_method = "ANGLE"
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.select_set(False)


def create_box(
    name: str,
    parent: bpy.types.Object,
    location: Sequence[float],
    dimensions: Sequence[float],
    material: bpy.types.Material,
    bevel: float = 0.06,
    rotation: Sequence[float] = (0.0, 0.0, 0.0),
    bevel_segments: int = 3,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    set_parent(obj, parent)
    assign_material(obj, material)
    apply_bevel(obj, min(bevel, min(dimensions) * 0.24), bevel_segments)
    return tag_render(obj)


def create_cylinder(
    name: str,
    parent: bpy.types.Object,
    location: Sequence[float],
    radius: float,
    depth: float,
    material: bpy.types.Material,
    vertices: int = 32,
    rotation: Sequence[float] = (0.0, 0.0, 0.0),
    bevel: float = 0.02,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=depth,
        end_fill_type="NGON",
        location=location,
        rotation=rotation,
    )
    obj = bpy.context.object
    obj.name = name
    set_parent(obj, parent)
    assign_material(obj, material)
    apply_bevel(obj, bevel, 2)
    return tag_render(obj)


def create_tube(
    name: str,
    parent: bpy.types.Object,
    start: Sequence[float],
    end: Sequence[float],
    radius: float,
    material: bpy.types.Material,
    vertices: int = 20,
) -> bpy.types.Object:
    start_vec = Vector(start)
    end_vec = Vector(end)
    direction = end_vec - start_vec
    midpoint = (start_vec + end_vec) * 0.5
    obj = create_cylinder(
        name,
        parent,
        midpoint,
        radius,
        direction.length,
        material,
        vertices=vertices,
        bevel=radius * 0.25,
    )
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = direction.to_track_quat("Z", "Y")
    return obj


def create_torus(
    name: str,
    parent: bpy.types.Object,
    material: bpy.types.Material,
    major_radius: float,
    minor_radius: float,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_torus_add(
        align="WORLD",
        major_segments=48,
        minor_segments=16,
        location=(0.0, 0.0, 0.0),
        rotation=(0.0, math.pi / 2.0, 0.0),
        major_radius=major_radius,
        minor_radius=minor_radius,
    )
    obj = bpy.context.object
    obj.name = name
    set_parent(obj, parent)
    assign_material(obj, material)
    return tag_render(obj)


def create_wedge(
    name: str,
    parent: bpy.types.Object,
    center: Sequence[float],
    dimensions: Sequence[float],
    material: bpy.types.Material,
    front_top_drop: float,
    bevel: float = 0.05,
) -> bpy.types.Object:
    dx, dy, dz = [value * 0.5 for value in dimensions]
    vertices = [
        (-dx, -dy, -dz),
        (dx, -dy, -dz),
        (dx, dy, -dz),
        (-dx, dy, -dz),
        (-dx, -dy, dz),
        (dx, -dy, dz),
        (dx, dy, dz - front_top_drop),
        (-dx, dy, dz - front_top_drop),
    ]
    faces = [
        (0, 1, 2, 3),
        (4, 7, 6, 5),
        (0, 4, 5, 1),
        (1, 5, 6, 2),
        (2, 6, 7, 3),
        (4, 0, 3, 7),
    ]
    mesh = bpy.data.meshes.new(name + "_MESH")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = center
    set_parent(obj, parent)
    assign_material(obj, material)
    apply_bevel(obj, bevel, 3)
    return tag_render(obj)


def build_wheel(
    key: str,
    axle: bpy.types.Object,
    contacts: bpy.types.Object,
    materials: dict[str, bpy.types.Material],
) -> None:
    runtime_position = CONTACT_POSITIONS[key]
    blender_position = runtime_to_blender(runtime_position)
    suffix = key.removeprefix("CONTACT_")

    create_empty(key, contacts, blender_position, "SPHERE", 0.09)
    pivot = create_empty(
        PIVOT_NAMES[key],
        axle,
        blender_position,
        "ARROWS" if suffix.startswith("F") else "CIRCLE",
        0.16,
    )
    pivot["runtime_kind"] = "steering" if suffix.startswith("F") else "hub"
    suspension = create_empty(
        SUSPENSION_NAMES[key],
        pivot,
        (0.0, 0.0, 0.0),
        "CUBE",
        0.12,
    )
    suspension["runtime_kind"] = "suspension"

    tire = create_torus(
        WHEEL_NAMES[key],
        suspension,
        materials["MAT_HEAVY_RUBBER"],
        major_radius=0.42,
        minor_radius=0.20,
    )
    tire["runtime_kind"] = "wheel"
    create_cylinder(
        "RIM_" + suffix,
        suspension,
        (0.0, 0.0, 0.0),
        0.28,
        0.34,
        materials["MAT_IRON_CHARCOAL"],
        vertices=32,
        rotation=(0.0, math.pi / 2.0, 0.0),
        bevel=0.025,
    )
    create_cylinder(
        "HUBCAP_" + suffix,
        suspension,
        ((-0.19 if suffix.endswith("L") else 0.19), 0.0, 0.0),
        0.13,
        0.05,
        materials["MAT_DUST_OCHRE_ENAMEL"],
        vertices=24,
        rotation=(0.0, math.pi / 2.0, 0.0),
        bevel=0.012,
    )

    # Chunky tread blocks are deliberate silhouette and traction geometry.
    for tread_index in range(24):
        angle = (2.0 * math.pi * tread_index) / 24.0
        tread = create_box(
            f"TREAD_{suffix}_{tread_index:02d}",
            suspension,
            (
                0.0,
                math.sin(angle) * 0.59,
                math.cos(angle) * 0.59,
            ),
            (0.48, 0.20, 0.14),
            materials["MAT_HEAVY_RUBBER"],
            bevel=0.025,
            rotation=(angle, 0.0, 0.0),
            bevel_segments=2,
        )
        tread["runtime_kind"] = "wheel_detail"


def build_vehicle(
    materials: dict[str, bpy.types.Material],
) -> bpy.types.Object:
    root = create_empty(ROOT_NAME, None, (0.0, 0.0, 0.0), "ARROWS", 0.4)
    root["content_id"] = CONTENT_ID
    root["source_version"] = SOURCE_VERSION
    root["stage"] = "C1_FIRST_LOOK_PROTOTYPE"
    root["units"] = "metres"
    root["blender_forward_axis"] = "+Y"
    root["blender_up_axis"] = "+Z"
    root["runtime_forward_axis"] = "+Z"
    root["runtime_up_axis"] = "+Y"
    root["pivot_contract"] = "ground center between axles"

    geometry = create_empty("GEO", root)
    lod0 = create_empty("LOD0_FIRSTLOOK", geometry)
    create_empty("LOD1_RESERVED", geometry)
    create_empty("LOD2_RESERVED", geometry)
    rig = create_empty("RIG", root)
    contacts = create_empty("CONTACTS", rig)
    front_axle = create_empty("AXLE_FRONT", rig)
    rear_axle = create_empty("AXLE_REAR", rig)
    sockets = create_empty("SOCKETS", root)
    modules = create_empty("MODULES", root)
    collision = create_empty("COLLISION", root)
    calibration = create_empty(
        "FORWARD_CALIBRATION_POSITIVE_Y",
        root,
        (0.0, 3.1, 0.0),
        "SINGLE_ARROW",
        0.45,
    )
    calibration.rotation_euler = (math.pi / 2.0, 0.0, 0.0)

    iron = materials["MAT_IRON_CHARCOAL"]
    enamel = materials["MAT_DUST_OCHRE_ENAMEL"]
    rubber = materials["MAT_HEAVY_RUBBER"]

    # Broad field-service chassis: low masses, visible rails, useful wheel space.
    create_box("BELLY_PAN", lod0, (0.0, 0.0, 0.55), (2.18, 4.20, 0.28), iron, 0.08)
    create_box("FRAME_RAIL_L", lod0, (-0.79, 0.0, 0.66), (0.20, 4.62, 0.36), iron, 0.07)
    create_box("FRAME_RAIL_R", lod0, (0.79, 0.0, 0.66), (0.20, 4.62, 0.36), iron, 0.07)
    for index, y_position in enumerate((-1.65, -0.55, 0.60, 1.72)):
        create_box(
            f"FRAME_CROSSMEMBER_{index + 1:02d}",
            lod0,
            (0.0, y_position, 0.68),
            (1.72, 0.18, 0.25),
            iron,
            0.05,
        )

    create_box("CAB_LOWER", lod0, (0.0, 0.35, 1.12), (2.02, 1.84, 0.88), iron, 0.10)
    create_wedge(
        "CAB_TUMBLEHOME",
        lod0,
        (0.0, 0.28, 1.78),
        (1.88, 1.56, 0.80),
        iron,
        front_top_drop=0.18,
        bevel=0.09,
    )
    create_box("ROOF_SHADE", lod0, (0.0, 0.16, 2.24), (2.20, 1.68, 0.13), enamel, 0.055)
    create_wedge(
        "ENGINE_COWL",
        lod0,
        (0.0, 1.56, 1.18),
        (1.90, 1.25, 0.45),
        iron,
        front_top_drop=0.14,
        bevel=0.07,
    )
    create_box(
        "HAND_REPAIRED_HOOD_PANEL",
        lod0,
        (-0.18, 1.62, 1.43),
        (1.18, 0.78, 0.075),
        enamel,
        0.025,
        rotation=(math.radians(-5.0), 0.0, math.radians(2.5)),
    )
    create_box("WINDSCREEN", lod0, (0.0, 1.02, 1.86), (1.55, 0.055, 0.52), rubber, 0.025)
    create_box("WINDOW_DRIVER", lod0, (-0.96, 0.30, 1.79), (0.055, 0.72, 0.47), rubber, 0.02)
    create_box("WINDOW_NAVIGATOR", lod0, (0.96, 0.30, 1.79), (0.055, 0.72, 0.47), rubber, 0.02)
    create_box(
        "DRIVER_SERVICE_PATCH",
        lod0,
        (-1.04, 0.40, 1.21),
        (0.07, 0.58, 0.48),
        enamel,
        0.025,
        rotation=(0.0, math.radians(-2.0), 0.0),
    )
    create_box("CAB_REAR_BULKHEAD", lod0, (0.0, -0.55, 1.56), (1.92, 0.16, 1.20), iron, 0.06)

    # A clean utility bed and two physical cargo shapes remain legible in city view.
    create_box("UTILITY_BED_FLOOR", lod0, (0.0, -1.45, 1.02), (2.10, 1.58, 0.18), iron, 0.05)
    for x_value in (-0.99, 0.99):
        create_tube(
            "BED_RAIL_L" if x_value < 0.0 else "BED_RAIL_R",
            lod0,
            (x_value, -2.10, 1.18),
            (x_value, -0.78, 1.18),
            0.055,
            enamel,
        )
        create_tube(
            "BED_STANCHION_A_L" if x_value < 0.0 else "BED_STANCHION_A_R",
            lod0,
            (x_value, -2.10, 1.02),
            (x_value, -2.10, 1.52),
            0.055,
            enamel,
        )
        create_tube(
            "BED_STANCHION_B_L" if x_value < 0.0 else "BED_STANCHION_B_R",
            lod0,
            (x_value, -0.78, 1.02),
            (x_value, -0.78, 1.52),
            0.055,
            enamel,
        )
    create_box("CARGO_CRATE_LEFT", lod0, (-0.47, -1.46, 1.35), (0.74, 0.94, 0.55), enamel, 0.07)
    create_box("CARGO_CRATE_RIGHT", lod0, (0.47, -1.46, 1.30), (0.66, 0.84, 0.45), iron, 0.06)
    for index, x_value in enumerate((-0.47, 0.47)):
        create_tube(
            f"CARGO_RETENTION_STRAP_{index + 1:02d}",
            lod0,
            (x_value, -1.92, 1.63),
            (x_value, -1.00, 1.63),
            0.025,
            rubber,
            vertices=12,
        )

    # The recovery proscenium is Sasha's unmistakable tool silhouette: a compact
    # service yoke, not a weapon, crane copy, or decorative spike array.
    create_box("FRONT_RECOVERY_RAM", lod0, (0.0, 2.46, 0.62), (2.62, 0.26, 0.32), enamel, 0.07)
    create_tube("RECOVERY_YOKE_L", lod0, (-0.92, 2.44, 0.68), (-0.58, 2.28, 1.55), 0.085, iron)
    create_tube("RECOVERY_YOKE_R", lod0, (0.92, 2.44, 0.68), (0.58, 2.28, 1.55), 0.085, iron)
    create_tube("RECOVERY_YOKE_BRIDGE", lod0, (-0.58, 2.28, 1.55), (0.58, 2.28, 1.55), 0.085, iron)
    create_cylinder(
        "RECOVERY_CABLE_DRUM",
        lod0,
        (0.0, 2.30, 1.24),
        0.22,
        0.78,
        iron,
        vertices=32,
        rotation=(0.0, math.pi / 2.0, 0.0),
        bevel=0.025,
    )
    create_cylinder(
        "RECOVERY_DRUM_BAND_L",
        lod0,
        (-0.31, 2.30, 1.24),
        0.25,
        0.08,
        enamel,
        vertices=24,
        rotation=(0.0, math.pi / 2.0, 0.0),
        bevel=0.012,
    )
    create_cylinder(
        "RECOVERY_DRUM_BAND_R",
        lod0,
        (0.31, 2.30, 1.24),
        0.25,
        0.08,
        enamel,
        vertices=24,
        rotation=(0.0, math.pi / 2.0, 0.0),
        bevel=0.012,
    )

    # Functional fenders, steps, work lamp brows, and a causal exhaust/service side.
    for suffix, x_value, y_value in (
        ("FL", -1.13, 1.55),
        ("FR", 1.13, 1.55),
        ("RL", -1.13, -1.55),
        ("RR", 1.13, -1.55),
    ):
        create_box(
            "FIELD_FENDER_" + suffix,
            lod0,
            (x_value, y_value, 0.92),
            (0.27, 1.06, 0.18),
            enamel if suffix in {"FL", "RR"} else iron,
            0.055,
        )
        create_tube(
            "SUSPENSION_STRUT_" + suffix,
            lod0,
            (x_value * 0.77, y_value, 0.76),
            (x_value * 0.88, y_value, 1.21),
            0.045,
            enamel,
            vertices=16,
        )
    create_box("STEP_DRIVER", lod0, (-1.20, 0.15, 0.78), (0.28, 1.18, 0.12), iron, 0.035)
    create_box("STEP_NAVIGATOR", lod0, (1.20, 0.15, 0.78), (0.28, 1.18, 0.12), iron, 0.035)
    create_tube("EXHAUST_RISER", lod0, (0.88, -0.58, 0.90), (0.88, -0.58, 2.20), 0.07, iron)
    create_cylinder("EXHAUST_CAP", lod0, (0.88, -0.58, 2.24), 0.11, 0.12, enamel, vertices=20)

    for x_value in (-0.64, 0.64):
        create_cylinder(
            "WORK_LAMP_L" if x_value < 0.0 else "WORK_LAMP_R",
            lod0,
            (x_value, 2.03, 1.38),
            0.15,
            0.10,
            enamel,
            vertices=24,
            rotation=(math.pi / 2.0, 0.0, 0.0),
            bevel=0.02,
        )
        create_box(
            "LAMP_BROW_L" if x_value < 0.0 else "LAMP_BROW_R",
            lod0,
            (x_value, 2.01, 1.54),
            (0.42, 0.18, 0.09),
            iron,
            0.025,
        )

    # A compact service gauge gives the vehicle affection without becoming a face.
    create_cylinder(
        "SCOUT_CONDITION_GAUGE",
        lod0,
        (0.0, 1.07, 2.28),
        0.12,
        0.055,
        enamel,
        vertices=32,
        rotation=(math.pi / 2.0, 0.0, 0.0),
        bevel=0.012,
    )

    for key in CONTACT_POSITIONS:
        axle = front_axle if key in {"CONTACT_FL", "CONTACT_FR"} else rear_axle
        build_wheel(key, axle, contacts, materials)

    for socket_name, runtime_position in SOCKET_POSITIONS.items():
        socket = create_empty(
            socket_name,
            sockets if socket_name.startswith("SOCKET_") else rig,
            runtime_to_blender(runtime_position),
            "ARROWS",
            0.12,
        )
        socket["runtime_position"] = list(runtime_position)

    # Stable module roots exist without declaring production module art complete.
    winch_module = create_empty("MODULE_WINCH_ASSEMBLY", modules)
    winch_module.location = runtime_to_blender(SOCKET_POSITIONS["SOCKET_UPGRADE_FRONT"])
    range_module = create_empty("MODULE_SEALED_RANGE_TANK", modules)
    range_module.location = runtime_to_blender(SOCKET_POSITIONS["SOCKET_UPGRADE_CARGO_01"])
    skid_upgrade = create_empty("UPGRADE_PATCHWORK_SKID_PLATE", modules)
    skid_upgrade.location = runtime_to_blender(SOCKET_POSITIONS["SOCKET_UPGRADE_UNDERBODY"])

    # Simple, render-disabled collision proxy. It is intentionally not derived
    # from decorative meshes and remains cheap to replace in Unity.
    for name, location, dimensions in (
        ("COL_CHASSIS", (0.0, 0.0, 0.60), (2.10, 4.10, 0.52)),
        ("COL_CAB", (0.0, 0.35, 1.50), (1.96, 1.72, 1.48)),
        ("COL_BED", (0.0, -1.45, 1.18), (2.04, 1.50, 0.68)),
        ("COL_RECOVERY_YOKE", (0.0, 2.36, 1.10), (2.48, 0.32, 1.10)),
    ):
        obj = create_box(name, collision, location, dimensions, iron, bevel=0.0)
        obj["asset_role"] = "collision"
        obj.display_type = "WIRE"
        obj.hide_render = True

    return root


def descendants(root: bpy.types.Object) -> list[bpy.types.Object]:
    result: list[bpy.types.Object] = []
    stack = list(root.children)
    while stack:
        current = stack.pop()
        result.append(current)
        stack.extend(current.children)
    return result


def count_triangles(objects: Iterable[bpy.types.Object]) -> int:
    triangles = 0
    for obj in objects:
        if obj.type != "MESH":
            continue
        triangles += sum(max(0, len(poly.vertices) - 2) for poly in obj.data.polygons)
    return triangles


def create_review_scene(
    materials: dict[str, bpy.types.Material],
) -> tuple[bpy.types.Object, bpy.types.Object]:
    review_root = create_empty("REVIEW_ONLY")
    ground = create_box(
        "REVIEW_GROUND",
        review_root,
        (0.0, 0.0, -0.08),
        (18.0, 18.0, 0.12),
        materials["MAT_IRON_CHARCOAL"],
        bevel=0.0,
    )
    ground["asset_role"] = "review"

    bpy.ops.object.light_add(type="AREA", location=(4.5, 5.5, 8.0))
    key = bpy.context.object
    key.name = "REVIEW_KEY_TUNGSTEN"
    key.data.energy = 1050.0
    key.data.color = (1.0, 0.52, 0.24)
    key.data.shape = "DISK"
    key.data.size = 5.0
    set_parent(key, review_root)

    bpy.ops.object.light_add(type="AREA", location=(-5.0, -3.5, 5.0))
    fill = bpy.context.object
    fill.name = "REVIEW_FILL_SKY"
    fill.data.energy = 850.0
    fill.data.color = (0.30, 0.48, 0.68)
    fill.data.size = 5.0
    set_parent(fill, review_root)

    bpy.ops.object.light_add(type="AREA", location=(0.0, -4.0, 2.5))
    rim = bpy.context.object
    rim.name = "REVIEW_RIM"
    rim.data.energy = 700.0
    rim.data.color = (1.0, 0.78, 0.48)
    rim.data.size = 3.0
    set_parent(rim, review_root)

    bpy.ops.object.camera_add(location=(7.3, 7.8, 4.8))
    camera = bpy.context.object
    camera.name = "CAM_REVIEW_TURNTABLE"
    camera.data.lens = 52.0
    camera.data.sensor_width = 36.0
    set_parent(camera, review_root)
    bpy.context.scene.camera = camera
    point_camera(camera, (0.0, 0.0, 1.10))
    return review_root, camera


def point_camera(camera: bpy.types.Object, target: Sequence[float]) -> None:
    direction = Vector(target) - camera.location
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def render_turntable(camera: bpy.types.Object) -> list[Path]:
    DERIVED_DIR.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    paths: list[Path] = []
    angles = [25.0, 85.0, 145.0, 205.0, 265.0, 325.0]
    for index, angle_degrees in enumerate(angles):
        angle = math.radians(angle_degrees)
        radius = 7.7
        camera.location = (
            math.sin(angle) * radius,
            math.cos(angle) * radius,
            4.6,
        )
        point_camera(camera, (0.0, 0.0, 1.08))
        path = DERIVED_DIR / f"turntable_{index:02d}_{int(angle_degrees):03d}deg.png"
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        paths.append(path)

    # Two decision views exercise the intended camera reads directly.
    for name, location, target, lens in (
        ("strategy_read", (6.6, 6.4, 7.0), (0.0, 0.0, 0.90), 58.0),
        ("garage_read", (4.8, 6.6, 2.75), (0.0, 0.35, 1.15), 54.0),
    ):
        camera.location = location
        camera.data.lens = lens
        point_camera(camera, target)
        path = DERIVED_DIR / f"{name}.png"
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        paths.append(path)

    create_contact_sheet(paths[:6], CONTACT_SHEET_PATH, columns=3)
    return paths


def create_contact_sheet(
    paths: Sequence[Path],
    output_path: Path,
    columns: int,
) -> None:
    images = [bpy.data.images.load(str(path), check_existing=False) for path in paths]
    width, height = images[0].size
    rows = math.ceil(len(images) / columns)
    sheet_width = width * columns
    sheet_height = height * rows
    sheet_pixels = array("f", [0.0]) * (sheet_width * sheet_height * 4)

    for index, image in enumerate(images):
        if tuple(image.size) != (width, height):
            raise RuntimeError("turntable frame dimensions drifted")
        source_pixels = array("f", [0.0]) * (width * height * 4)
        image.pixels.foreach_get(source_pixels)
        column = index % columns
        row = rows - 1 - (index // columns)
        for source_y in range(height):
            source_start = source_y * width * 4
            target_start = (
                ((row * height + source_y) * sheet_width) + column * width
            ) * 4
            sheet_pixels[target_start : target_start + width * 4] = (
                source_pixels[source_start : source_start + width * 4]
            )

    sheet = bpy.data.images.new(
        "SASHA_SCOUT_TURNTABLE_CONTACT",
        width=sheet_width,
        height=sheet_height,
        alpha=True,
    )
    sheet.pixels.foreach_set(sheet_pixels)
    sheet.filepath_raw = str(output_path)
    sheet.file_format = "PNG"
    sheet.save()
    for image in images:
        bpy.data.images.remove(image)
    bpy.data.images.remove(sheet)


def export_glb(root: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    export_objects = [root] + descendants(root)
    for obj in export_objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.gltf(
        filepath=str(GLB_PATH),
        check_existing=False,
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_yup=True,
        export_extras=True,
        export_cameras=False,
        export_lights=False,
    )
    bpy.ops.object.select_all(action="DESELECT")


def relative_output_record(path: Path) -> dict[str, object]:
    return {
        "path": path.relative_to(PACKAGE_DIR).as_posix(),
        "bytes": path.stat().st_size,
        "sha256": sha256(path),
    }


def collect_counts(root: bpy.types.Object) -> dict[str, object]:
    objects = [root] + descendants(root)
    render_meshes = [
        obj for obj in objects
        if obj.type == "MESH" and obj.get("asset_role") == "render"
    ]
    collision_meshes = [
        obj for obj in objects
        if obj.type == "MESH" and obj.get("asset_role") == "collision"
    ]
    material_names = sorted(
        {
            material.name
            for obj in render_meshes
            for material in obj.data.materials
            if material is not None
        }
    )
    socket_names = sorted(
        obj.name for obj in objects if obj.name.startswith("SOCKET_")
    )
    return {
        "render_mesh_objects": len(render_meshes),
        "render_triangles": count_triangles(render_meshes),
        "collision_mesh_objects": len(collision_meshes),
        "collision_triangles": count_triangles(collision_meshes),
        "material_count": len(material_names),
        "materials": material_names,
        "socket_count": len(socket_names),
        "sockets": socket_names,
        "wheel_count": sum(
            1 for obj in objects if obj.get("runtime_kind") == "wheel"
        ),
    }


def write_manifest(
    counts: dict[str, object],
    render_paths: Sequence[Path],
) -> None:
    outputs = [
        relative_output_record(BLEND_PATH),
        relative_output_record(GLB_PATH),
        relative_output_record(CONTACT_SHEET_PATH),
    ]
    outputs.extend(relative_output_record(path) for path in render_paths)
    manifest = {
        "schema": "atomic-land-pirate.asset-first-look.v1",
        "content_id": CONTENT_ID,
        "source_version": SOURCE_VERSION,
        "status": "prototype-only",
        "canonical_source": BLEND_PATH.name,
        "tool": {
            "name": "Blender",
            "version": bpy.app.version_string,
            "build_hash": bpy.app.build_hash.decode("utf-8")
            if isinstance(bpy.app.build_hash, bytes)
            else str(bpy.app.build_hash),
            "generator": Path(__file__).name,
            "generator_sha256": sha256(Path(__file__)),
        },
        "axis_and_scale": {
            "units": "1 Blender unit = 1 metre",
            "pivot": "ground center between axles at origin",
            "blender_forward": "+Y",
            "blender_up": "+Z",
            "runtime_forward": "+Z",
            "runtime_up": "+Y",
            "mapping": "runtime (x,y,z) -> Blender (x,z,y)",
        },
        "design_intent": {
            "silhouette": "broad compact scout with a recovery proscenium",
            "function": "field service, cargo return, and recoverable roadside work",
            "history": "one hand-repaired ochre hood panel and maintained mixed ironwork",
            "strategy_read": "wheel contact, cargo bed, and recovery yoke remain separate masses",
            "originality": (
                "Authored from primitives and code. No reference pixels, marks, "
                "mesh topology, weapons, spikes, skulls, flags, or recognizable "
                "third-party vehicle expression were imported."
            ),
        },
        "counts": counts,
        "reference_provenance": {
            "source": "user-provided /Users/sasha/Downloads/vehicles",
            "use": "read-only directional inspiration; no package content imported",
            "license_status": (
                "Not asserted here. Reference files are excluded from shipping "
                "content and remain outside the repository."
            ),
            "images": REFERENCE_INPUTS,
            "zip_metadata_only": ZIP_AUDIT,
        },
        "outputs": outputs,
        "turntable_order_degrees": [25, 85, 145, 205, 265, 325],
        "hard_cuts": [
            "No Unity runtime, scene, package, project-setting, schema, or balance changes.",
            "No source texture set, UV polish, baked maps, decals, or final wear pass.",
            "No LOD1 or LOD2 production geometry.",
            "No production suspension rig, wheel deformation, animation, or physics tuning.",
            "No final winch or sealed-range-tank module art.",
            "No Tripo FBX or zip member was extracted or imported.",
            "No creator acceptance, Unity import acceptance, or ship-cleared claim.",
        ],
    }
    MANIFEST_PATH.write_text(
        json.dumps(manifest, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )


def assert_vector(
    label: str,
    observed: Vector,
    expected: Sequence[float],
    tolerance: float = 0.0001,
) -> None:
    expected_vector = Vector(expected)
    if (observed - expected_vector).length > tolerance:
        raise AssertionError(
            f"{label}: expected {tuple(expected_vector)}, observed {tuple(observed)}"
        )


def assert_identity_rotation(label: str, obj: bpy.types.Object) -> None:
    identity = Euler((0.0, 0.0, 0.0)).to_quaternion()
    observed = obj.rotation_euler.to_quaternion()
    if observed.rotation_difference(identity).angle > 0.0001:
        raise AssertionError(f"{label}: expected identity local rotation")


def assert_parent(
    label: str,
    obj: bpy.types.Object,
    expected_parent: bpy.types.Object,
) -> None:
    if obj.parent is not expected_parent:
        observed = obj.parent.name if obj.parent is not None else None
        raise AssertionError(
            f"{label}: expected parent {expected_parent.name}, observed {observed}"
        )


def validate_scene(root: bpy.types.Object) -> dict[str, object]:
    checks: list[str] = []
    if bpy.context.scene.unit_settings.system != "METRIC":
        raise AssertionError("scene unit system must be METRIC")
    if abs(bpy.context.scene.unit_settings.scale_length - 1.0) > 0.0001:
        raise AssertionError("scene scale must be 1 metre")
    checks.append("metric unit scale is 1 metre")

    assert_vector("root pivot", root.location, (0.0, 0.0, 0.0))
    if root["blender_forward_axis"] != "+Y":
        raise AssertionError("Blender forward-axis contract drifted")
    if root["runtime_forward_axis"] != "+Z":
        raise AssertionError("runtime forward-axis contract drifted")
    checks.append("ground-center pivot and forward-axis mapping are explicit")

    objects = {obj.name: obj for obj in [root] + descendants(root)}
    required_names = {
        "GEO",
        "LOD0_FIRSTLOOK",
        "LOD1_RESERVED",
        "LOD2_RESERVED",
        "RIG",
        "CONTACTS",
        "AXLE_FRONT",
        "AXLE_REAR",
        "SOCKETS",
        "MODULES",
        "COLLISION",
        "DOOR_DRIVER",
        "MODULE_WINCH_ASSEMBLY",
        "MODULE_SEALED_RANGE_TANK",
        "UPGRADE_PATCHWORK_SKID_PLATE",
        *CONTACT_POSITIONS.keys(),
        *PIVOT_NAMES.values(),
        *WHEEL_NAMES.values(),
        *SUSPENSION_NAMES.values(),
        *SOCKET_POSITIONS.keys(),
    }
    missing = sorted(required_names - set(objects))
    if missing:
        raise AssertionError(f"required object names missing: {missing}")
    checks.append(f"{len(required_names)} required semantic names exist")

    contacts_root = objects["CONTACTS"]
    front_axle = objects["AXLE_FRONT"]
    rear_axle = objects["AXLE_REAR"]
    assert_parent("CONTACTS", contacts_root, objects["RIG"])
    assert_parent("AXLE_FRONT", front_axle, objects["RIG"])
    assert_parent("AXLE_REAR", rear_axle, objects["RIG"])
    assert_vector("AXLE_FRONT", front_axle.location, (0.0, 0.0, 0.0))
    assert_vector("AXLE_REAR", rear_axle.location, (0.0, 0.0, 0.0))
    assert_identity_rotation("AXLE_FRONT", front_axle)
    assert_identity_rotation("AXLE_REAR", rear_axle)

    expected_wheel_rotation = Euler(
        (0.0, math.pi / 2.0, 0.0)
    ).to_quaternion()
    for contact_name, runtime_position in CONTACT_POSITIONS.items():
        expected = runtime_to_blender(runtime_position)
        contact = objects[contact_name]
        pivot = objects[PIVOT_NAMES[contact_name]]
        suspension = objects[SUSPENSION_NAMES[contact_name]]
        wheel = objects[WHEEL_NAMES[contact_name]]
        expected_axle = (
            front_axle
            if contact_name in {"CONTACT_FL", "CONTACT_FR"}
            else rear_axle
        )
        assert_parent(contact_name, contact, contacts_root)
        assert_parent(PIVOT_NAMES[contact_name], pivot, expected_axle)
        assert_parent(SUSPENSION_NAMES[contact_name], suspension, pivot)
        assert_parent(WHEEL_NAMES[contact_name], wheel, suspension)
        assert_vector(contact_name, contact.location, expected)
        assert_vector(PIVOT_NAMES[contact_name], pivot.location, expected)
        assert_vector(
            SUSPENSION_NAMES[contact_name],
            suspension.location,
            (0.0, 0.0, 0.0),
        )
        assert_vector(WHEEL_NAMES[contact_name], wheel.location, (0.0, 0.0, 0.0))
        assert_identity_rotation(PIVOT_NAMES[contact_name], pivot)
        assert_identity_rotation(SUSPENSION_NAMES[contact_name], suspension)
        wheel_rotation = wheel.rotation_euler.to_quaternion()
        if wheel_rotation.rotation_difference(expected_wheel_rotation).angle > 0.0001:
            raise AssertionError(
                f"{WHEEL_NAMES[contact_name]}: axle orientation drifted"
            )
    checks.append(
        "four contact, axle, steering/hub, suspension, and wheel transforms "
        "match runtime hierarchy"
    )

    sockets_root = objects["SOCKETS"]
    rig_root = objects["RIG"]
    for socket_name, runtime_position in SOCKET_POSITIONS.items():
        socket = objects[socket_name]
        expected_parent = rig_root if socket_name == "DOOR_DRIVER" else sockets_root
        assert_parent(socket_name, socket, expected_parent)
        assert_vector(
            socket_name,
            socket.location,
            runtime_to_blender(runtime_position),
        )
        assert_identity_rotation(socket_name, socket)
    checks.append("upgrade, cargo, tool, camera, door, and service sockets match contract")

    modules_root = objects["MODULES"]
    assert_parent("MODULES", modules_root, root)
    for module_name, socket_name in (
        ("MODULE_WINCH_ASSEMBLY", "SOCKET_UPGRADE_FRONT"),
        ("MODULE_SEALED_RANGE_TANK", "SOCKET_UPGRADE_CARGO_01"),
        ("UPGRADE_PATCHWORK_SKID_PLATE", "SOCKET_UPGRADE_UNDERBODY"),
    ):
        module = objects[module_name]
        assert_parent(module_name, module, modules_root)
        assert_vector(module_name, module.location, objects[socket_name].location)
        assert_identity_rotation(module_name, module)
    checks.append("three module roots remain bound to their upgrade sockets")

    counts = collect_counts(root)
    if counts["wheel_count"] != 4:
        raise AssertionError(f"expected 4 wheels, observed {counts['wheel_count']}")
    if counts["material_count"] > 3:
        raise AssertionError(f"material family cap exceeded: {counts['materials']}")
    if counts["render_triangles"] > LOD0_BASE_MAXIMUM_TRIANGLES:
        raise AssertionError(
            f"LOD0 first-look exceeds semantic base budget: {counts['render_triangles']}"
        )
    if counts["render_triangles"] < LOD0_BASE_MINIMUM_TRIANGLES:
        raise AssertionError(
            f"LOD0 first-look is below semantic base budget: "
            f"{counts['render_triangles']}"
        )
    if counts["collision_mesh_objects"] != 4:
        raise AssertionError("collision proxy must remain four simple boxes")
    checks.append(
        f"{counts['render_triangles']} render triangles, "
        f"{counts['material_count']} materials, and 4 collision boxes are bounded"
    )
    return {"checks": checks, "counts": counts}


def validate_export_names(required_names: set[str]) -> dict[str, object]:
    if not GLB_PATH.exists() or GLB_PATH.stat().st_size <= 20:
        raise AssertionError("GLB export is absent or empty")
    with GLB_PATH.open("rb") as handle:
        if handle.read(4) != b"glTF":
            raise AssertionError("derived export does not have a GLB header")

    # Blender object names are global across scenes. Clear the reopened source
    # only after its validation is complete, then import into the now-empty
    # process so an exact exported name cannot be hidden behind a `.001` suffix.
    # This validation process never saves the cleared scene.
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.ops.import_scene.gltf(filepath=str(GLB_PATH))
    imported_objects = list(bpy.context.scene.objects)
    imported_names = {obj.name for obj in imported_objects}
    missing = sorted(required_names - imported_names)
    if missing:
        raise AssertionError(f"GLB import lost semantic names: {missing}")
    imported_triangles = count_triangles(imported_objects)
    return {
        "glb_header": "glTF",
        "imported_object_count": len(imported_objects),
        "imported_triangles_including_collision": imported_triangles,
        "required_names_preserved": len(required_names),
    }


def write_validation_report(
    scene_validation: dict[str, object],
    export_validation: dict[str, object],
) -> None:
    report = {
        "schema": "atomic-land-pirate.asset-validation.v1",
        "content_id": CONTENT_ID,
        "source_version": SOURCE_VERSION,
        "status": "pass",
        "blender_version": bpy.app.version_string,
        "canonical_source": relative_output_record(BLEND_PATH),
        "derived_export": relative_output_record(GLB_PATH),
        "scene": scene_validation,
        "export_reimport": export_validation,
    }
    VALIDATION_PATH.write_text(
        json.dumps(report, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )

    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    manifest["validation"] = relative_output_record(VALIDATION_PATH)
    MANIFEST_PATH.write_text(
        json.dumps(manifest, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )


def generate() -> None:
    PACKAGE_DIR.mkdir(parents=True, exist_ok=True)
    DERIVED_DIR.mkdir(parents=True, exist_ok=True)
    clear_scene()
    configure_scene()
    materials = create_materials()
    root = build_vehicle(materials)
    _, camera = create_review_scene(materials)
    scene_validation = validate_scene(root)
    render_paths = render_turntable(camera)
    export_glb(root)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH), compress=True)
    write_manifest(scene_validation["counts"], render_paths)
    print(
        "SASHA_SCOUT_GENERATED "
        + json.dumps(
            {
                "blend": str(BLEND_PATH),
                "glb": str(GLB_PATH),
                "render_triangles": scene_validation["counts"]["render_triangles"],
            },
            sort_keys=True,
        )
    )


def validate_existing() -> None:
    if Path(bpy.data.filepath).resolve() != BLEND_PATH.resolve():
        raise AssertionError(
            f"validation opened {bpy.data.filepath}, expected {BLEND_PATH}"
        )
    root = bpy.data.objects.get(ROOT_NAME)
    if root is None:
        raise AssertionError(f"{ROOT_NAME} root is missing")
    scene_validation = validate_scene(root)
    required_export_names = {
        ROOT_NAME,
        "GEO",
        "LOD0_FIRSTLOOK",
        "LOD1_RESERVED",
        "LOD2_RESERVED",
        "RIG",
        "CONTACTS",
        "AXLE_FRONT",
        "AXLE_REAR",
        "SOCKETS",
        "MODULES",
        "COLLISION",
        "DOOR_DRIVER",
        "MODULE_WINCH_ASSEMBLY",
        "MODULE_SEALED_RANGE_TANK",
        "UPGRADE_PATCHWORK_SKID_PLATE",
        *CONTACT_POSITIONS.keys(),
        *PIVOT_NAMES.values(),
        *WHEEL_NAMES.values(),
        *SUSPENSION_NAMES.values(),
        *SOCKET_POSITIONS.keys(),
    }
    export_validation = validate_export_names(required_export_names)
    write_validation_report(scene_validation, export_validation)
    print(
        "SASHA_SCOUT_VALIDATED "
        + json.dumps(
            {
                "scene_checks": len(scene_validation["checks"]),
                "export_names": export_validation["required_names_preserved"],
                "status": "pass",
            },
            sort_keys=True,
        )
    )


if __name__ == "__main__":
    arguments = parse_args()
    if arguments.validate_existing:
        validate_existing()
    else:
        generate()
