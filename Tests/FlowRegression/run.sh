#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../.."
version="$(sed -n 's/^m_EditorVersion: //p' ProjectSettings/ProjectVersion.txt)"
editor="${UNITY_EDITOR_DIR:-$HOME/Unity/Hub/Editor/$version/Editor}"
if [[ ! -d "$editor/Data/MonoBleedingEdge" ]]; then
  echo 'Set UNITY_EDITOR_DIR to your Unity Editor directory (containing Data/MonoBleedingEdge).' >&2
  exit 1
fi
mono="$editor/Data/MonoBleedingEdge"
out="$(mktemp -d /tmp/colony-regression.XXXXXX)"
trap 'rm -rf "$out"' EXIT
python3 - "$out/StartNewGame.cs" <<'PYGEN'
from pathlib import Path
import sys
source=Path('Assets/_Project/Scripts/Core/ColonyGame.cs').read_text()
a=source.index('        public void StartNewGame(')
b=source.index('{',a)
depth=1
end=b+1
while depth:
    if source[end]=='{': depth+=1
    elif source[end]=='}': depth-=1
    end+=1
Path(sys.argv[1]).write_text('namespace AntColony { public partial class ColonyGame {\n'+source[a:end]+'\n} }')
wave=Path('Assets/_Project/Scripts/Enemies/WaveSpawner.cs').read_text()
start=wave.index('        readonly List<WaveReportRecord> reports')
end=wave.index('        ColonyGame game;',start)
body=wave[start:end]
for declaration in ['        void LateUpdate()', '        public void RemoveEnemy(']:
    start=wave.index(declaration)
    opening=wave.index('{',start)
    depth=1
    end=opening+1
    while depth:
        if wave[end]=='{': depth+=1
        elif wave[end]=='}': depth-=1
        end+=1
    body+='\n'+wave[start:end]
Path(sys.argv[1]).with_name('WaveReporting.cs').write_text('using System.Collections.Generic; using UnityEngine; namespace AntColony { public partial class WaveSpawner {\n'+body+'\n} }')

PYGEN
"$mono/bin/mono" "$mono/lib/mono/4.5/mcs.exe" -langversion:latest -r:System.Web.Extensions \
  -out:"$out/checks.exe" "$out/StartNewGame.cs" "$out/WaveReporting.cs" Tests/FlowRegression/RuntimeStubs.cs Tests/FlowRegression/Program.cs \
  Assets/_Project/Scripts/Core/Tutorial.cs Assets/_Project/Scripts/Core/SaveData.cs
"$mono/bin/mono" "$out/checks.exe"
