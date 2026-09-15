#!/usr/bin/env python3
"""Compile current game assemblies and run managed battle rules without editing Assets.

Requires the project's imported Library cache and its installed Unity editor.
This does not replace Unity asset import, Play Mode, or a player build.
"""

import argparse
import json
import re
import subprocess
import sys
import tempfile
from pathlib import Path


def version_key(path):
    return tuple(int(part) for part in re.findall(r"\d+", path.name))


def quote(path):
    return '"' + str(path) + '"'


class VerificationError(Exception):
    pass


class Verifier:
    def __init__(self, root, unity, output):
        self.root = root
        self.output = output
        self.output.mkdir(parents=True, exist_ok=True)
        self.scripting = unity.parent.parent / "Resources/Scripting"
        sdk_root = self.scripting / "DotNetSdk"
        self.dotnet = sdk_root / "dotnet"
        sdk_versions = sorted((sdk_root / "sdk").glob("*"), key=version_key)
        if not self.dotnet.exists() or not sdk_versions:
            raise VerificationError("Unity bundled .NET SDK not found; specify --unity.")
        self.csc = sdk_versions[-1] / "Roslyn/bincore/csc.dll"
        self.local = {}
        self.guid_names = {}
        for path in (root / "Assets").rglob("*.asmdef"):
            data = json.loads(path.read_text(encoding="utf-8-sig"))
            self.local[data["name"]] = (path, data)
            self.register_guid(path, data["name"])
        self.packages = {}
        for path in (root / "Library/PackageCache").rglob("*.asmdef"):
            data = json.loads(path.read_text(encoding="utf-8-sig"))
            self.packages[data["name"]] = path
            self.register_guid(path, data["name"])
        self.compiled = {}
        self.results = []

    def register_guid(self, path, name):
        meta = path.with_suffix(path.suffix + ".meta")
        if meta.exists():
            match = re.search(r"^guid:\s*(\w+)", meta.read_text(), re.MULTILINE)
            if match:
                self.guid_names[match.group(1)] = name

    def command(self, args, log_name):
        result = subprocess.run(args, cwd=self.root, text=True, capture_output=True)
        log = self.output / log_name
        log.write_text(result.stdout + result.stderr)
        errors = [line for line in log.read_text().splitlines() if "error " in line]
        warnings = sum("warning " in line for line in log.read_text().splitlines())
        print(f"{log_name}: exit={result.returncode}, warnings={warnings}")
        for line in errors[:20]:
            print(line)
        if result.returncode:
            if not errors:
                print((result.stdout + result.stderr)[-5000:])
            raise VerificationError(f"Command failed; see {log}")
        return log

    def package_reference(self, name):
        if name.startswith("GUID:"):
            guid = name[5:]
            if guid not in self.guid_names:
                raise VerificationError(f"Unknown assembly GUID: {guid}")
            name = self.guid_names[guid]
        if name in self.local:
            return self.compile_assembly(name)
        if name not in self.packages:
            raise VerificationError(f"asmdef reference '{name}' does not resolve to a project/package assembly.")
        # Prefer an actual imported package assembly; never substitute another name.
        library = self.root / "Library/ScriptAssemblies" / (name + ".dll")
        if library.exists():
            return library
        refs = sorted((self.root / "Library/Bee/artifacts").glob("*/" + name + ".ref.dll"))
        if refs:
            return refs[-1]
        raise VerificationError(f"No imported package binary for '{name}'; open the project in licensed Unity first.")

    def compile_assembly(self, name):
        if name in self.compiled:
            return self.compiled[name]
        path, definition = self.local[name]
        is_test = "TestAssemblies" in definition.get("optionalUnityReferences", [])
        templates = list((self.root / "Library/Bee/artifacts").glob("*/" + name + ".rsp"))
        if not templates and is_test:
            # Test assemblies added before a licensed Unity import have no Bee
            # response file yet. Use the editor's engine/define template, then
            # replace sources and honor the test asmdef's explicit references.
            baseline = "FinalDefense.Editor" if "Editor" in definition.get("includePlatforms", []) else "FinalDefense"
            templates = list((self.root / "Library/Bee/artifacts").glob("*/" + baseline + ".rsp"))
        if not templates:
            raise VerificationError(f"No Unity compiler response file for {name}; import the project first.")
        template = max(templates, key=lambda item: item.stat().st_mtime)
        lines = []
        for line in template.read_text().splitlines():
            if line.startswith(('"Assets/', '-out:', '-refout:')):
                continue
            match = re.match(r'(-r:|-analyzer:|/additionalfile:)"([^"]+)"$', line)
            if match:
                option, value = match.groups()
                # Old Bee references can mask missing current asmdef dependencies.
                if option == "-r:" and value.startswith(("Library/Bee/", "Library/ScriptAssemblies/")):
                    continue
                if (option == "-r:" and value.startswith("Library/PackageCache/")
                        and definition.get("overrideReferences", False)
                        and Path(value).name not in definition.get("precompiledReferences", [])):
                    continue
                resolved = Path(value)
                if not resolved.is_absolute():
                    resolved = self.root / resolved
                if not resolved.exists():
                    raise VerificationError(f"Cached compiler dependency is missing: {resolved}")
                line = option + quote(resolved)
            lines.append(line)
        if is_test:
            lines.append("-define:UNITY_INCLUDE_TESTS")
        for reference in definition.get("references", []):
            lines.append("-r:" + quote(self.package_reference(reference)))
        if is_test:
            # Unity adds these through optionalUnityReferences=TestAssemblies;
            # repeating them in the asmdef itself is a duplicate-reference error.
            test_references = ["UnityEngine.TestRunner"]
            if "Editor" in definition.get("includePlatforms", []):
                test_references.append("UnityEditor.TestRunner")
            for reference in test_references:
                lines.append("-r:" + quote(self.package_reference(reference)))
        sources = []
        for source in sorted(path.parent.rglob("*.cs")):
            # A nested asmdef owns its own source files.
            if any(other != path and path.parent in other.parents and other.parent in source.parents
                   for other, _ in self.local.values()):
                continue
            sources.append(source)
        if not sources:
            raise VerificationError(f"No sources found for {name}.")
        binary = self.output / (name + ".dll")
        lines.extend(["-out:" + quote(binary), "-refout:" + quote(self.output / (name + ".ref.dll"))])
        lines.extend(quote(source) for source in sources)
        response = self.output / (name + ".rsp")
        response.write_text("\n".join(lines) + "\n")
        log = self.command([str(self.dotnet), "exec", str(self.csc), "/nostdlib", "/noconfig", "@" + str(response)], name + ".compile.log")
        self.compiled[name] = binary
        self.results.append({"assembly": name, "sourceCount": len(sources), "passed": True,
                             "errors": 0, "warnings": sum("warning " in line for line in log.read_text().splitlines()),
                             "scope": "test assembly compiled only; not executed" if is_test else "production assembly compiled"})
        return binary

    def logic_tests(self):
        suite = self.root / "Tests/Offline"
        manifest = json.loads((suite / "sources.json").read_text())
        sources = [self.root / name for name in manifest]
        sources.extend(sorted(suite.glob("*.cs")))
        for source in sources:
            if not source.exists():
                raise VerificationError(f"Missing test/source: {source}")
        sdk_root = self.scripting / "DotNetSdk"
        packs = sorted((sdk_root / "packs/Microsoft.NETCore.App.Ref").glob("*"), key=version_key)
        if not packs:
            raise VerificationError("Unity bundled .NET reference pack not found.")
        pack = packs[-1]
        frameworks = sorted((pack / "ref").glob("net*"), key=version_key)
        framework = frameworks[-1]
        refs = list(framework.glob("*.dll"))
        binary = self.output / "BattleLogicTests.dll"
        response = self.output / "BattleLogicTests.rsp"
        response.write_text("\n".join(["-target:exe", "-langversion:9.0", "-out:" + quote(binary)]
                                     + ["-r:" + quote(ref) for ref in refs]
                                     + [quote(source) for source in sources]) + "\n")
        self.command([str(self.dotnet), "exec", str(self.csc), "/nologo", "/nostdlib", "/noconfig", "@" + str(response)], "logic.compile.log")
        (self.output / "BattleLogicTests.runtimeconfig.json").write_text(json.dumps({
            "runtimeOptions": {"tfm": framework.name,
                               "framework": {"name": "Microsoft.NETCore.App", "version": pack.name}}
        }, indent=2))
        log = self.command([str(self.dotnet), str(binary)], "logic.tests.log")
        print(log.read_text().strip())
        counts = re.search(r"(\d+)/(\d+) scenarios passed; (\d+) assertions; (\d+) failed\.", log.read_text())
        if not counts:
            raise VerificationError("Logic test log did not report scenario/assertion counts.")
        self.results.append({"testSuite": "BattleLogicTests", "passed": True,
                             "scenariosPassed": int(counts[1]), "scenariosTotal": int(counts[2]),
                             "assertions": int(counts[3]), "scenariosFailed": int(counts[4])})


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--unity", type=Path, help="Path to Unity editor executable")
    parser.add_argument("--output", type=Path, help="Output directory; default is a new /tmp directory")
    parser.add_argument("--mode", choices=("all", "compile", "logic"), default="all")
    args = parser.parse_args()
    root = args.project.resolve()
    version = re.search(r"m_EditorVersion:\s*(\S+)", (root / "ProjectSettings/ProjectVersion.txt").read_text()).group(1)
    unity = args.unity or Path(f"/Applications/Unity/Hub/Editor/{version}/Unity.app/Contents/MacOS/Unity")
    output = (args.output or Path(tempfile.mkdtemp(prefix="unitygame-verify-", dir="/tmp"))).resolve()
    print(f"Verification artifacts: {output}")
    try:
        verifier = Verifier(root, unity.resolve(), output)
        if args.mode in ("all", "compile"):
            verifier.compile_assembly("FinalDefense")
            verifier.compile_assembly("FinalDefense.Editor")
            for name, (_, definition) in sorted(verifier.local.items()):
                if "TestAssemblies" in definition.get("optionalUnityReferences", []):
                    verifier.compile_assembly(name)
        if args.mode in ("all", "logic"):
            verifier.logic_tests()
        (output / "summary.json").write_text(json.dumps({
            "unityVersion": version, "mode": args.mode, "results": verifier.results,
            "limitations": "Offline compilation and managed rules only; Unity asset import and Play Mode were not run."
        }, indent=2))
    except (VerificationError, OSError, ValueError) as error:
        print(f"FAILED: {error}", file=sys.stderr)
        return 1
    print("PASS (offline scope only; see summary.json)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
