#!/usr/bin/env bash
set -euo pipefail

# Install the .NET SDK locally if not already installed
if command -v dotnet >/dev/null; then
    echo ".NET SDK already installed"
    exit 0
fi

curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
bash /tmp/dotnet-install.sh --channel 8.0
rm /tmp/dotnet-install.sh

cat <<'EOM'
.NET SDK installed. If the dotnet command is not available, add the following to your shell profile:
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$PATH:$DOTNET_ROOT:$DOTNET_ROOT/tools"
EOM
