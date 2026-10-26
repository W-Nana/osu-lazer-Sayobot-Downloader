#!/usr/bin/env bash
set -euo pipefail
task_project_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$task_project_root"
task_dotnet="${task_project_root}/.tools/dotnet/dotnet"
if [[ ! -x "$task_dotnet" ]]; then
    task_dotnet="$(command -v dotnet)"
fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
python3 scripts/prepare-references.py
"$task_dotnet" build osu.Game.Rulesets.Sayobot/osu.Game.Rulesets.Sayobot.csproj -c Release --nologo
"$task_dotnet" run --project tests/Sayobot.Tests -c Release -- "$@"
python3 scripts/package.py
