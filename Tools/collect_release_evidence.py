#!/usr/bin/env python3
"""Archive successful native Unity evidence with matching production/data/art hashes."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]

def hashes(root):
    result = {}
    for area in ('Assets', 'Packages', 'ProjectSettings'):
        for path in sorted((root / area).rglob('*')):
            if path.is_file() and path.name != '.DS_Store':
                result[path.relative_to(root).as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()
    return result

def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--project-copy', required=True, type=Path)
    ap.add_argument('--run-output', required=True, type=Path)
    ap.add_argument('--offline-output', required=True, type=Path)
    ap.add_argument('--target', type=Path, default=ROOT/'Docs/ReleaseVerification')
    ap.add_argument('--build-summary', type=Path)
    args = ap.parse_args()
    original, tested = hashes(ROOT), hashes(args.project_copy)
    mismatches = [name for name in sorted(set(original)|set(tested)) if original.get(name)!=tested.get(name)]
    if mismatches: raise SystemExit('Tested project differs from workspace: '+', '.join(mismatches))
    recorded = args.run_output/'input-hashes-before-tests.json'
    if recorded.exists() and json.loads(recorded.read_text())['files'] != original:
        raise SystemExit('Workspace changed since native tests began')
    checks = {}
    for name in ('editmode-results.xml','playmode-results.xml','cold-resume-results.xml'):
        doc = ET.parse(args.run_output/name).getroot()
        stats = {key:doc.get(key) for key in ('total','passed','failed','skipped','result','start-time','end-time','duration')}
        if stats['failed']!='0' or stats['skipped']!='0' or stats['result']!='Passed':
            raise SystemExit('Refusing to archive unsuccessful/incomplete native run: '+name+' '+str(stats))
        checks[name] = stats
    out = args.target; out.mkdir(parents=True,exist_ok=True)
    for name in checks: shutil.copy2(args.run_output/name,out/name)
    shutil.copy2(args.offline_output/'summary.json',out/'offline-summary.json')
    shutil.copy2(args.offline_output/'logic.tests.log',out/'offline-logic-results.txt')
    for folder in [args.run_output] + sorted(path for path in args.run_output.rglob('*') if path.is_dir()):
        if not folder.exists(): continue
        relative=folder.relative_to(args.run_output)
        for file in folder.iterdir():
            if file.suffix.lower()=='.png' or file.name in ('journey-actions.txt','screenshots.txt','checkpoint-writer.json','checkpoint-reader.json'):
                target=out/'screenshots'/relative/file.name;target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(file,target)
    digest=hashlib.sha256(json.dumps(original,sort_keys=True,separators=(',',':')).encode()).hexdigest()
    (out/'source-data-art-hashes.json').write_text(json.dumps({'algorithm':'SHA-256','fileCount':len(original),
        'aggregateHash':digest,'aggregateEncoding':'SHA-256 of JSON-encoded sorted file-hash map, ASCII and compact separators','workspaceMatchesTestedProject':True,'scope':['Assets','Packages','ProjectSettings'],'files':original},ensure_ascii=False,indent=2)+'\n')
    if args.build_summary:
        build=json.loads(args.build_summary.read_text())
        if build.get('result')!='Succeeded' or build.get('errors')!=0: raise SystemExit('Mac build did not succeed')
        shutil.copy2(args.build_summary,out/'mac-build-summary.json')
    if recorded.exists(): shutil.copy2(recorded,out/'input-hashes-before-tests.json')
    (out/'native-summary.json').write_text(json.dumps(checks,indent=2)+'\n')
    print(json.dumps({'directory':str(out),'native':checks,'matchingFiles':len(original),'aggregateHash':digest},indent=2))

if __name__=='__main__': main()
