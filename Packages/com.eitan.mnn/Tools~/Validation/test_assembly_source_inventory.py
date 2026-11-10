import importlib.util
from pathlib import Path
import tempfile
import unittest

HERE = Path(__file__).resolve().parent
SPEC = importlib.util.spec_from_file_location("assembly_compilation", HERE / "check_assembly_compilation.py")
COMPILATION = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(COMPILATION)


class AssemblySourceInventoryTests(unittest.TestCase):
    def test_compiler_reads_live_sources_and_importable_samples_only(self):
        artifacts = HERE.parents[3] / "TestArtifacts~/UnifiedAPI/InventoryFixtures"
        artifacts.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(dir=artifacts) as temporary:
            root = Path(temporary)
            wanted = {"Runtime/Api.cs", "Runtime/MNN.asmdef", "Samples~/Basic/Example.cs"}
            ignored = {"TestArtifacts~/Copy/Old.cs", "TestArtifacts~/Copy/Old.asmdef",
                       "Library/Generated.cs", ".cache/Old.cs", "Tools~/Probe.cs", "Runtime/Plugins/Old.cs"}
            for relative in wanted | ignored:
                path = root / relative
                path.parent.mkdir(parents=True, exist_ok=True)
                path.touch()
            actual = {path.relative_to(root).as_posix() for path in COMPILATION.managed_files(root)}
            self.assertEqual(wanted, actual)


if __name__ == "__main__":
    unittest.main()
