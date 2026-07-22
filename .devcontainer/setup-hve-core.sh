#!/usr/bin/env bash
set -euo pipefail

# Best-effort, non-fatal, idempotent install of the GitHub Copilot CLI and the
# hve-core-all plugin (from github.com/microsoft/hve-core).
#
# This script is shared by the Copilot cloud sandbox setup workflow
# (.github/workflows/copilot-setup-steps.yml) and the dev container
# (.devcontainer/devcontainer.json). Every step degrades to a warning instead
# of aborting, so the sandbox / container still starts even if the network is
# unavailable or the Copilot CLI needs interactive authentication.

# 1. Install the Copilot CLI only if it is not already on PATH.
command -v copilot >/dev/null 2>&1 || npm install -g @github/copilot || echo "::warning::copilot CLI install failed"

# 2. Register the hve-core marketplace (idempotent; re-adding is a no-op).
copilot plugin marketplace add microsoft/hve-core || echo "::warning::marketplace add failed"

# 3. Install the hve-core-all plugin from that marketplace.
copilot plugin install hve-core-all@hve-core || echo "::warning::hve-core-all install failed"

# 4. Show the installed plugins for logs/verification (never fatal).
copilot plugin list || true
