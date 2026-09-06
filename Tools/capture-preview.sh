#!/bin/sh
# 构建 macOS 预览版并自动截取五阶段画面，拼图后复制一份到桌面（桌面只保留最新一份）。
# 用法: Tools/capture-preview.sh [--no-build]
set -e
PROJECT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY=/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity
APP="$PROJECT/Builds/macOS/SleepHealingPreview.app/Contents/MacOS/星空深睡"

if [ "$1" != "--no-build" ]; then
  "$UNITY" -batchmode -nographics -projectPath "$PROJECT" -buildTarget StandaloneOSX \
    -executeMethod SleepHealing.Editor.SleepHealingBuild.BuildMacPreview -quit \
    -logFile "$PROJECT/Logs/build-macos-urp.log"
fi

rm -f "$PROJECT"/Artifacts/0[1-5]-*.png
"$APP" --capture-artifacts "$PROJECT/Artifacts"
python3 "$PROJECT/Tools/montage.py" "$PROJECT/Artifacts" "$PROJECT/Artifacts/五阶段预览.png"

STAMP="$(date +%m%d-%H%M)"
rm -f "$HOME/Desktop/星空深睡-五阶段预览-"*.png
cp "$PROJECT/Artifacts/五阶段预览.png" "$HOME/Desktop/星空深睡-五阶段预览-$STAMP.png"
echo "done: $HOME/Desktop/星空深睡-五阶段预览-$STAMP.png"
