#!/usr/bin/env python3
"""Give an already synchronized Unity test copy its own preference identity."""
import argparse
import hashlib
import json
import re
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
COMPANY="CodexVerification"
PRODUCT="OriginalUI20260915"
BUNDLE="com.codexverification.originalui20260915"


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project-copy",required=True,type=Path)
    parser.add_argument("--evidence",required=True,type=Path)
    args=parser.parse_args()
    target=args.project_copy.resolve()
    if target==ROOT or ROOT in target.parents:raise SystemExit("Use a separate project copy outside the working repository")
    source=(ROOT/"ProjectSettings/ProjectSettings.asset").read_bytes()
    path=target/"ProjectSettings/ProjectSettings.asset"
    if path.read_bytes()!=source:raise SystemExit("Synchronize original ProjectSettings before applying the recorded test-only identity")
    text=source.decode()
    before={}
    for key,value in (("companyName",COMPANY),("productName",PRODUCT)):
        pattern=r"(?m)^  "+key+r": (.*)$"
        match=re.search(pattern,text)
        if match is None:raise SystemExit("Missing Unity setting "+key)
        before[key]=match[1];text=re.sub(pattern,"  "+key+": "+value,text,count=1)
    pattern=r"(  applicationIdentifier:\n    Standalone: )([^\n]+)"
    match=re.search(pattern,text)
    if match is None:raise SystemExit("Missing Standalone application identifier")
    before["Standalone"]=match[2];text=re.sub(pattern,lambda m:m[1]+BUNDLE,text,count=1)
    path.write_text(text)
    result={"file":"ProjectSettings/ProjectSettings.asset","purpose":"Isolated editor and Mac test-player preferences; restore original ProjectSettings for the formal build",
            "originalSha256":hashlib.sha256(source).hexdigest(),"testSha256":hashlib.sha256(path.read_bytes()).hexdigest(),"before":before,
            "after":{"companyName":COMPANY,"productName":PRODUCT,"Standalone":BUNDLE},"expectedEditorPrefsDomain":"unity."+COMPANY+"."+PRODUCT,"testPlayerPrefsDomain":BUNDLE}
    args.evidence.parent.mkdir(parents=True,exist_ok=True);args.evidence.write_text(json.dumps(result,indent=2)+"\n")
    print(json.dumps(result,indent=2))


if __name__=="__main__":main()
