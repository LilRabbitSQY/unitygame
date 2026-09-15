#!/usr/bin/env python3
"""Compile and execute campaign rules with Unity's bundled .NET, using only temporary saves.
No Editor, PlayerPrefs, imported Library or graphics session is used.
"""
import argparse, hashlib, json, re, subprocess, tempfile
from pathlib import Path

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output',type=Path)
    args=parser.parse_args()
    root=Path(__file__).resolve().parents[1]
    version=re.search(r'm_EditorVersion:\s*(\S+)',(root/'ProjectSettings/ProjectVersion.txt').read_text())[1]
    sdk=Path(f'/Applications/Unity/Hub/Editor/{version}/Unity.app/Contents/Resources/Scripting/DotNetSdk')
    dotnet=sdk/'dotnet'; csc=next((sdk/'sdk').glob('*/Roslyn/bincore/csc.dll'))
    pack=sorted((sdk/'packs/Microsoft.NETCore.App.Ref').glob('*'))[-1]; framework=next((pack/'ref').glob('net*'))
    out=(args.output or Path(tempfile.mkdtemp(prefix='campaign-verify-'))).resolve();out.mkdir(parents=True,exist_ok=True)
    scripts=root/'Assets/_Game/Scripts'
    sources=[]
    for folder in ['Contracts','Campaign','Persistence']:
        sources+=sorted((scripts/folder).glob('*.cs'))
    sources+=[scripts/'Shop/BattleItemDefs.cs',scripts/'Dialogue/CampaignDialogueCoordinator.cs',scripts/'Dialogue/HttpDialogueGateway.cs']
    sources+=sorted((root/'Tests/Offline/Campaign').glob('*.cs'))
    binary=out/'CampaignTests.dll'; rsp=out/'compile.rsp'
    rsp.write_text('\n'.join(['-target:exe','-langversion:9.0',f'-out:"{binary}"']+[f'-r:"{p}"' for p in framework.glob('*.dll')]+[f'"{p}"' for p in sources]))
    commands=[[str(dotnet),'exec',str(csc),'/nologo','/nostdlib','/noconfig','@'+str(rsp)],[str(dotnet),str(binary),str(root),str(out)]]
    (out/'CampaignTests.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':framework.name,'framework':{'name':'Microsoft.NETCore.App','version':pack.name}}}))
    for name,cmd in zip(['compile','tests'],commands):
        result=subprocess.run(cmd,cwd=root,text=True,capture_output=True)
        (out/f'{name}.log').write_text(result.stdout+result.stderr);print(result.stdout+result.stderr)
        if result.returncode:return result.returncode
    result_text=(out/'tests.log').read_text()
    counts=re.search(r'(\d+) scenarios passed; (\d+) assertions; 0 failed',result_text)
    (out/'summary.json').write_text(json.dumps({'passed':True,'scenarios':int(counts[1]),'assertions':int(counts[2]),'unityVersion':version,'scope':'Offline managed campaign tests; fake AI and supplied test outcomes, not a natural Unity playthrough','sourceHashes':{str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest() for p in sources}},indent=2))
    print(f'PASS — offline campaign scope only; artifacts: {out}')
    return 0
if __name__=='__main__':raise SystemExit(main())
