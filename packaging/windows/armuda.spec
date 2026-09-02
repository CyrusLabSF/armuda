from pathlib import Path
from PyInstaller.utils.hooks import collect_dynamic_libs


ROOT = Path(SPECPATH).parents[1]
SOURCE_ROOT = ROOT / "Armuda World Directory Map"
APP_DIR = SOURCE_ROOT / "Armuda"
ASSETS_DIR = ROOT / "assets"


def add_tree(data_entries, source, destination, excluded_prefixes=()):
    source = Path(source)
    for file_path in source.rglob("*"):
        if not file_path.is_file():
            continue
        relative = file_path.relative_to(source)
        normalized = "/".join(part.lower() for part in relative.parts)
        if "__pycache__" in relative.parts or file_path.suffix.lower() in {".pyc", ".pyo"}:
            continue
        if any(normalized == prefix or normalized.startswith(prefix + "/") for prefix in excluded_prefixes):
            continue
        data_entries.append((str(file_path), str(Path(destination) / relative.parent)))


datas = []
binaries = collect_dynamic_libs("glfw")

# Runtime shaders and non-Python package resources.
for resource_dir in ("Aesthetics", "Audio", "Objects", "Visuals", "Zones"):
    add_tree(datas, APP_DIR / resource_dir, Path("Armuda") / resource_dir)

# Runtime UI resources referenced relative to the importing modules.
add_tree(
    datas,
    APP_DIR / "assets",
    Path("Armuda") / "assets",
    excluded_prefixes=("userimages", "usermeshes"),
)
add_tree(datas, APP_DIR / "UI" / "Fonts", Path("Armuda") / "UI" / "Fonts")
add_tree(datas, APP_DIR / "Core" / "shaders", Path("Armuda") / "Core" / "shaders")
datas.append((str(APP_DIR / "Core" / "master_domains.json"), str(Path("Armuda") / "Core")))

# Clean default configuration only; local identities/worlds/logs are excluded.
for filename in (
    "artus_connection.json",
    "hud_configs.json",
    "image_backend.json",
    "image_generation_connection.json",
):
    datas.append((str(APP_DIR / "Data" / filename), str(Path("Armuda") / "Data")))

# Shared library assets, excluding local uploads.
add_tree(datas, ASSETS_DIR, "assets", excluded_prefixes=("builderlibrary/uploads",))


hiddenimports = [
    "Armuda.Core.armuda_emotion_field",
    "Armuda.Core.icon_spawner",
    "Armuda.Core.jeopardy_cube_spawner",
    "Armuda.Core.smar_link_node",
    "Armuda.UI.simple_font_renderer",
    "Armuda.UI.text_shader",
    # Legacy modules loaded after OceanBrain extends sys.path at runtime.
    # PyInstaller cannot discover these imports from the package root alone.
    "environment_assets_loader",
    "jelly_chat_ui",
    "link_node_autoloader",
    "ocean_floor_renderer",
    "smar_link_node",
    "world_map",
    "pywavefront",
    "trimesh",
]

legacy_module_paths = [
    APP_DIR / "Core",
    APP_DIR / "UI",
    APP_DIR / "Visuals",
]


a = Analysis(
    [str(APP_DIR / "run_forever.py")],
    pathex=[str(SOURCE_ROOT), *(str(path) for path in legacy_module_paths)],
    binaries=binaries,
    datas=datas,
    hiddenimports=hiddenimports,
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=["cv2", "pygame", "pytest", "matplotlib"],
    noarchive=False,
    optimize=1,
)
pyz = PYZ(a.pure)

exe = EXE(
    pyz,
    a.scripts,
    [],
    exclude_binaries=True,
    name="Armuda",
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=True,
    console=False,
    disable_windowed_traceback=False,
)

coll = COLLECT(
    exe,
    a.binaries,
    a.datas,
    strip=False,
    upx=True,
    upx_exclude=[],
    name="Armuda",
)
